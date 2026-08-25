using Robust.Shared.GameStates;

namespace Content.Shared._Persistence14.Bastion;

/// <summary>
/// Marks a hand-held artifact as the key to a Paragon Artifact - the centerpiece artifact sealed
/// inside a Bastion Ruin. This artifact resynced (spawned) the ruin and is bound to it; it is both
/// the key that will later unlock the Paragon's node graph and a locator for finding the ruin. One
/// key is bound to one ruin; re-activating the resync effect on an already-bound key does nothing.
///
/// This is the minimal slice: it records the bound ruin. Locator behaviour, the slot/unlock gate on
/// the Paragon, and save-stable references come later.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ParagonArtifactKeyComponent : Component
{
    /// <summary>The Bastion Ruin grid this key spawned and is bound to.</summary>
    [DataField]
    public EntityUid? BastionRuin;
}
