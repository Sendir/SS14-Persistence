using Content.Server.Xenoarchaeology.Artifact.XAE;
using Robust.Shared.Prototypes;

namespace Content.Server.Xenoarchaeology.Artifact.XAE.Components;

/// <summary>
/// Xeno artifact effect: forces every alive mob within <see cref="Radius"/> into a repeated emote
/// "fit" (e.g. uncontrollable laughter) for <see cref="Duration"/>. Line of sight is not required.
/// The actual repeat and its cleanup are handled by the applied forced-emote status effect (see the
/// effect entity's ForcedEmoteStatusEffect component); this effect just applies it and (re)sets its
/// duration on each activation.
/// </summary>
[RegisterComponent, Access(typeof(XAEForcedEmoteSystem))]
public sealed partial class XAEForcedEmoteComponent : Component
{
    /// <summary>
    /// The forced-emote status effect applied to affected mobs. It defines which emote is performed
    /// and its cadence. Must be an entity prototype carrying a StatusEffect + ForcedEmoteStatusEffect.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId StatusEffect;

    /// <summary>
    /// Radius (in tiles) around the artifact within which mobs are affected. Line of sight is ignored.
    /// </summary>
    [DataField]
    public float Radius = 5f;

    /// <summary>
    /// How long the fit lasts. Re-activating refreshes the timer back to this value.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);
}
