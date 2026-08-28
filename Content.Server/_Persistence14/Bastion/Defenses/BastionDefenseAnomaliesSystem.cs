using System.Numerics;
using Content.Server.Anomaly;
using Content.Server.Spawners.Components;
using Content.Server.Tether;
using Content.Shared._Persistence14.PersistentIdentifier;
using Content.Shared.Anomaly.Components;
using Content.Shared.Sprite;
using Content.Shared.Tether;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>
/// Defense B: each pulse the Paragon flings tethers onto its platform and grows a hostile anomaly in at
/// each endpoint, then ramps that anomaly's severity toward supercritical. The tether is a leash: the only
/// thing that can move a Static anomaly is the G.O.R.I.L.L.A. gauntlet, which punches it through the air -
/// knock one past <see cref="BastionDefenseAnomaliesComponent.LeashRange"/> and the tether snaps and the
/// anomaly is forced supercritical on the spot. Otherwise it crits on its own ramp. The anomaly's own crit
/// deletes it, so there is nothing to reel back (and the tether self-cleans when its target is gone).
///
/// The anomaly pool is reused from the generator's <c>RandomAnomalySpawner</c> list, so new anomaly types
/// are picked up automatically. That list's rare entry is the body-injector TRAP: a trap has no standalone
/// anomaly to ramp, so it gets the alternate treatment - the tether reaches out, holds a beat, then detaches,
/// leaving the trap lurking there.
/// </summary>
public sealed class BastionDefenseAnomaliesSystem : BaseBastionDefenseSystem<BastionDefenseAnomaliesComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TetherLinkSystem _tether = default!;
    [Dependency] private readonly AnomalySystem _anomaly = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scale = default!;
    [Dependency] private readonly SharedXenoArtifactSystem _xenoArtifact = default!;
    [Dependency] private readonly PersistentIdentifierSystem _pid = default!;

    public override void Initialize()
    {
        base.Initialize(); // subscribes OnPulse
        SubscribeLocalEvent<BastionAnomalyComponent, TetherLinkBrokenEvent>(OnLinkBroken);
        SubscribeLocalEvent<BastionAnomalyComponent, EntityTerminatingEvent>(OnAnomalyTerminating);
        // The reach-out tether fires this on the Paragon (its source) - that's when we spawn the anomaly.
        SubscribeLocalEvent<BastionDefenseAnomaliesComponent, TetherConnectedEvent>(OnReachConnected);
    }

    /// <summary>
    /// A reach-toward-coords tether has arrived: NOW spawn the anomaly (or trap) at the reach point, bind the
    /// tether to it, and start it growing/ramping (or, for a trap, holding-then-detaching). Nothing existed at
    /// the destination until this moment - the tether reached out to empty space first.
    /// </summary>
    private void OnReachConnected(Entity<BastionDefenseAnomaliesComponent> ent, ref TetherConnectedEvent args)
    {
        // Only a still-unbound reach tether (no target entity, a target position) is ours to fulfil here.
        if (!TryComp<TetherVisualComponent>(args.Tether, out var visual) || visual.TargetCoords is not { } coords)
            return;

        var comp = ent.Comp;
        var now = _timing.CurTime;

        if (SpawnAnomaly(comp.AnomalySpawner, coords) is not { } spawned)
        {
            _tether.BreakLink(args.Tether); // pool resolved to nothing usable; drop the empty tether
            return;
        }

        var isTrap = !HasComp<AnomalyComponent>(spawned);

        // Bind the tether to the freshly-spawned thing; real anomalies get the gauntlet leash.
        _tether.AttachTarget(args.Tether, spawned, maxDistance: isTrap ? null : comp.LeashRange);

        var marker = AddComp<BastionAnomalyComponent>(spawned);
        _pid.AssignIdReference(ref marker.Paragon, ent.Owner);
        _pid.AssignIdReference(ref marker.Tether, args.Tether);
        // Recompute severity here (not the pulse's stale value): the tether reach takes a couple of seconds,
        // over which the players may have unlocked more of the graph.
        var severity = TryComp<XenoArtifactComponent>(ent.Owner, out var xeno)
            ? _xenoArtifact.GetUnlockedFraction((ent.Owner, xeno))
            : 0f;
        marker.RampPerSecond = comp.MinRampPerSecond + (comp.MaxRampPerSecond - comp.MinRampPerSecond) * severity;

        if (isTrap)
        {
            // No anomaly to ramp: hold a beat, then detach and leave the trap lurking there.
            marker.Phase = BastionAnomalyPhase.TrapDelivering;
            marker.PhaseEndsAt = now + comp.TrapHoldTime;
            return;
        }

        // Real anomaly (stability/severity left at the generator's own RNG roll): grow in, then ramp to crit.
        marker.StartScale = comp.StartScale;
        marker.GrowDuration = comp.GrowDuration;
        marker.Phase = BastionAnomalyPhase.Growing;
        marker.GrowStartedAt = now;
        marker.PhaseEndsAt = now + comp.GrowDuration;
        _scale.SetSpriteScale(spawned, Vector2.One * comp.StartScale);
    }

    /// <summary>
    /// The anomaly is being deleted (its own supercritical ended it, or teardown). Break its tether NOW,
    /// while the anomaly still resolves, so the tether snapshots the anomaly's last position and retracts
    /// toward it - and clears its now-dangling target ref before the next state build, closing the
    /// "can't resolve MetaDataComponent" PVS window regardless of system update order.
    /// </summary>
    private void OnAnomalyTerminating(Entity<BastionAnomalyComponent> ent, ref EntityTerminatingEvent args)
    {
        if (_pid.TryResolveId(ent.Comp.Tether, out var tether))
            _tether.BreakLink(tether.Owner);
    }

    /// <summary>
    /// Pulse = fling a severity-scaled batch of tethers out onto the platform, up to the live cap. Each tether
    /// reaches toward empty space; the anomaly is spawned only when it arrives (see <see cref="OnReachConnected"/>).
    /// </summary>
    protected override void OnPulse(Entity<BastionDefenseAnomaliesComponent> ent, ref BastionDefensePulseEvent args)
    {
        var comp = ent.Comp;

        var available = comp.MaxAlive - CountAlive(ent.Owner);
        if (available <= 0)
            return; // already at the cap; let the field crit itself out before growing more

        var count = Math.Min(GetWaveCount(comp, args.Severity), available);

        var centre = _transform.GetMapCoordinates(ent.Owner);

        for (var i = 0; i < count; i++)
        {
            var angle = _random.NextAngle();
            var distance = _random.NextFloat(comp.MinDistance, comp.MaxDistance);
            var coords = new MapCoordinates(centre.Position + angle.ToVec() * distance, centre.MapId);

            _tether.TetherLinkToCoords(
                ent.Owner, coords, comp.TetherVisual,
                connectDuration: comp.TetherConnectDuration,
                disconnectDuration: comp.TetherDisconnectDuration);
        }
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<BastionAnomalyComponent>();
        while (query.MoveNext(out var uid, out var marker))
        {
            switch (marker.Phase)
            {
                case BastionAnomalyPhase.Growing:
                    var span = (marker.PhaseEndsAt - marker.GrowStartedAt).TotalSeconds;
                    var t = span > 0 ? (float)Math.Clamp((now - marker.GrowStartedAt).TotalSeconds / span, 0d, 1d) : 1f;
                    var s = marker.StartScale + (1f - marker.StartScale) * t;
                    _scale.SetSpriteScale(uid, Vector2.One * s);
                    if (t >= 1f)
                        marker.Phase = BastionAnomalyPhase.Ramping;
                    break;

                case BastionAnomalyPhase.Ramping:
                    // Once it's begun going critical, stop pushing - the anomaly system finishes it and deletes it.
                    if (HasComp<AnomalySupercriticalComponent>(uid))
                        break;
                    if (TryComp<AnomalyComponent>(uid, out var anom))
                        _anomaly.ChangeAnomalySeverity(uid, marker.RampPerSecond * frameTime, anom);
                    break;

                case BastionAnomalyPhase.TrapDelivering:
                    if (now < marker.PhaseEndsAt)
                        break;
                    // Delivery done: detach the tether and forget the trap - it stays as a lurking hazard.
                    if (_pid.TryResolveId(marker.Tether, out var trapTether))
                        _tether.BreakLink(trapTether.Owner);
                    RemCompDeferred<BastionAnomalyComponent>(uid);
                    break;
            }
        }
    }

    /// <summary>
    /// A leash broke. The one we care about is <see cref="TetherBreakReason.OutOfRange"/>: the anomaly was
    /// punched off its leash (only the G.O.R.I.L.L.A. gauntlet can move a Static anomaly), so it goes
    /// supercritical immediately. Every other reason needs nothing - a self-crit already ended the anomaly
    /// (and the tether self-cleans), and a trap's own detach is a Manual break.
    /// </summary>
    private void OnLinkBroken(Entity<BastionAnomalyComponent> ent, ref TetherLinkBrokenEvent args)
    {
        if (!_pid.CompareId(ent.Comp.Tether, args.Tether))
            return;

        if (args.Reason == TetherBreakReason.OutOfRange && HasComp<AnomalyComponent>(ent.Owner))
            _anomaly.StartSupercriticalEvent(ent.Owner);

        ent.Comp.Tether = default; // link's gone; drop the reference
    }

    private int CountAlive(EntityUid paragon)
    {
        var paragonId = _pid.EnsureId(paragon); // resolve once, then string-compare each marker's stored ref
        var count = 0;
        var query = EntityQueryEnumerator<BastionAnomalyComponent>();
        while (query.MoveNext(out _, out var marker))
        {
            if (marker.Paragon.TargetId == paragonId)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Picks one entity from a random-spawner's list the same way <c>RandomSpawner</c> would (rare list at
    /// RareChance, else the common list) and spawns it directly, so we get a handle on the result. A pick can
    /// itself be a nested spawner (e.g. the rock-anomaly spawner, or the injector-trap spawner in the rare
    /// slot); those are resolved recursively. Returns the spawned anomaly OR trap, or null if the list
    /// resolves to nothing usable.
    /// </summary>
    private EntityUid? SpawnAnomaly(EntProtoId spawnerId, MapCoordinates coords, int depth = 0)
    {
        if (depth > 4)
            return null;

        if (!_proto.TryIndex(spawnerId, out var proto) ||
            !proto.TryGetComponent<RandomSpawnerComponent>(out var spawner, EntityManager.ComponentFactory))
            return null;

        var list = spawner.RarePrototypes.Count > 0 && _random.Prob(spawner.RareChance)
            ? spawner.RarePrototypes
            : spawner.Prototypes;
        if (list.Count == 0)
            return null;

        var picked = _random.Pick(list);

        // Nested spawner? Resolve it rather than spawning the marker (which would spawn its own anomaly on
        // MapInit and rob us of the handle we need to tether/grow it).
        if (_proto.TryIndex(picked, out var pickedProto) &&
            pickedProto.Components.ContainsKey(EntityManager.ComponentFactory.GetComponentName<RandomSpawnerComponent>()))
            return SpawnAnomaly(picked, coords, depth + 1);

        var spawned = Spawn(picked, coords);

        // Only anomalies and anomaly traps belong here; anything else from an unexpected list entry is junk.
        if (!HasComp<AnomalyComponent>(spawned) && !HasComp<InnerBodyAnomalyInjectorComponent>(spawned))
        {
            QueueDel(spawned);
            return null;
        }

        return spawned;
    }
}
