namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>
/// Base for a Bastion Ruin defense marker/config component. Each concrete defense (artifacts, anomalies,
/// mobs) is its own component that inherits this, paired with its own <see cref="BaseBastionDefenseSystem{T}"/>
/// subclass. Only the concrete components are <c>[RegisterComponent]</c>; this base carries config common
/// to every defense and lets the base system constrain its generic to "a defense".
/// </summary>
public abstract partial class BastionDefenseComponent : Component
{
    /// <summary>
    /// Relative weight for when a Paragon carries more than one defense and one is chosen per pulse.
    /// Unused while each Paragon variant has a single defense, but reserved so multi-defense Paragons
    /// can weight their selection later.
    /// </summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>
    /// Things summoned per pulse at minimum severity (Paragon fully locked). The actual count is lerped
    /// between this and <see cref="MaxCount"/> by severity - see <c>BaseBastionDefenseSystem.GetWaveCount</c>.
    /// The per-defense values live on the prototypes so they can be balanced without code changes.
    /// </summary>
    [DataField]
    public int MinCount = 1;

    /// <summary>Things summoned per pulse at maximum severity (Paragon fully unlocked). See <see cref="MinCount"/>.</summary>
    [DataField]
    public int MaxCount = 1;
}
