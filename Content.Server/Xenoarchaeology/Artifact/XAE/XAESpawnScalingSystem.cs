using System.Numerics;
using Content.Server.Spawners.Components;
using Content.Shared.EntityTable;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Artifact.XAE.Components;
using Robust.Shared.Random;

namespace Content.Server.Xenoarchaeology.Artifact.XAE;

/// <summary>
/// Scales spawn effects with the artifact's EffectScale. XAEApplyComponents (shared) adds an
/// EntityTableSpawner to the artifact which spawns once per activation; this server-side system runs on
/// the same activation and spawns that table (Scale - 1) additional times, so a "spawn 10 steel" node
/// on a x10 Paragon yields ~100. Only touches spawner components; everything else XAEApplyComponents
/// applies is untouched. Lives server-side because EntityTableSpawnerComponent is server-only.
///
/// It subscribes on <see cref="XenoArtifactNodeComponent"/> (present on every effect node) rather than
/// on XAEApplyComponentsComponent, because Robust allows only one subscription per (component, event)
/// pair and BaseXAESystem&lt;XAEApplyComponentsComponent&gt; already owns that one. We just read the
/// apply-components data off the same node.
/// </summary>
public sealed class XAESpawnScalingSystem : EntitySystem
{
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenoArtifactNodeComponent, XenoArtifactNodeActivatedEvent>(OnActivated);
    }

    private void OnActivated(Entity<XenoArtifactNodeComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        // Only effect nodes that apply a spawner scale; everything else ignores this.
        if (!TryComp<XAEApplyComponentsComponent>(ent, out var apply))
            return;

        // XAEApplyComponents already produced one spawn; add the remaining (Scale - 1).
        var extra = (int) MathF.Round(args.Scale) - 1;
        if (extra <= 0)
            return;

        var coords = _transform.GetMoverCoordinates(args.Artifact.Owner);

        foreach (var registry in apply.Components)
        {
            if (registry.Value.Component is not EntityTableSpawnerComponent spawner)
                continue;

            for (var i = 0; i < extra; i++)
            {
                foreach (var proto in _entityTable.GetSpawns(spawner.Table))
                {
                    var off = spawner.Offset;
                    var at = coords.Offset(new Vector2(_random.NextFloat(-off, off), _random.NextFloat(-off, off)));
                    if (spawner.SpawnDetached)
                        SpawnAtPosition(proto, at);
                    else
                        SpawnAttachedTo(proto, at);
                }
            }
        }
    }
}
