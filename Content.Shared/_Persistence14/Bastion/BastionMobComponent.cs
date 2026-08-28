using Content.Shared._Persistence14.PersistentIdentifier.Reference;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Persistence14.Bastion;

/// <summary>
/// Tags a mob as a Bastion guardian - spawned either by a Mobs-defense Paragon's nodes or by its defense
/// wave. Every mob prototype in the Bastion summon pool carries this (alongside the ParagonBastion faction).
///
/// It is the hook for the space-rescue tether: the Mobs defense system scans tagged mobs and, if one drifts
/// off the ruin platform or past the leash radius, reels it home on a tether (see BastionDefenseMobsSystem).
/// <see cref="FetchTether"/> holds the active rescue tether so a mob isn't re-fetched while one is running.
/// Server-only logic reads this; it lives in Shared purely so it can be placed on shared mob prototypes.
///
/// The rescue link is a <see cref="PersistentEntityReference"/> and the cooldown a
/// <see cref="TimeOffsetSerializer"/> timestamp, so a mid-rescue save/reload restores cleanly rather than
/// leaving a mob bound to a stale UID or a mis-shifted deadline.
/// </summary>
[RegisterComponent]
public sealed partial class BastionMobComponent : Component
{
    /// <summary>
    /// The rescue tether currently reeling this mob home, if any. Cleared when the tether ends.
    /// <see cref="PersistentEntityReference"/> so it survives a save/reload (and resolves to nothing if the
    /// transient tether is gone) rather than aliasing a reassigned UID.
    /// </summary>
    [DataField]
    public PersistentEntityReference FetchTether;

    /// <summary>
    /// Absolute time until which the mob is not re-fetched. Set briefly after a rescue completes so it isn't
    /// re-flagged as stray during the one-tick window before grid-traversal re-parents it onto the floor.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? FetchCooldownUntil;
}
