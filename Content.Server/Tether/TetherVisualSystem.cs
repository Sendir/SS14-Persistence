using Content.Shared.Tether;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Tether;

/// <summary>
/// Server-side half of the tether visual effect
/// </summary>
public sealed class TetherVisualSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Spawns a new tether-visual entity from the given prototype, bridging source and target.
    /// The prototype should have a TetherVisualComponent (Source/Target get overwritten here
    /// regardless of whatever the prototype declares) plus a Sprite with at least SegmentCount
    /// layers for the client to individually position along the curve.
    /// </summary>
    /// <param name="connectDuration">
    /// If given, overrides the prototype's ConnectDuration - how long the tether visually takes
    /// to extend from source out to target ("reach out" travel time).
    /// </param>
    /// <param name="disconnectDuration">
    /// If given, overrides the prototype's DisconnectDuration - how long the tether visually
    /// takes to retract once BeginDisconnect is called, before it's deleted.
    /// </param>
    public EntityUid SpawnTether(EntityUid source, EntityUid target, string prototype,
        TimeSpan? connectDuration = null, TimeSpan? disconnectDuration = null)
    {
        var tether = Spawn(prototype, _transform.GetMapCoordinates(source));
        var comp = EnsureComp<TetherVisualComponent>(tether);
        comp.Source = source;
        comp.Target = target;
        comp.ConnectStartedAt = _timing.CurTime;

        if (connectDuration is { } connect)
            comp.ConnectDuration = connect;
        if (disconnectDuration is { } disconnect)
            comp.DisconnectDuration = disconnect;

        Dirty(tether, comp);

        Log.Info($"Spawned tether {ToPrettyString(tether)} from {ToPrettyString(source)} to {ToPrettyString(target)} " +
                 $"(connect: {comp.ConnectDuration.TotalSeconds:F2}s, disconnect: {comp.DisconnectDuration.TotalSeconds:F2}s).");

        return tether;
    }

    /// <summary>
    /// Like <see cref="SpawnTether"/>, but reaches toward a fixed map position instead of a target entity -
    /// for "reach out to empty space, then spawn the thing on arrival". The tether has no <see cref="TetherVisualComponent.Target"/>
    /// until <see cref="TetherLinkSystem.AttachTarget"/> is called (typically on the connect event).
    /// </summary>
    public EntityUid SpawnTetherToCoords(EntityUid source, MapCoordinates coords, string prototype,
        TimeSpan? connectDuration = null, TimeSpan? disconnectDuration = null)
    {
        var tether = Spawn(prototype, _transform.GetMapCoordinates(source));
        var comp = EnsureComp<TetherVisualComponent>(tether);
        comp.Source = source;
        comp.Target = EntityUid.Invalid;
        comp.TargetCoords = coords;
        comp.ConnectStartedAt = _timing.CurTime;

        if (connectDuration is { } connect)
            comp.ConnectDuration = connect;
        if (disconnectDuration is { } disconnect)
            comp.DisconnectDuration = disconnect;

        Dirty(tether, comp);

        Log.Info($"Spawned tether {ToPrettyString(tether)} from {ToPrettyString(source)} reaching toward {coords} " +
                 $"(connect: {comp.ConnectDuration.TotalSeconds:F2}s, disconnect: {comp.DisconnectDuration.TotalSeconds:F2}s).");

        return tether;
    }

    /// <summary>
    /// Starts the tether's retract animation; the entity deletes itself once DisconnectDuration
    /// has elapsed. Safe to call more than once (subsequent calls are ignored) and safe to call
    /// with a zero DisconnectDuration (deletes on the next update, matching the old instant
    /// behavior). Prefer this over deleting the tether entity directly, unless an instant
    /// disappearance is genuinely wanted.
    /// </summary>
    public void BeginDisconnect(EntityUid tetherUid, TetherVisualComponent? comp = null)
    {
        if (!Resolve(tetherUid, ref comp, false))
            return;

        if (comp.DisconnectStartedAt != null)
            return; // already disconnecting

        // Pin the retract anchor. LastKnownTargetCoords is kept current tick-by-tick while the tether is
        // connected (see Update), so it already holds the target's last good position; refresh it here from a
        // still-living target to catch the exact final position. Then clear the (possibly dangling) Target
        // ref: from here on the client anchors the retract to LastKnownTargetCoords, not the live entity, so
        // there is no reason to keep networking a Target that may no longer exist - doing so would spam
        // "can't resolve MetaDataComponent" from PVS every tick until the tether finally deletes.
        if (Exists(comp.Target))
            comp.LastKnownTargetCoords = _transform.GetMapCoordinates(comp.Target);
        comp.Target = EntityUid.Invalid;

        comp.DisconnectStartedAt = _timing.CurTime;
        Dirty(tetherUid, comp);

        Log.Info($"Tether {ToPrettyString(tetherUid)} disconnecting - retracting over {comp.DisconnectDuration.TotalSeconds:F2}s before deletion.");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TetherVisualComponent>();
        while (query.MoveNext(out var uid, out var tether))
        {
            // Once disconnecting, just run out the retract timer. Endpoints no longer matter here - Target
            // has been cleared to Invalid and the retract anchors to LastKnownTargetCoords - so a deleted
            // target can't short-circuit the animation.
            if (tether.DisconnectStartedAt is { } startedAt)
            {
                if (_timing.CurTime - startedAt >= tether.DisconnectDuration)
                    QueueDel(uid);
                continue;
            }

            if (!Exists(tether.Source) || !Exists(tether.Target))
            {
                // A linked tether's teardown (and its break event) is owned by TetherLinkSystem - let
                // it observe the missing endpoint and fire TetherLinkBrokenEvent first (which begins the
                // retract), rather than silently deleting the tether out from under it here.
                if (HasComp<TetherLinkComponent>(uid))
                    continue;

                // An unlinked tether with a gone endpoint has no owner to retract it - just delete.
                QueueDel(uid);
                continue;
            }

            // Keep the last-known target position current so a disconnect that begins AFTER the target has
            // already vanished still has somewhere sensible to retract toward. Only re-network on an actual
            // change, so a stationary target (e.g. a Static anomaly) dirties this exactly once.
            var pos = _transform.GetMapCoordinates(tether.Target);
            if (tether.LastKnownTargetCoords != pos)
            {
                tether.LastKnownTargetCoords = pos;
                Dirty(uid, tether);
            }
        }
    }
}
