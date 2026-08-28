using System.Numerics;
using Content.Server.Tether;
using Content.Shared._Persistence14.Bastion;
using Content.Shared._Persistence14.PersistentIdentifier;
using Content.Shared.EntityTable;
using Content.Shared.Interaction.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Pulling.Events;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>
/// Defense C: the Paragon summons waves of guardian mobs (the ParagonBastion faction - one team, hostile
/// to all but the eye hivemind). Wave tier scales with unlock severity (weak → medium → deadly pools) and
/// a live-mob cap prevents a long ruin from flooding. Mobs walk, so no per-wave state machine is needed.
///
/// It also runs the space-rescue tether every <see cref="BastionDefenseMobsComponent.FetchScanInterval"/>:
/// any Bastion mob (wave OR Paragon-node spawned - both carry <see cref="BastionMobComponent"/>) that has
/// drifted off the ruin platform or past the leash radius is reeled home on a tether and then released
/// intact (the tether restores its body type on break), so it keeps fighting on solid floor.
/// </summary>
public sealed class BastionDefenseMobsSystem : BaseBastionDefenseSystem<BastionDefenseMobsComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly TetherLinkSystem _tether = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly PersistentIdentifierSystem _pid = default!;

    private readonly HashSet<Entity<BastionMobComponent>> _strays = new();

    public override void Initialize()
    {
        base.Initialize(); // subscribes OnPulse
        SubscribeLocalEvent<BastionDefenseMobsComponent, TetherMoveEndedEvent>(OnMoveEnded);
        SubscribeLocalEvent<BastionDefenseMobsComponent, TetherLinkBrokenEvent>(OnLinkBroken);
        SubscribeLocalEvent<BastionMobComponent, BeingPulledAttemptEvent>(OnMobPullAttempt);
    }

    /// <summary>No one can grab a guardian while the ruin is reeling it home - the tether has sole claim on it.</summary>
    private void OnMobPullAttempt(Entity<BastionMobComponent> ent, ref BeingPulledAttemptEvent args)
    {
        if (_pid.TryResolveId(ent.Comp.FetchTether, out _))
            args.Cancel();
    }

    /// <summary>Pulse = summon a severity-scaled wave, up to the live-mob cap.</summary>
    protected override void OnPulse(Entity<BastionDefenseMobsComponent> ent, ref BastionDefensePulseEvent args)
    {
        var comp = ent.Comp;
        PruneActive(comp);

        var available = comp.MaxAlive - comp.Active.Count;
        if (available <= 0)
            return; // already at the cap; let the field thin out before summoning more

        var severity = Math.Clamp(args.Severity, 0f, 1f);
        var count = Math.Min(GetWaveCount(comp, severity), available);

        var pool = severity >= comp.DeadlySeverity ? comp.DeadlyPool
                 : severity >= comp.MediumSeverity ? comp.MediumPool
                 : comp.WeakPool;
        if (!_proto.TryIndex(pool, out var table))
            return;

        var centre = _transform.GetMapCoordinates(ent.Owner);
        for (var i = 0; i < count; i++)
        {
            foreach (var proto in _entityTable.GetSpawns(table)) // GroupSelector => one mob per roll
            {
                var off = new Vector2(
                    _random.NextFloat(-comp.SpawnRadius, comp.SpawnRadius),
                    _random.NextFloat(-comp.SpawnRadius, comp.SpawnRadius));
                var mob = Spawn(proto, new MapCoordinates(centre.Position + off, centre.MapId));
                comp.Active.Add(_pid.EnsureId(mob)); // store a persistent id ref (implicitly from the id string)
            }
        }
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<BastionDefenseMobsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextFetchScan)
                continue;

            comp.NextFetchScan = now + comp.FetchScanInterval;
            FetchStrays((uid, comp), now);
        }
    }

    /// <summary>
    /// Reels home any Bastion mob near this Paragon that has left the ruin grid or wandered past the leash
    /// radius - the "mob adrift in space can't walk back" case, plus a soft leash so guardians stay on the
    /// platform where they threaten intruders.
    /// </summary>
    private void FetchStrays(Entity<BastionDefenseMobsComponent> ent, TimeSpan now)
    {
        var paragon = ent.Owner;
        var grid = Transform(paragon).GridUid;
        var centre = _transform.GetMapCoordinates(paragon);

        _strays.Clear();
        _lookup.GetEntitiesInRange(centre, ent.Comp.FetchScanRadius, _strays);

        foreach (var mob in _strays)
        {
            var mc = mob.Comp;

            if (_pid.TryResolveId(mc.FetchTether, out var active))
            {
                // Release once reeled within range. We detach here rather than waiting on the tether's own
                // "Reached" (a collidable mob can't reliably hit the exact ReelDistance and would hang forever).
                if ((_transform.GetWorldPosition(mob) - centre.Position).Length() <= ent.Comp.ReelDistance)
                    _tether.BreakLink(active.Owner);
                continue; // still reeling
            }

            mc.FetchTether = default; // no live tether (empty ref, or it vanished) - fall through and re-fetch

            if (mc.FetchCooldownUntil is { } cd && now < cd)
                continue; // just released; give traversal a tick to re-anchor it
            if (TryComp<MobStateComponent>(mob, out var state) && !_mobState.IsAlive(mob, state))
                continue; // dead guardians aren't worth fetching

            var mxform = Transform(mob);
            var offGrid = mxform.GridUid != grid;
            var far = (_transform.GetWorldPosition(mob) - centre.Position).Length() > ent.Comp.LeashRadius;
            if (!offGrid && !far)
                continue;

            // Rip it out of anyone's grip - the ruin's reel takes priority, and while tethered nobody can
            // re-grab it (see OnMobPullAttempt), so a pull can never fight the reel.
            if (TryComp<PullableComponent>(mob, out var pullable) && pullable.BeingPulled)
                _pulling.TryStopPull(mob, pullable);

            var tether = _tether.TetherLink(paragon, mob, ent.Comp.TetherVisual, breakOnTargetNotAlive: true);
            _tether.TetherDrive(tether, ent.Comp.ReelDistance, autoDirection: true);
            mc.FetchTether = _pid.EnsureId(tether); // persistent ref so a reload can't alias it to the wrong entity

            // The mob stays collidable. The tether reels it home (kinematic drive, so a pull can't win) and
            // releases on the first TetherMoveEndedEvent (OnMoveEnded): either it arrives within ReelDistance
            // of the Paragon, or it hits something solid it can't pass and detaches there. BlockMovement
            // satisfies TileFrictionController (an InputMover entity must be a KinematicController unless
            // movement-blocked) and stops the mob steering against the reel; it's removed on release.
            EnsureComp<BlockMovementComponent>(mob);
        }
    }

    /// <summary>
    /// The reel ended - the mob either arrived within ReelDistance of the Paragon or hit something solid it
    /// couldn't pass. Either way, detach: break the tether (which restores its body so it walks again).
    /// </summary>
    private void OnMoveEnded(Entity<BastionDefenseMobsComponent> ent, ref TetherMoveEndedEvent args)
    {
        if (TryComp<BastionMobComponent>(args.Target, out var mc) && _pid.CompareId(mc.FetchTether, args.Tether))
            _tether.BreakLink(args.Tether);
    }

    /// <summary>
    /// Rescue tether ended: clear the mob's fetch state and brief-cooldown it. Unlike Defense A the mob is
    /// NOT deleted - guardians persist and keep fighting. The visual retracts on its own (the mob still
    /// exists, so there's no dangling-target PVS spam to delete around).
    /// </summary>
    private void OnLinkBroken(Entity<BastionDefenseMobsComponent> ent, ref TetherLinkBrokenEvent args)
    {
        if (TryComp<BastionMobComponent>(args.Target, out var mc) && _pid.CompareId(mc.FetchTether, args.Tether))
        {
            mc.FetchTether = default;
            mc.FetchCooldownUntil = _timing.CurTime + TimeSpan.FromSeconds(2);
            RemComp<BlockMovementComponent>(args.Target); // reel over: let it walk and fight again
        }
    }

    private void PruneActive(BastionDefenseMobsComponent comp)
    {
        comp.Active.RemoveAll(m =>
            !_pid.TryResolveId(m, out var mob)
            || (TryComp<MobStateComponent>(mob.Owner, out var state) && _mobState.IsDead(mob.Owner, state)));
    }
}
