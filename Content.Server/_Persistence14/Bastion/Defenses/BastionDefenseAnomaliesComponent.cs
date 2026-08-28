using Robust.Shared.Prototypes;

namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>
/// Defense B: this Paragon defends by flinging tethers onto its platform and growing a hostile anomaly in
/// at each endpoint, then ramping that anomaly's severity toward supercritical. The tether is a leash - the
/// only thing that can move a Static anomaly is the G.O.R.I.L.L.A. gauntlet (<c>WeaponGauntletGorilla</c>),
/// which punches it through the air; knock one past <see cref="LeashRange"/> and it goes supercritical on
/// the spot. Otherwise it crits on its own ramp. Either way the anomaly's own crit deletes it, so - unlike
/// Defense A - there is nothing to reel back. See <see cref="BastionDefenseAnomaliesSystem"/>.
/// </summary>
[RegisterComponent, Access(typeof(BastionDefenseAnomaliesSystem))]
public sealed partial class BastionDefenseAnomaliesComponent : BastionDefenseComponent
{
    // --- Anomaly source (reused from the anomaly generator, so new anomaly types are picked up automatically) ---

    /// <summary>
    /// The random-anomaly spawner whose list we draw from - the SAME one the anomaly generator uses, so any
    /// anomaly type added there is automatically eligible here. We read its <c>RandomSpawner</c> list and
    /// pick ourselves (rather than spawning the marker) so we get a handle on the resulting anomaly to tether
    /// + grow it; nested spawners (e.g. the rock-anomaly spawner) are resolved recursively. The rare list
    /// (body-injector traps, which have no standalone anomaly) is skipped.
    /// </summary>
    [DataField]
    public EntProtoId AnomalySpawner = "RandomAnomalySpawner";

    // --- Wave size (MinCount/MaxCount are on the base BastionDefenseComponent) ---

    /// <summary>Cap on simultaneously-live defense anomalies for this Paragon, so stacked pulses can't flood.</summary>
    [DataField]
    public int MaxAlive = 20;

    // --- Placement (kept on the ruin platform so crit effects have atmosphere, like Defense A's fling) ---

    [DataField]
    public float MinDistance = 3f;

    [DataField]
    public float MaxDistance = 7f;

    /// <summary>
    /// Leash length (tiles from the Paragon). Only the G.O.R.I.L.L.A. gauntlet can shove a Static anomaly
    /// this far; when one crosses it the tether breaks and the anomaly is forced supercritical immediately.
    /// </summary>
    [DataField]
    public float LeashRange = 7f;

    // --- Severity ramp toward crit (per second), lerped by Paragon severity ---
    // ~0.02/s at a fully-locked Paragon (~50s to crit) climbing to ~0.1/s when fully unlocked (~10s to crit).

    [DataField]
    public float MinRampPerSecond = 0.02f;

    [DataField]
    public float MaxRampPerSecond = 0.1f;

    // --- Grow-in ---

    /// <summary>How long the grow-from-tiny-to-full animation takes once the anomaly materializes on arrival.</summary>
    [DataField]
    public TimeSpan GrowDuration = TimeSpan.FromSeconds(0.8);

    /// <summary>Sprite scale the anomaly starts at while materializing, growing to full (1).</summary>
    [DataField]
    public float StartScale = 0.2f;

    // --- Traps ---

    /// <summary>
    /// How long the tether lingers on a delivered anomaly TRAP before detaching. A trap (an injector, drawn
    /// from the generator's rare list) has no standalone anomaly to ramp - the tether just reaches out, holds
    /// a beat, and lets go, leaving the trap lurking there as a touch hazard.
    /// </summary>
    [DataField]
    public TimeSpan TrapHoldTime = TimeSpan.FromSeconds(1.5);

    // --- Tether visual ---

    [DataField]
    public string TetherVisual = "EyeTetherVisual";

    [DataField]
    public TimeSpan TetherConnectDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan TetherDisconnectDuration = TimeSpan.FromSeconds(2);
}
