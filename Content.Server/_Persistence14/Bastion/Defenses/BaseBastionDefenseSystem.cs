namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>
/// Base for a Bastion Ruin defense. Each defense is a marker/config component on the Paragon Artifact plus
/// a subclass here that reacts to <see cref="BastionDefensePulseEvent"/> - the same "component + event + base
/// system" shape as the artifact effects (BaseXAESystem). A new defense is a new component, a new subclass,
/// and adding the component to a Paragon variant; the three shipped defenses are Artifacts (A), Anomalies
/// (B), and Mobs (C).
///
/// The pulse itself is raised by <c>BastionPulseSystem</c> on a random interval while a player is near, with
/// a severity scaled to how much of the Paragon's graph is unlocked.
/// </summary>
public abstract class BaseBastionDefenseSystem<T> : EntitySystem where T : BastionDefenseComponent
{
    public override void Initialize()
    {
        SubscribeLocalEvent<T, BastionDefensePulseEvent>(OnPulse);
    }

    /// <summary>Runs the defense for one pulse, at the given <see cref="BastionDefensePulseEvent.Severity"/>.</summary>
    protected abstract void OnPulse(Entity<T> ent, ref BastionDefensePulseEvent args);
}

/// <summary>
/// Raised on a Paragon Artifact by <c>BastionPulseSystem</c> when its defense should act once.
/// <see cref="Severity"/> is the fraction of the Paragon's graph unlocked (0..1), which each defense uses to
/// scale its intensity (wave size, ramp rate, tier, etc.).
/// </summary>
[ByRefEvent]
public readonly record struct BastionDefensePulseEvent(EntityUid Paragon, float Severity);
