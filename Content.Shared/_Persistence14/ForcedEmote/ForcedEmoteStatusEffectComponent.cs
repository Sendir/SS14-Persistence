using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Persistence14.ForcedEmote;

/// <summary>
/// Status effect that forces the affected mob to repeatedly perform an emote (e.g. uncontrollable
/// coughing) for as long as the effect lasts. Lives on the status effect entity, alongside
/// <see cref="Content.Shared.StatusEffectNew.Components.StatusEffectComponent"/> - not on the mob.
/// Duration and cleanup are handled centrally by the status effect system; this component only
/// describes the repeat, and <see cref="Content.Server._Persistence14.ForcedEmote.ForcedEmoteStatusEffectSystem"/> drives it.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ForcedEmoteStatusEffectComponent : Component
{
    /// <summary>
    /// The emote attempted each interval.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<EmotePrototype> Emote;

    /// <summary>
    /// How often an attempt at the emote is made.
    /// </summary>
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(0.2);

    /// <summary>
    /// Probability of actually performing the emote on each attempt.
    /// </summary>
    [DataField]
    public float Chance = 0.3f;

    /// <summary>
    /// When the next emote attempt happens. Managed by the system.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextEmoteTime;
}
