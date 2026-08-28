using Content.Shared._Persistence14.PersistentIdentifier.Reference;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>
/// Marks a single anomaly summoned by Defense B and carries its per-anomaly state: it grows in on arrival,
/// ramps its severity toward supercritical, and is force-critted if it's knocked off the leash. Driven
/// entirely by <see cref="BastionDefenseAnomaliesSystem"/>, and removed when the anomaly dies (its own crit)
/// or, for a trap, once the delivery tether detaches.
///
/// The two entity links are <see cref="PersistentEntityReference"/> and the two absolute timestamps use
/// <see cref="TimeOffsetSerializer"/>, so a mid-wave world save/reload restores the anomaly's state cleanly
/// (raw UIDs get reassigned each session, and raw timestamps would jump relative to the paused clock).
/// </summary>
[RegisterComponent, Access(typeof(BastionDefenseAnomaliesSystem))]
public sealed partial class BastionAnomalyComponent : Component
{
    /// <summary>
    /// The Paragon that summoned this anomaly (the tether source, and what its leash is measured from).
    /// <see cref="PersistentEntityReference"/> rather than a raw UID so the link survives a save/reload.
    /// </summary>
    [DataField]
    public PersistentEntityReference Paragon;

    /// <summary>
    /// The leash tether binding this anomaly to the Paragon; an <c>OutOfRange</c> break forces an immediate
    /// crit. <see cref="PersistentEntityReference"/> so a save/reload can't alias it to the wrong entity - it
    /// simply resolves to nothing if the (transient) tether visual is gone.
    /// </summary>
    [DataField]
    public PersistentEntityReference Tether;

    /// <summary>Severity added per second once it's ramping (set from Paragon severity at spawn).</summary>
    [DataField]
    public float RampPerSecond;

    /// <summary>Current lifecycle phase. Set when the anomaly is spawned (on tether arrival).</summary>
    [DataField]
    public BastionAnomalyPhase Phase;

    /// <summary>End of the current timed phase: the grow-in deadline, or the trap's hold-then-detach deadline.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan PhaseEndsAt;

    /// <summary>When the grow-in started, for the scale lerp.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan GrowStartedAt;

    /// <summary>
    /// How long the grow-in takes (copied from the Paragon's config so Update needn't re-read it). A relative
    /// duration, so it needs no time-offset serializer.
    /// </summary>
    [DataField]
    public TimeSpan GrowDuration;

    /// <summary>Sprite scale it starts at while growing in (grows to 1).</summary>
    [DataField]
    public float StartScale = 0.2f;
}

/// <summary>Lifecycle phase of a Defense B anomaly (it's spawned only once the tether has arrived).</summary>
public enum BastionAnomalyPhase : byte
{
    /// <summary>Growing in from <see cref="BastionAnomalyComponent.StartScale"/> to full size.</summary>
    Growing,

    /// <summary>Full size; actively ramping severity toward supercritical.</summary>
    Ramping,

    /// <summary>
    /// This is an anomaly TRAP (an injector, no standalone anomaly): the tether just holds a beat, then
    /// detaches and the marker is dropped, leaving the trap in place as a lurking hazard.
    /// </summary>
    TrapDelivering,
}
