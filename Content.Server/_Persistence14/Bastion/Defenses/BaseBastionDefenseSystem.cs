namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>
/// Base for a Bastion Ruin defense. Each defense is a marker/config component on the Paragon Artifact
/// plus a subclass here that reacts to <see cref="BastionDefensePulseEvent"/> - the same "component +
/// event + base system" shape as the artifact effects (BaseXAESystem). Adding a new defense later is:
/// a new component, a new subclass, and adding the component to a Paragon variant.
///
/// The framework is wired but currently dormant: nothing raises the pulse yet (that arrives with the
/// controller/timer + dormancy work), and the per-defense <see cref="OnPulse"/> handlers are empty.
/// </summary>
public abstract class BaseBastionDefenseSystem<T> : EntitySystem where T : BastionDefenseComponent
{
    public override void Initialize()
    {
        SubscribeLocalEvent<T, BastionDefensePulseEvent>(OnPulse);
    }

    /// <summary>Runs the defense for one pulse. Empty for now.</summary>
    protected abstract void OnPulse(Entity<T> ent, ref BastionDefensePulseEvent args);
}

/// <summary>
/// Raised on a Paragon Artifact when its defense should act once. Not raised yet - the controller that
/// pulses it (on a timer, only while players are near) comes later. <see cref="Severity"/> is intended
/// to scale each defense's intensity with how much of the Paragon's graph has been unlocked.
/// </summary>
[ByRefEvent]
public readonly record struct BastionDefensePulseEvent(EntityUid Paragon, float Severity);
