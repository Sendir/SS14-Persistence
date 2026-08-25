namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>Defense B. Empty for now - see <see cref="BaseBastionDefenseSystem{T}"/>.</summary>
public sealed class BastionDefenseAnomaliesSystem : BaseBastionDefenseSystem<BastionDefenseAnomaliesComponent>
{
    protected override void OnPulse(Entity<BastionDefenseAnomaliesComponent> ent, ref BastionDefensePulseEvent args)
    {
        // TODO: tether out, grow in an anomaly, ramp severity toward crit; drag-out forces crit.
    }
}
