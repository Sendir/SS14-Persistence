using Content.Shared._Persistence14.Bastion;

namespace Content.Server._Persistence14.Bastion;

/// <summary>
/// Marks a grid as a Bastion Ruin - the ancient, self-defending site that stores a Paragon Artifact.
/// For now it just identifies the ruin and remembers the key bound to it; the analyzer, defenses, and
/// lifecycle timers are added later.
/// </summary>
[RegisterComponent, Access(typeof(BastionRuinSystem))]
public sealed partial class BastionRuinComponent : Component
{
    /// <summary>The Paragon Artifact key bound to this ruin.</summary>
    [DataField]
    public EntityUid? Key;

    /// <summary>The Paragon Artifact centerpiece sitting on the pad inside this ruin.</summary>
    [DataField]
    public EntityUid? Paragon;

    /// <summary>Which defense this ruin's Paragon uses (also picks the Paragon sprite variant).</summary>
    [DataField]
    public BastionDefenseType DefenseType;
}
