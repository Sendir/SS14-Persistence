using Content.Shared._Persistence14.PersistentIdentifier.Reference;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>
/// Defense A: this Paragon defends by spawning partially-unlocked artifacts (x3 scaled) at its centre,
/// flinging them outward on tethers, pulsing them, then reeling them back in. Holds both the tuning
/// config and the live per-wave state machine (see <see cref="BastionDefenseArtifactsSystem"/>).
/// </summary>
[RegisterComponent, Access(typeof(BastionDefenseArtifactsSystem))]
public sealed partial class BastionDefenseArtifactsComponent : BastionDefenseComponent
{
    // --- Config ---

    /// <summary>The defensive artifact to spawn. A structure (non-handheld) artifact so it can't roll resync.</summary>
    [DataField]
    public EntProtoId ArtifactProto = "StructureBastionDefenseArtifact";

    // MinCount/MaxCount are on the base BastionDefenseComponent.

    /// <summary>
    /// Random fling distance range (tiles from the Paragon centre). Kept inside the ruin platform
    /// (~6-tile radius) so the artifacts land on the grid's simulated atmosphere - a gas/heat effect
    /// flung into bare space has no atmosphere to disperse into and just sits on one tile forever.
    /// </summary>
    [DataField]
    public float MinFlingDistance = 3f;

    [DataField]
    public float MaxFlingDistance = 5f;

    /// <summary>Distance from centre the artifacts are reeled back to. 0 = onto the Paragon, then deleted on arrival.</summary>
    [DataField]
    public float ReelDistance;

    /// <summary>How long the artifacts hang flung-out before they pulse and get reeled back in.</summary>
    [DataField]
    public TimeSpan HoldTime = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Safety cap on the reel: if an artifact hasn't arrived within this long, it's cleaned up anyway so a
    /// wave always completes and the Paragon frees up to pulse again.
    /// </summary>
    [DataField]
    public TimeSpan ReelTimeout = TimeSpan.FromSeconds(8);

    /// <summary>Visual tether prototype linking each artifact to the Paragon.</summary>
    [DataField]
    public string TetherVisual = "EyeTetherVisual";

    [DataField]
    public TimeSpan TetherConnectDuration = TimeSpan.FromSeconds(0.4);

    [DataField]
    public TimeSpan TetherDisconnectDuration = TimeSpan.FromSeconds(0.4);

    // --- Unlock roll (depth-biased, random per artifact) ---

    /// <summary>Unlock chance for a root (depth 0) node - roots open readily so the graph always has a way in.</summary>
    [DataField]
    public float RootUnlockChance = 0.7f;

    /// <summary>Base unlock chance for a non-root node before the severity/depth term.</summary>
    [DataField]
    public float DepthUnlockBase = 0.15f;

    /// <summary>How strongly severity and depth push the unlock chance up: P = base + weight * severity * depth.</summary>
    [DataField]
    public float DepthSeverityWeight = 0.5f;

    /// <summary>Ceiling on any single node's unlock chance.</summary>
    [DataField]
    public float MaxUnlockChance = 0.95f;

    // --- Runtime state machine ---

    /// <summary>Current wave phase. Idle = ready for the next pulse; a pulse is ignored while a wave is live.</summary>
    [DataField]
    public BastionDefenseArtifactsPhase Phase = BastionDefenseArtifactsPhase.Idle;

    /// <summary>When the current phase ends (Holding -> pulse+reel transition, and the reel-timeout deadline).</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextPhaseTime;

    /// <summary>The artifacts (and their tethers) spawned by the current wave.</summary>
    [DataField]
    public List<BastionDefenseArtifactEntry> Active = new();
}

/// <summary>Phase of a Defense A wave.</summary>
public enum BastionDefenseArtifactsPhase : byte
{
    /// <summary>No wave in flight; the next pulse can start one.</summary>
    Idle,

    /// <summary>Artifacts are flung out and holding until <see cref="BastionDefenseArtifactsComponent.NextPhaseTime"/>.</summary>
    Holding,

    /// <summary>Artifacts have pulsed and are being reeled back to the centre; each is deleted on arrival.</summary>
    Reeling,
}

/// <summary>
/// One spawned defensive artifact and the tether binding it to the Paragon. Both are
/// <see cref="PersistentEntityReference"/> so a mid-wave save/reload restores the roster with correct links
/// rather than reassigned UIDs (the wave then reels and deletes them on the next phase as normal).
/// </summary>
[DataDefinition]
public sealed partial class BastionDefenseArtifactEntry
{
    [DataField]
    public PersistentEntityReference Artifact;

    [DataField]
    public PersistentEntityReference Tether;
}
