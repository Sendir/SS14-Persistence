using System.Numerics;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tether;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Tether;

/// <summary>
/// High-level, reusable tether API pairing the purely-visual <see cref="TetherVisualSystem"/> with two
/// composable concerns:
///
/// <list type="bullet">
/// <item><b>Link</b> (<see cref="TetherLinkComponent"/>): fires <see cref="TetherConnectedEvent"/> when
/// the reach-out finishes, and <see cref="TetherLinkBrokenEvent"/> when a break condition trips
/// (endpoint deleted, target downed, out of range, cross-map) or a consumer calls <see cref="BreakLink"/>.</item>
/// <item><b>Mover</b> (<see cref="TetherMoverComponent"/>): kinematically drives the target to a chosen
/// distance/direction from the source, firing <see cref="TetherMoveEndedEvent"/> when it arrives - or, if
/// the target can collide, when it hits a solid on the way. Movement never breaks the tether, so one
/// continuous tether can fling a thing out, hold it, then reel it back.</item>
/// </list>
///
/// Create links with <see cref="TetherLink"/> and drive them with <see cref="TetherDrive"/>.
/// </summary>
public sealed class TetherLinkSystem : EntitySystem
{
    [Dependency] private readonly TetherVisualSystem _tetherVisual = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TetherMoveTargetComponent, StartCollideEvent>(OnMoveTargetCollide);
    }

    // =====================================================================================
    // Public API
    // =====================================================================================

    /// <summary>
    /// Spawns a visual tether from <paramref name="source"/> to <paramref name="target"/> AND starts
    /// monitoring it for the given break conditions and connect completion. Returns the tether entity.
    /// This is what most gameplay code should use instead of calling
    /// <see cref="TetherVisualSystem.SpawnTether"/> directly.
    /// </summary>
    public EntityUid TetherLink(
        EntityUid source,
        EntityUid target,
        string visualPrototype,
        float? maxDistance = null,
        bool breakOnTargetNotAlive = false,
        bool breakOnDifferentMap = true,
        TimeSpan? connectDuration = null,
        TimeSpan? disconnectDuration = null)
    {
        var tether = _tetherVisual.SpawnTether(source, target, visualPrototype, connectDuration, disconnectDuration);
        var link = EnsureComp<TetherLinkComponent>(tether);
        link.MaxDistance = maxDistance;
        link.BreakOnTargetNotAlive = breakOnTargetNotAlive;
        link.BreakOnDifferentMap = breakOnDifferentMap;
        return tether;
    }

    /// <summary>
    /// Drives the tether's target toward a point at <paramref name="distance"/> from the source. Push
    /// or pull is decided purely by that distance vs. where the target currently is. Safe to call
    /// repeatedly on the same tether to give it a new destination (e.g. fling out, then reel in).
    ///
    /// Movement is a physics velocity drive: on the first drive the target is forced <see cref="BodyType.Dynamic"/>
    /// and thereafter its velocity is steered toward the destination each tick (capped by the mover's Speed,
    /// ramped by its Strength), so it collides and drags for real. Fires <see cref="TetherMoveEndedEvent"/>
    /// on arrival, or <see cref="TetherMoveOutcome.Collided"/> if a collidable target hits something solid.
    /// </summary>
    /// <param name="angle">Fixed direction from the source. Ignored if <paramref name="autoDirection"/>.</param>
    /// <param name="autoDirection">Recompute the direction each tick as the current source→target line.</param>
    public void TetherDrive(EntityUid tether, float distance, Angle? angle = null, bool autoDirection = false)
    {
        if (!TryComp<TetherVisualComponent>(tether, out var visual))
            return;

        var target = visual.Target;
        if (!Exists(target))
            return;

        var mover = EnsureComp<TetherMoverComponent>(tether);
        mover.Distance = distance;
        mover.AutoDirection = autoDirection;
        if (angle is { } a)
            mover.Direction = a;
        mover.Active = true;

        if (mover.Setup)
            return; // already forced kinematic on a previous drive; just retarget it

        // First drive: unanchor and force the target Dynamic so the physics solver moves it by the velocity
        // we set each tick - a real pull that collides and drags. Clear any leftover velocity (from a fling
        // or a pull before we grabbed it) so the drive starts clean.
        _transform.Unanchor(target);
        if (TryComp<PhysicsComponent>(target, out var targetPhysics))
        {
            mover.OriginalBodyType = targetPhysics.BodyType;
            _physics.SetBodyType(target, BodyType.Dynamic, body: targetPhysics);
            _physics.SetLinearVelocity(target, Vector2.Zero, body: targetPhysics);
            _physics.SetAngularVelocity(target, 0f, body: targetPhysics);
        }

        EnsureComp<TetherMoveTargetComponent>(target).Tether = tether;
        mover.Setup = true;
    }

    /// <summary>
    /// Forcibly breaks a link (default reason <see cref="TetherBreakReason.Manual"/>): tears down any
    /// mover rig, retracts the visual, and raises <see cref="TetherLinkBrokenEvent"/> on both surviving
    /// endpoints. Safe to call more than once / on a non-link entity.
    /// </summary>
    public void BreakLink(EntityUid tether, TetherBreakReason reason = TetherBreakReason.Manual)
    {
        if (!TryComp<TetherVisualComponent>(tether, out var visual))
            return;

        DoBreak(tether, visual, reason);
    }

    // =====================================================================================
    // Update: connect + break monitoring, and the active drive
    // =====================================================================================

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var linkQuery = EntityQueryEnumerator<TetherLinkComponent, TetherVisualComponent>();
        while (linkQuery.MoveNext(out var uid, out var link, out var visual))
        {
            if (visual.DisconnectStartedAt != null)
                continue; // already retracting

            // Connect ("reach out") finished → fire once.
            if (!link.Established
                && visual.ConnectStartedAt is { } startedAt
                && _timing.CurTime - startedAt >= visual.ConnectDuration)
            {
                link.Established = true;
                var connected = new TetherConnectedEvent(uid, visual.Source, visual.Target);
                if (Exists(visual.Source))
                    RaiseLocalEvent(visual.Source, ref connected);
                if (Exists(visual.Target))
                    RaiseLocalEvent(visual.Target, ref connected);
            }

            var sourceGone = !Exists(visual.Source);
            var targetGone = !Exists(visual.Target);
            if (sourceGone || targetGone)
            {
                DoBreak(uid, visual, sourceGone ? TetherBreakReason.SourceGone : TetherBreakReason.TargetGone);
                continue;
            }

            if (link.BreakOnTargetNotAlive
                && HasComp<MobStateComponent>(visual.Target)
                && !_mobState.IsAlive(visual.Target))
            {
                DoBreak(uid, visual, TetherBreakReason.TargetDied);
                continue;
            }

            if (!link.BreakOnDifferentMap && link.MaxDistance is null)
                continue;

            var srcCoords = _transform.GetMapCoordinates(visual.Source);
            var tgtCoords = _transform.GetMapCoordinates(visual.Target);
            if (srcCoords.MapId != tgtCoords.MapId)
            {
                if (link.BreakOnDifferentMap)
                    DoBreak(uid, visual, TetherBreakReason.DifferentMap);
                continue;
            }

            if (link.MaxDistance is { } maxDistance
                && (tgtCoords.Position - srcCoords.Position).Length() > maxDistance)
            {
                DoBreak(uid, visual, TetherBreakReason.OutOfRange);
            }
        }

        var moverQuery = EntityQueryEnumerator<TetherMoverComponent, TetherVisualComponent>();
        while (moverQuery.MoveNext(out var uid, out var mover, out var visual))
        {
            if (visual.DisconnectStartedAt != null || !mover.Active)
                continue;

            if (!Exists(visual.Source) || !Exists(visual.Target))
                continue; // the link loop will break it; nothing sane to drive toward

            var sourcePos = _transform.GetWorldPosition(visual.Source);
            var targetPos = _transform.GetWorldPosition(visual.Target);

            Angle dir;
            if (mover.AutoDirection)
            {
                var delta = targetPos - sourcePos;
                dir = delta.LengthSquared() > 0.0001f ? delta.ToAngle() : mover.Direction;
            }
            else
            {
                dir = mover.Direction;
            }

            if (!TryComp<PhysicsComponent>(visual.Target, out var targetPhysics))
                continue;

            var destination = sourcePos + dir.ToVec() * mover.Distance;
            var toDest = destination - targetPos;
            var dist = toDest.Length();

            if (dist <= mover.ArriveTolerance)
            {
                _physics.SetLinearVelocity(visual.Target, Vector2.Zero, body: targetPhysics);
                mover.Active = false;
                RaiseMoveEnded(uid, visual, TetherMoveOutcome.Reached, null);
                continue;
            }

            // Velocity drive: command a speed toward the destination, capped at Speed and slowed as we near
            // so we don't overshoot. Ramp our COMMANDED speed toward that by Strength (accel) and drive at
            // exactly it - tracking the commanded speed on the mover (not reading the body's velocity) means
            // damping or bumping other mobs can't bleed it away, so every target reels at the same rate. The
            // physics solver still does the moving, so it collides and fires Collided on hitting something solid.
            var desiredSpeed = MathF.Min(mover.Speed, dist / frameTime);
            var accel = mover.Strength * frameTime;
            mover.CurrentSpeed = mover.CurrentSpeed < desiredSpeed
                ? MathF.Min(desiredSpeed, mover.CurrentSpeed + accel)
                : MathF.Max(desiredSpeed, mover.CurrentSpeed - accel);
            _physics.SetLinearVelocity(visual.Target, toDest / dist * mover.CurrentSpeed, body: targetPhysics);
        }
    }

    /// <summary>
    /// A driven target that can collide hit something solid: end the drive and report it (does NOT break
    /// the tether). Only fires for targets left collidable by the consumer; phasing drives never hit this.
    /// </summary>
    private void OnMoveTargetCollide(Entity<TetherMoveTargetComponent> ent, ref StartCollideEvent args)
    {
        var tether = ent.Comp.Tether;
        if (!TryComp<TetherMoverComponent>(tether, out var mover) || !mover.Active)
            return;
        if (!TryComp<TetherVisualComponent>(tether, out var visual))
            return;

        // Ignore the source endpoint and non-blocking (sensor) contacts.
        if (args.OtherEntity == visual.Source)
            return;
        if (!args.OurFixture.Hard || !args.OtherFixture.Hard)
            return;

        mover.Active = false;
        RaiseMoveEnded(tether, visual, TetherMoveOutcome.Collided, args.OtherEntity);
    }

    // =====================================================================================
    // Teardown
    // =====================================================================================

    private void DoBreak(EntityUid tether, TetherVisualComponent visual, TetherBreakReason reason)
    {
        var source = visual.Source;
        var target = visual.Target;

        CleanupMover(tether, target);
        RemCompDeferred<TetherLinkComponent>(tether);
        _tetherVisual.BeginDisconnect(tether, visual);

        var ev = new TetherLinkBrokenEvent(tether, source, target, reason);
        if (Exists(source))
            RaiseLocalEvent(source, ref ev);
        if (Exists(target))
            RaiseLocalEvent(target, ref ev);
    }

    /// <summary>Restores the dragged target's original body type, if a mover was present.</summary>
    private void CleanupMover(EntityUid tether, EntityUid target)
    {
        if (!TryComp<TetherMoverComponent>(tether, out var mover))
            return;

        if (mover.Setup && Exists(target) && TryComp<PhysicsComponent>(target, out var targetPhysics))
        {
            // Kill the drive velocity before restoring the body type, so it doesn't fly off with whatever
            // speed the reel had it at when released.
            _physics.SetLinearVelocity(target, Vector2.Zero, body: targetPhysics);
            _physics.SetAngularVelocity(target, 0f, body: targetPhysics);
            _physics.SetBodyType(target, mover.OriginalBodyType, body: targetPhysics);
        }

        RemComp<TetherMoveTargetComponent>(target);
        RemCompDeferred<TetherMoverComponent>(tether);
    }

    private void RaiseMoveEnded(EntityUid tether, TetherVisualComponent visual, TetherMoveOutcome outcome, EntityUid? hit)
    {
        var ev = new TetherMoveEndedEvent(tether, visual.Source, visual.Target, outcome, hit);
        if (Exists(visual.Source))
            RaiseLocalEvent(visual.Source, ref ev);
        if (Exists(visual.Target))
            RaiseLocalEvent(visual.Target, ref ev);
    }
}

/// <summary>
/// Raised (by-ref, directed) on both endpoints once the tether's connect ("reach out") animation
/// finishes. Handy for "the tether reaches out, THEN the thing at the far end appears".
/// </summary>
[ByRefEvent]
public readonly record struct TetherConnectedEvent(EntityUid Tether, EntityUid Source, EntityUid Target);

/// <summary>
/// Raised (by-ref, directed) on both endpoints when an active drive ends - either it
/// <see cref="TetherMoveOutcome.Reached"/> its destination or it <see cref="TetherMoveOutcome.Collided"/>
/// with a solid. Does NOT break the tether; the consumer decides what happens next (drive again,
/// break, activate, etc.). <see cref="HitEntity"/> is set only for a collision.
/// </summary>
[ByRefEvent]
public readonly record struct TetherMoveEndedEvent(
    EntityUid Tether,
    EntityUid Source,
    EntityUid Target,
    TetherMoveOutcome Outcome,
    EntityUid? HitEntity);

public enum TetherMoveOutcome : byte
{
    /// <summary>The target reached the destination distance/direction.</summary>
    Reached,

    /// <summary>The target hit something solid on the way (using its normal collision).</summary>
    Collided,
}

/// <summary>
/// Raised (by-ref, directed) on both surviving endpoints when a link breaks, for any reason. Subscribe
/// on whichever endpoint your system owns a component on. The visual is already retracting by the time
/// this fires.
/// </summary>
[ByRefEvent]
public readonly record struct TetherLinkBrokenEvent(
    EntityUid Tether,
    EntityUid Source,
    EntityUid Target,
    TetherBreakReason Reason);

public enum TetherBreakReason : byte
{
    /// <summary>The source endpoint stopped existing.</summary>
    SourceGone,

    /// <summary>The target endpoint stopped existing.</summary>
    TargetGone,

    /// <summary>The target went critical or dead (<see cref="TetherLinkComponent.BreakOnTargetNotAlive"/>).</summary>
    TargetDied,

    /// <summary>The endpoints drifted more than <see cref="TetherLinkComponent.MaxDistance"/> apart.</summary>
    OutOfRange,

    /// <summary>The endpoints ended up on different maps.</summary>
    DifferentMap,

    /// <summary>A consumer called <see cref="TetherLinkSystem.BreakLink"/>.</summary>
    Manual,
}
