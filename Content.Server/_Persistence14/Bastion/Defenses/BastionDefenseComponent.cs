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
}
