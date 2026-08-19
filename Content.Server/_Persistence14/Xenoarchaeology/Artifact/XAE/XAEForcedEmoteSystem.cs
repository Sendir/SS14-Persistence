using Content.Server.Xenoarchaeology.Artifact.XAE.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;

namespace Content.Server.Xenoarchaeology.Artifact.XAE;

/// <summary>
/// System for a xeno artifact effect that forces nearby mobs into a repeated emote "fit"
/// (e.g. uncontrollable laughter) for a fixed duration. Line of sight is ignored. The repeat itself
/// and its expiry are driven by a forced-emote status effect; this system only applies that status
/// effect to living mobs in range and (re)sets its duration.
/// </summary>
public sealed class XAEForcedEmoteSystem : BaseXAESystem<XAEForcedEmoteComponent>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    /// <summary> Pre-allocated and re-used collection. </summary>
    private readonly HashSet<EntityUid> _entities = new();

    /// <inheritdoc/>
    protected override void OnActivated(Entity<XAEForcedEmoteComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        var comp = ent.Comp;

        _entities.Clear();
        _lookup.GetEntitiesInRange(args.Coordinates, comp.Radius, _entities);
        foreach (var mob in _entities)
        {
            // Only living mobs are affected.
            if (!_mobState.IsAlive(mob))
                continue;

            // Set the duration so re-triggering the same emote just refreshes its timer back
            // to full, while different forced emotes coexist as their own status effects. The status
            // effect system times it out and cleans it up
            _statusEffects.TrySetStatusEffectDuration(mob, comp.StatusEffect, comp.Duration);
        }
    }
}
