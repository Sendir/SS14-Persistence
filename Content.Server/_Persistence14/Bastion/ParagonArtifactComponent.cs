namespace Content.Server._Persistence14.Bastion;

/// <summary>
/// The centerpiece Paragon Artifact inside a Bastion Ruin. It starts with its node graph suppressed
/// (locked); inserting the matching Paragon Artifact key into its <see cref="KeySlotId"/> slot
/// permanently unlocks it (see <see cref="ParagonArtifactSystem"/>). The key is consumed on insert.
/// </summary>
[RegisterComponent, Access(typeof(ParagonArtifactSystem))]
public sealed partial class ParagonArtifactComponent : Component
{
    /// <summary>The item-slot the key goes into. Must match the slot id declared on the prototype's ItemSlots.</summary>
    [DataField]
    public string KeySlotId = "paragon_key";

    /// <summary>Whether the key has been inserted and the graph unlocked. One-way: once true, it stays unlocked.</summary>
    [DataField]
    public bool KeyInserted;
}
