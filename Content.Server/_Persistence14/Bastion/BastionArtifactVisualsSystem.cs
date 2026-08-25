using Content.Shared._Persistence14.Bastion;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.XenoArtifacts;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Persistence14.Bastion;

/// <summary>
/// Server half of <see cref="BastionArtifactVisualsComponent"/>: sets the artifact's appearance data on
/// the unlock/activation events so the client visualizer can toggle its effect layers. Mirrors what the
/// stock <c>RandomArtifactSpriteSystem</c> does, but for the Paragon (which has no RandomArtifactSprite).
/// </summary>
public sealed class BastionArtifactVisualsSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BastionArtifactVisualsComponent, ArtifactUnlockingStartedEvent>(OnUnlockingStarted);
        SubscribeLocalEvent<BastionArtifactVisualsComponent, ArtifactUnlockingFinishedEvent>(OnUnlockingFinished);
        SubscribeLocalEvent<BastionArtifactVisualsComponent, XenoArtifactActivatedEvent>(OnActivated);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<BastionArtifactVisualsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ActivationStart is not { } start)
                continue;

            if (now - start < comp.ActivationTime)
                continue;

            _appearance.SetData(uid, SharedArtifactsVisuals.IsActivated, false);
            comp.ActivationStart = null;
        }
    }

    private void OnUnlockingStarted(Entity<BastionArtifactVisualsComponent> ent, ref ArtifactUnlockingStartedEvent args)
    {
        _appearance.SetData(ent, SharedArtifactsVisuals.IsUnlocking, true);
    }

    private void OnUnlockingFinished(Entity<BastionArtifactVisualsComponent> ent, ref ArtifactUnlockingFinishedEvent args)
    {
        _appearance.SetData(ent, SharedArtifactsVisuals.IsUnlocking, false);
    }

    private void OnActivated(Entity<BastionArtifactVisualsComponent> ent, ref XenoArtifactActivatedEvent args)
    {
        _appearance.SetData(ent, SharedArtifactsVisuals.IsActivated, true);
        ent.Comp.ActivationStart = _timing.CurTime;
    }
}
