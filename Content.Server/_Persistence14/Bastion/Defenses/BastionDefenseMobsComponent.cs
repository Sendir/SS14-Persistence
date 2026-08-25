namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>
/// Defense C marker: this Paragon defends by spawning waves of hostile mobs. Config and behaviour are
/// not implemented yet - framework only.
/// </summary>
[RegisterComponent, Access(typeof(BastionDefenseMobsSystem))]
public sealed partial class BastionDefenseMobsComponent : BastionDefenseComponent
{
}
