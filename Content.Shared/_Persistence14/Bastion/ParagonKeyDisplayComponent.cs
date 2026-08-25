using Robust.Shared.GameStates;

namespace Content.Shared._Persistence14.Bastion;

/// <summary>
/// Networked pointer from a Paragon Artifact to the key that unlocks it, purely so the console's
/// locked screen can render that key's live in-game sprite (tinted into a silhouette) rather than a
/// baked image. Set when the ruin binds its key. The key still exists while the Paragon is locked
/// (it's in the player's hands / the world); once slotted it's consumed and the locked screen hides.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ParagonKeyDisplayComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? Key;
}
