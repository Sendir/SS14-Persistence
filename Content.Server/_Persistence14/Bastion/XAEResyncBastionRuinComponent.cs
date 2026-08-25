namespace Content.Server._Persistence14.Bastion;

/// <summary>
/// Xeno artifact effect: "resyncs the Bastion Ruin to this dimension". On activation it spawns a
/// whole Bastion Ruin somewhere in open space and turns the hand-held artifact it lives on into that
/// ruin's Paragon Artifact key (see <see cref="Content.Shared._Persistence14.Bastion.ParagonArtifactKeyComponent"/>).
/// If no free spot can be found, the node shatters instead, signalling the failure. Only ever spawns
/// one ruin per artifact.
/// </summary>
[RegisterComponent, Access(typeof(XAEResyncBastionRuinSystem))]
public sealed partial class XAEResyncBastionRuinComponent : Component
{
}
