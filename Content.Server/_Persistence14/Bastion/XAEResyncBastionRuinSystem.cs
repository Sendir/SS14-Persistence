using Content.Shared._Persistence14.Bastion;
using Content.Shared.Popups;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;
using Robust.Shared.Random;

namespace Content.Server._Persistence14.Bastion;

/// <summary>
/// Handles the "resync Bastion Ruin" artifact effect: spawn the ruin in open space and bind this
/// artifact to it as the Paragon Artifact key. Idempotent - once the artifact is a key, re-activating
/// does nothing. Shatters the node if no free spot in space can be found.
/// </summary>
public sealed class XAEResyncBastionRuinSystem : BaseXAESystem<XAEResyncBastionRuinComponent>
{
    [Dependency] private readonly BastionRuinSystem _bastionRuin = default!;
    [Dependency] private readonly SharedXenoArtifactSystem _xenoArtifact = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly BastionDefenseType[] Defenses =
        { BastionDefenseType.Artifacts, BastionDefenseType.Anomalies, BastionDefenseType.Mobs };

    protected override void OnActivated(Entity<XAEResyncBastionRuinComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        var artifact = args.Artifact.Owner;

        // One ruin per artifact.
        if (HasComp<ParagonArtifactKeyComponent>(artifact))
            return;

        var mapId = _transform.GetMapCoordinates(artifact).MapId;

        if (_bastionRuin.TrySpawnRuinForKey(artifact, mapId, _random.Pick(Defenses), out _))
        {
            _popup.PopupEntity(Loc.GetString("bastion-resync-success"), artifact, args.User ?? artifact, PopupType.Large);
            return;
        }

        // Couldn't find open space - shatter the node so the player knows the resync failed.
        _xenoArtifact.Shatter(args.Node.AsNullable());
        _popup.PopupEntity(Loc.GetString("bastion-resync-failed"), artifact, args.User ?? artifact, PopupType.MediumCaution);
    }
}
