namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>
/// Defense B marker: this Paragon defends by spawning anomalies tethered to it, ramping toward crit.
/// Config and behaviour are not implemented yet - framework only.
/// </summary>
[RegisterComponent, Access(typeof(BastionDefenseAnomaliesSystem))]
public sealed partial class BastionDefenseAnomaliesComponent : BastionDefenseComponent
{
}
