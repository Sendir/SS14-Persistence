using Content.Shared._Persistence14.Bastion;
using Content.Shared._Persistence14.PersistentIdentifier;
using Content.Shared.Hands.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Persistence14.Bastion;

/// <summary>
/// Drives the Paragon Artifact key's locator beep. While the key is held, it beeps toward its
/// <see cref="ParagonArtifactLocatorComponent.Target"/> ONLY while the holder is MOVING roughly toward
/// it - so you fly around and the beeps confirm when your heading is right, then quicken as you close
/// in. Silent when not carried, not moving, heading the wrong way, targetless, or on another map.
///
/// It gates on heading (world-space velocity) rather than facing: a stationary player's facing can't
/// be read reliably server-side, whereas which way they're actually travelling is unambiguous.
/// </summary>
public sealed class ParagonArtifactLocatorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PersistentIdentifierSystem _pid = default!;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<ParagonArtifactLocatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var locator, out var xform))
        {
            if (!_pid.TryResolveId(locator.Target, out var targetEnt))
                continue;
            var target = targetEnt.Owner;

            // Only beep while actually held by someone - a held item is parented to the holder mob,
            // which carries HandsComponent. On the ground or in a bag it stays silent.
            var holder = xform.ParentUid;
            if (!HasComp<HandsComponent>(holder))
                continue;

            var holderPos = _transform.GetMapCoordinates(holder);
            var targetPos = _transform.GetMapCoordinates(target);
            if (holderPos.MapId != targetPos.MapId)
                continue;

            var toTarget = targetPos.Position - holderPos.Position;
            var distance = toTarget.Length();

            // Directional gate: stay silent unless the holder is travelling toward the target. Skipped
            // once you're basically on top of it, so it keeps beeping while you search the platform.
            if (distance > locator.ArrivedDistance)
            {
                var velocity = _physics.GetMapLinearVelocity(holder);
                if (velocity.Length() < locator.MinSpeed)
                    continue; // not moving - can't tell heading

                var headingDiff = Angle.ShortestDistance(velocity.ToAngle(), toTarget.ToAngle());
                if (Math.Abs(headingDiff.Theta) > locator.HeadingTolerance.Theta)
                    continue; // heading the wrong way - no beep
            }

            if (now < locator.NextBeep)
                continue;

            var scale = Math.Clamp(distance / locator.MaxScaleDistance, 0f, 1f);
            locator.NextBeep = now + locator.MinInterval + (locator.MaxInterval - locator.MinInterval) * scale;

            _audio.PlayPvs(locator.BeepSound, uid);
        }
    }
}
