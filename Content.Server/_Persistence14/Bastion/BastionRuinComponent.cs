using Content.Shared._Persistence14.Bastion;
using Content.Shared._Persistence14.PersistentIdentifier.Reference;

namespace Content.Server._Persistence14.Bastion;

/// <summary>
/// Marks a grid as a Bastion Ruin - the ancient, self-defending site that stores a Paragon Artifact. It
/// identifies the ruin and remembers the two entities bound to it (the key and the Paragon centerpiece).
/// Both are <see cref="PersistentEntityReference"/> so the ruin's identity survives a world save/reload.
/// </summary>
[RegisterComponent, Access(typeof(BastionRuinSystem))]
public sealed partial class BastionRuinComponent : Component
{
    /// <summary>The Paragon Artifact key bound to this ruin. Persistent ref so the binding survives a reload.</summary>
    [DataField]
    public PersistentEntityReference Key;

    /// <summary>The Paragon Artifact centerpiece on the pad inside this ruin. Persistent ref so it survives a reload.</summary>
    [DataField]
    public PersistentEntityReference Paragon;

    /// <summary>Which defense this ruin's Paragon uses (also picks the Paragon sprite variant).</summary>
    [DataField]
    public BastionDefenseType DefenseType;
}
