using Content.Shared._Persistence14.PersistentIdentifier.Reference;
using Robust.Shared.GameStates;

namespace Content.Shared._Persistence14.Bastion;

/// <summary>
/// Networked pointer from a Paragon Artifact to the key that unlocks it, purely so the console's locked
/// screen can render that key's live in-game sprite (tinted into a silhouette) rather than a baked image.
/// Set when the ruin binds its key. The key still exists while the Paragon is locked (it's in the player's
/// hands / the world); once slotted it's consumed and the locked screen hides.
///
/// A <see cref="PersistentEntityReference"/> (networked - it serializes its stable id string) rather than a
/// <c>NetEntity</c>, so the pointer survives a world save/reload; both server and client resolve it through
/// the persistent-id registry.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ParagonKeyDisplayComponent : Component
{
    /// <summary>The key whose sprite the locked screen renders. Resolved via the persistent-id registry.</summary>
    [DataField, AutoNetworkedField]
    public PersistentEntityReference Key;
}
