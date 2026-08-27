using Content.Shared.EntityTable;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>
/// Defense C: this Paragon defends by summoning waves of hostile guardian mobs (one team - the
/// ParagonBastion faction) around itself. The wave tier scales with how much of the graph is unlocked
/// (severity): weak → medium → deadly pools. A live-mob cap keeps a long-lived ruin from flooding.
///
/// It also runs the space-rescue tether: every Bastion mob (wave OR Paragon-node spawned) that drifts off
/// the ruin platform or past the leash radius is reeled home on a tether, then released intact to keep
/// fighting. See <see cref="BastionDefenseMobsSystem"/>.
/// </summary>
[RegisterComponent, Access(typeof(BastionDefenseMobsSystem))]
public sealed partial class BastionDefenseMobsComponent : BastionDefenseComponent
{
    // --- Summon pools (tiered), selected by severity ---

    [DataField]
    public ProtoId<EntityTablePrototype> WeakPool = "BastionMobPoolWeak";

    [DataField]
    public ProtoId<EntityTablePrototype> MediumPool = "BastionMobPoolMedium";

    [DataField]
    public ProtoId<EntityTablePrototype> DeadlyPool = "BastionMobPoolDeadly";

    /// <summary>Severity at/above which the wave draws from the medium pool.</summary>
    [DataField]
    public float MediumSeverity = 0.34f;

    /// <summary>Severity at/above which the wave draws from the deadly pool.</summary>
    [DataField]
    public float DeadlySeverity = 0.67f;

    // --- Wave size ---

    /// <summary>Mobs summoned at minimum severity (Paragon fully locked).</summary>
    [DataField]
    public int MinCount = 1;

    /// <summary>Mobs summoned at maximum severity (Paragon fully unlocked).</summary>
    [DataField]
    public int MaxCount = 5;

    /// <summary>Cap on simultaneously-alive wave mobs, so a 10-minute ruin can't flood. Node-spawned mobs don't count.</summary>
    [DataField]
    public int MaxAlive = 12;

    /// <summary>Random offset radius around the Paragon centre that wave mobs spawn within (tiles).</summary>
    [DataField]
    public float SpawnRadius = 4f;

    /// <summary>Live wave mobs, pruned as they die or despawn.</summary>
    [DataField]
    public List<EntityUid> Active = new();

    // --- Space-rescue tether (applies to ALL Bastion mobs near the Paragon) ---

    /// <summary>A mob farther than this from the Paragon (or off its grid) is reeled home.</summary>
    [DataField]
    public float LeashRadius = 9f;

    /// <summary>How far out from the Paragon we scan for stray guardians.</summary>
    [DataField]
    public float FetchScanRadius = 60f;

    /// <summary>
    /// The tether reels a stray toward this distance from the Paragon, and releases it once it's within it.
    /// Keep it outside the Paragon's 2x2 fixture (~2 radius) so a collidable mob can actually reach it -
    /// otherwise it can never arrive and hangs tethered forever.
    /// </summary>
    [DataField]
    public float ReelDistance = 3.5f;

    /// <summary>Visual tether prototype used for the rescue.</summary>
    [DataField]
    public string TetherVisual = "EyeTetherVisual";

    /// <summary>How often the stray scan runs (it needn't be every tick).</summary>
    [DataField]
    public TimeSpan FetchScanInterval = TimeSpan.FromSeconds(1);

    /// <summary>Next scheduled stray scan.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextFetchScan;
}
