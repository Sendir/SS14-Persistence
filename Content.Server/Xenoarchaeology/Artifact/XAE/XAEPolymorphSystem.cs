using Content.Server.Polymorph.Systems;
using Content.Server.Xenoarchaeology.Artifact.XAE.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Xenoarchaeology.Artifact.XAE;

/// <summary>
/// System for xeno artifact activation effect that is polymorphing all humanoid entities in range.
/// </summary>
public sealed class XAEPolymorphSystem : BaseXAESystem<XAEPolymorphComponent>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly PolymorphSystem _poly = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    /// <summary> Pre-allocated and re-used collection.</summary>
    private readonly HashSet<Entity<HumanoidProfileComponent>> _humanoids = new();

    /// <inheritdoc />
    protected override void OnActivated(Entity<XAEPolymorphComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        // Scale the polymorph duration by Scale/2 (a copy of the prototype's config, so no new prototype
        // is needed). Range scales fully.
        var config = _proto.Index(ent.Comp.PolymorphPrototypeName).Configuration;
        if (config.Duration is { } duration)
            config = config with { Duration = Math.Max(1, (int) (duration * args.Scale / 2f)) };

        _humanoids.Clear();
        _lookup.GetEntitiesInRange(args.Coordinates, ent.Comp.Range * args.Scale, _humanoids);
        foreach (var comp in _humanoids)
        {
            var target = comp.Owner;
            if (!_mob.IsAlive(target))
                continue;

            _poly.PolymorphEntity(target, config);
            _audio.PlayPvs(ent.Comp.PolySound, ent);
        }
    }
}
