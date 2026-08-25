using System.Linq;
using Content.Server.Tether;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>
/// Defense A: the Paragon spawns suppressed, partially-unlocked artifacts, flings them out on tethers,
/// holds, pulses them (x3 effects fire out in the field), then reels them onto the Paragon and deletes them.
///
/// A wave is a small per-instance state machine on <see cref="BastionDefenseArtifactsComponent"/>:
/// <list type="number">
/// <item><b>OnPulse</b> (Idle): spawn N artifacts (N scales with severity) at the Paragon centre, roll a
/// depth-biased random unlock on each, tether + fling each out. Go to Holding.</item>
/// <item><b>Update</b> (Holding, on timeout): pulse each artifact (the effects) and start reeling them
/// back in. Go to Reeling.</item>
/// <item><b>OnMoveEnded</b> (Reeling): as each artifact reaches the Paragon, break its tether, which
/// deletes both the artifact and the tether.</item>
/// </list>
/// </summary>
public sealed class BastionDefenseArtifactsSystem : BaseBastionDefenseSystem<BastionDefenseArtifactsComponent>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TetherLinkSystem _tether = default!;
    [Dependency] private readonly SharedXenoArtifactSystem _xenoArtifact = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize(); // subscribes OnPulse
        SubscribeLocalEvent<BastionDefenseArtifactsComponent, TetherMoveEndedEvent>(OnMoveEnded);
        SubscribeLocalEvent<BastionDefenseArtifactsComponent, TetherLinkBrokenEvent>(OnLinkBroken);
    }

    /// <summary>Pulse = start a new wave, unless one is already in flight.</summary>
    protected override void OnPulse(Entity<BastionDefenseArtifactsComponent> ent, ref BastionDefensePulseEvent args)
    {
        var comp = ent.Comp;
        if (comp.Phase != BastionDefenseArtifactsPhase.Idle)
            return; // a wave is still live; ignore this pulse

        var severity = Math.Clamp(args.Severity, 0f, 1f);
        var count = Math.Max(1, (int)MathF.Round(comp.MinCount + (comp.MaxCount - comp.MinCount) * severity));
        var centre = _transform.GetMapCoordinates(ent.Owner);

        for (var i = 0; i < count; i++)
        {
            var artifact = Spawn(comp.ArtifactProto, centre);

            // Strip its mob/pull comps and collision; the tether mover drives it kinematically.
            PrepareArtifactBody(artifact);

            // The graph generated on spawn (MapInit) starts fully locked; open a depth-biased random set.
            UnlockArtifactNodes(ent.Comp, artifact, severity);

            var tether = _tether.TetherLink(
                ent.Owner,
                artifact,
                comp.TetherVisual,
                connectDuration: comp.TetherConnectDuration,
                disconnectDuration: comp.TetherDisconnectDuration);

            var angle = _random.NextAngle();
            var distance = _random.NextFloat(comp.MinFlingDistance, comp.MaxFlingDistance);
            _tether.TetherDrive(tether, distance, angle);

            comp.Active.Add(new BastionDefenseArtifactEntry { Artifact = artifact, Tether = tether });
        }

        comp.Phase = BastionDefenseArtifactsPhase.Holding;
        comp.NextPhaseTime = _timing.CurTime + comp.HoldTime;
    }

    /// <summary>
    /// Strips the artifact's mob-movement + pull machinery and disables its collision. The tether mover
    /// itself forces the body kinematic and drives it purely by position, so it moves ONLY via the tether
    /// (immune to its own pulse effects, gravity, etc.); disabling collision lets it phase onto the Paragon
    /// (ReelDistance 0) so it's deleted the instant it arrives. Runs right after Spawn.
    /// </summary>
    private void PrepareArtifactBody(EntityUid artifact)
    {
        RemComp<InputMoverComponent>(artifact);
        RemComp<MobMoverComponent>(artifact);
        RemComp<MobCollisionComponent>(artifact);
        RemComp<PullableComponent>(artifact);

        if (HasComp<PhysicsComponent>(artifact))
            _physics.SetCanCollide(artifact, false);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<BastionDefenseArtifactsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Phase == BastionDefenseArtifactsPhase.Holding && now >= comp.NextPhaseTime)
                PulseAndReel((uid, comp));
            else if (comp.Phase == BastionDefenseArtifactsPhase.Reeling && now >= comp.NextPhaseTime)
                ForceCleanupWave((uid, comp)); // safety: a stalled reel must never wedge the Paragon
        }
    }

    /// <summary>Break every still-tethered artifact's link (which deletes it), so a stuck reel can't leave the Paragon busy forever.</summary>
    private void ForceCleanupWave(Entity<BastionDefenseArtifactsComponent> ent)
    {
        // Copy first: BreakLink -> OnLinkBroken mutates Active as we go.
        foreach (var entry in ent.Comp.Active.ToList())
            _tether.BreakLink(entry.Tether);

        // OnLinkBroken flips the phase to Idle once Active empties; guard against any that failed to break.
        if (ent.Comp.Active.Count == 0)
            ent.Comp.Phase = BastionDefenseArtifactsPhase.Idle;
    }

    /// <summary>End of the hold: each artifact fires its effects, then gets reeled back toward the centre.</summary>
    private void PulseAndReel(Entity<BastionDefenseArtifactsComponent> ent)
    {
        foreach (var entry in ent.Comp.Active)
        {
            PulseArtifact(entry.Artifact);

            // Re-drive the same tether inward to the centre (ReelDistance 0 = onto the Paragon). Keep the
            // existing spoke angle so each returns straight along its own line.
            _tether.TetherDrive(entry.Tether, ent.Comp.ReelDistance);
        }

        ent.Comp.Phase = BastionDefenseArtifactsPhase.Reeling;
        ent.Comp.NextPhaseTime = _timing.CurTime + ent.Comp.ReelTimeout;
    }

    /// <summary>
    /// Fires a suppressed defense artifact's effects once. It's Suppressed (so the trigger/unlock machinery
    /// leaves it alone), which means TryActivateXenoArtifact would refuse - so activate its active nodes
    /// directly, then raise the artifact-level event so the activation flash still plays.
    /// </summary>
    private void PulseArtifact(EntityUid artifact)
    {
        if (!TryComp<XenoArtifactComponent>(artifact, out var xeno))
            return;

        var ent = (artifact, xeno);
        var coords = Transform(artifact).Coordinates;

        foreach (var node in _xenoArtifact.GetActiveNodes(ent))
            _xenoArtifact.ActivateNode(ent, node, user: null, target: null, coords, consumeDurability: false);

        var ev = new XenoArtifactActivatedEvent(ent, null, null, coords);
        RaiseLocalEvent(artifact, ref ev);
    }

    /// <summary>A reeling artifact reached the Paragon (or collided with it): break its link, which deletes it.</summary>
    private void OnMoveEnded(Entity<BastionDefenseArtifactsComponent> ent, ref TetherMoveEndedEvent args)
    {
        if (ent.Comp.Phase != BastionDefenseArtifactsPhase.Reeling)
            return; // the fling-out also reports Reached; only react while reeling in

        var tether = args.Tether;
        if (ent.Comp.Active.Any(e => e.Tether == tether))
            _tether.BreakLink(tether); // reeled home -> break -> OnLinkBroken deletes it
    }

    /// <summary>
    /// Any tether teardown deletes its artifact (the whole point of the wave is spawn -> fling -> pulse ->
    /// reel-and-destroy, so a wave artifact never outlives its tether) and drops its entry; empty wave -> Idle.
    /// </summary>
    private void OnLinkBroken(Entity<BastionDefenseArtifactsComponent> ent, ref TetherLinkBrokenEvent args)
    {
        var tether = args.Tether;
        var entry = ent.Comp.Active.FirstOrDefault(e => e.Tether == tether);
        if (entry is null)
            return;

        if (Exists(entry.Artifact))
            QueueDel(entry.Artifact);

        // Delete the tether visual now too, rather than letting it play out its disconnect animation: it
        // still points at the artifact we just deleted, and a lingering tether whose target is gone spams
        // "can't resolve MetaDataComponent" every PVS tick until it finally retracts.
        if (Exists(tether))
            QueueDel(tether);

        ent.Comp.Active.Remove(entry);
        if (ent.Comp.Active.Count == 0)
            ent.Comp.Phase = BastionDefenseArtifactsPhase.Idle;
    }

    /// <summary>
    /// Opens a depth-biased random set of nodes on a freshly spawned artifact. Walks the graph in
    /// increasing depth (so predecessors are decided first) and rolls each eligible node: roots open at a
    /// flat chance; deeper nodes open with a probability that climbs with both depth and Paragon severity,
    /// so identical artifacts unlock different sets that trend deeper as the Bastion's danger grows.
    /// Always leaves at least one node open so a pulse does something.
    /// </summary>
    private void UnlockArtifactNodes(BastionDefenseArtifactsComponent config, EntityUid artifact, float severity)
    {
        if (!TryComp<XenoArtifactComponent>(artifact, out var xeno))
            return;

        var ent = (artifact, xeno);
        var byDepth = _xenoArtifact.GetDepthOrderedNodes(_xenoArtifact.GetAllNodes(ent));
        if (byDepth.Count == 0)
            return;

        var anyUnlocked = false;
        foreach (var depth in byDepth.Keys.OrderBy(d => d))
        {
            foreach (var node in byDepth[depth])
            {
                // A node can only open once all its predecessors are open (roots have none).
                if (!_xenoArtifact.HasUnlockedPredecessor(ent, node))
                    continue;

                var chance = depth <= 0
                    ? config.RootUnlockChance
                    : Math.Clamp(
                        config.DepthUnlockBase + config.DepthSeverityWeight * severity * depth,
                        0f,
                        config.MaxUnlockChance);

                if (!_random.Prob(chance))
                    continue;

                _xenoArtifact.SetNodeUnlocked(ent, node);
                anyUnlocked = true;
            }
        }

        if (anyUnlocked)
            return;

        // Nothing rolled open: force a random root so the artifact isn't inert.
        if (byDepth.TryGetValue(0, out var roots) && roots.Count > 0)
            _xenoArtifact.SetNodeUnlocked(ent, _random.Pick(roots));
    }
}
