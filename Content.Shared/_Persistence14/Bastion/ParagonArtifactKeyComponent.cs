using Content.Shared._Persistence14.PersistentIdentifier.Reference;
using Robust.Shared.GameStates;

namespace Content.Shared._Persistence14.Bastion;

/// <summary>
/// Marks a hand-held artifact as the key to a Paragon Artifact - the centerpiece artifact sealed inside a
/// Bastion Ruin. This artifact resynced (spawned) the ruin and is bound to it; it is both the key that
/// unlocks the Paragon's node graph when slotted (see <c>ParagonArtifactSystem</c>) and a locator for
/// finding the ruin. One key is bound to one ruin; re-activating the resync effect on an already-bound key
/// does nothing.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ParagonArtifactKeyComponent : Component
{
    /// <summary>
    /// The Bastion Ruin grid this key spawned and is bound to. <see cref="PersistentEntityReference"/> so the
    /// binding survives a world save/reload rather than pointing at a reassigned UID.
    /// </summary>
    [DataField]
    public PersistentEntityReference BastionRuin;
}
