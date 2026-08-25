namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>Defense C. Empty for now - see <see cref="BaseBastionDefenseSystem{T}"/>.</summary>
public sealed class BastionDefenseMobsSystem : BaseBastionDefenseSystem<BastionDefenseMobsComponent>
{
    protected override void OnPulse(Entity<BastionDefenseMobsComponent> ent, ref BastionDefensePulseEvent args)
    {
        // TODO: spawn a wave of hostile mobs from the summon pool; space-rescue tether stragglers.
    }
}
