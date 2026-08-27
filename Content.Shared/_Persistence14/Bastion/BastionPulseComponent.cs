using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Persistence14.Bastion;

/// <summary>
/// The Paragon Artifact's defense pulse clock. It counts down while a player is within
/// <see cref="ActivationRange"/> (otherwise it lies dormant), and fires a defense pulse when the timer
/// runs out - or earlier, if the timer is sitting in a band that allows an early discharge. The
/// console reads <see cref="CurrentDescriptor"/> to show a qualitative "how close is the next pulse"
/// readout. Everything is data-driven via <see cref="Bands"/>.
///
/// The pulse itself is wired (it raises the defense event) but the defenses are still empty, so a pulse
/// currently does nothing visible.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BastionPulseComponent : Component
{
    /// <summary>Base time from one pulse to the next. This is also the guaranteed-fire timeout.</summary>
    [DataField]
    public TimeSpan PulseInterval = TimeSpan.FromSeconds(200);

    /// <summary>
    /// If both <see cref="PulseIntervalMin"/> and <see cref="PulseIntervalMax"/> are set (and max &gt; min),
    /// each cycle rolls a random interval in [min, max] instead of using the fixed <see cref="PulseInterval"/>.
    /// Leave unset to keep the fixed interval. Used to give a jittered cadence (e.g. a 10-30s test pulse).
    /// </summary>
    [DataField]
    public TimeSpan? PulseIntervalMin;

    /// <summary>Upper bound of the random pulse interval. See <see cref="PulseIntervalMin"/>.</summary>
    [DataField]
    public TimeSpan? PulseIntervalMax;

    /// <summary>
    /// Readout bands, matched by seconds-until-next-pulse. The band whose [From, To) range contains the
    /// current remaining time supplies the console descriptor. A band with EarlyPulseChance &gt; 0 can
    /// also discharge the pulse early while the timer sits in it - the pulse always fires on timeout.
    /// Order doesn't matter; the first band that contains the remaining time is used.
    /// </summary>
    [DataField]
    public List<BastionPulseBand> Bands = new();

    /// <summary>Players must be within this many tiles for the Paragon to charge and pulse; else it lies dormant.</summary>
    [DataField]
    public float ActivationRange = 100f;

    /// <summary>Console text shown while dormant (no players in range).</summary>
    [DataField]
    public string DormantDescriptor = "bastion-pulse-dormant";

    /// <summary>Console text shown once the Paragon is fully unlocked and the defense has shut off.</summary>
    [DataField]
    public string CompletedDescriptor = "bastion-pulse-complete";

    /// <summary>When the next pulse is due. Server runtime.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPulse;

    /// <summary>Current console descriptor loc-id (a band's, or the dormant one). Networked for the console UI.</summary>
    [DataField, AutoNetworkedField]
    public string? CurrentDescriptor;

    /// <summary>Whether the Paragon is currently dormant (no players in range). Networked.</summary>
    [DataField, AutoNetworkedField]
    public bool Dormant = true;
}

/// <summary>One console-readout band: a range of "seconds until next pulse" and its descriptor.</summary>
[DataDefinition]
public sealed partial class BastionPulseBand
{
    /// <summary>Console text (loc-id) while the timer sits in this band.</summary>
    [DataField(required: true)]
    public string Descriptor = string.Empty;

    /// <summary>Lower bound (inclusive) of seconds-until-pulse for this band.</summary>
    [DataField]
    public float From;

    /// <summary>Upper bound (exclusive) of seconds-until-pulse for this band.</summary>
    [DataField]
    public float To = float.MaxValue;

    /// <summary>Per-second chance to discharge the pulse early while the timer is in this band. 0 = never early.</summary>
    [DataField]
    public float EarlyPulseChance;
}
