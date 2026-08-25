namespace Content.Server.Tether;

/// <summary>
/// Logic layer that pairs a visual tether (<see cref="TetherVisualComponent"/> /
/// <see cref="TetherVisualSystem"/>) with automatic lifecycle events. Lives on the tether entity
/// itself - which already knows both endpoints - so consumers never hand-roll an Update loop that
/// watches for an endpoint being deleted, a target going down, wandering out of range, or ending up
/// on another map, nor a timer for when the "reach out" animation finishes.
///
/// <see cref="TetherLinkSystem"/> raises:
/// <list type="bullet">
/// <item><see cref="TetherConnectedEvent"/> once, when the connect ("reach out") animation completes.</item>
/// <item><see cref="TetherLinkBrokenEvent"/> when any configured break condition trips (or a consumer
/// calls <see cref="TetherLinkSystem.BreakLink"/>).</item>
/// </list>
///
/// Create one with <see cref="TetherLinkSystem.TetherLink"/> rather than adding it by hand.
/// </summary>
[RegisterComponent, Access(typeof(TetherLinkSystem))]
public sealed partial class TetherLinkComponent : Component
{
    /// <summary>
    /// Break the link once the straight-line distance between the two endpoints exceeds this many
    /// tiles. Null (default) never breaks on distance. Reported as <see cref="TetherBreakReason.OutOfRange"/>.
    /// </summary>
    [DataField]
    public float? MaxDistance;

    /// <summary>
    /// Break the link when the target is no longer alive (critical or dead). Reads MobState; a target
    /// without one is ignored. Reported as <see cref="TetherBreakReason.TargetDied"/>.
    /// </summary>
    [DataField]
    public bool BreakOnTargetNotAlive;

    /// <summary>
    /// Break the link if the endpoints end up on different maps. On by default (a cross-map tether
    /// can't be drawn sensibly). Reported as <see cref="TetherBreakReason.DifferentMap"/>.
    /// </summary>
    [DataField]
    public bool BreakOnDifferentMap = true;

    /// <summary>
    /// Set true once <see cref="TetherConnectedEvent"/> has fired, so it only fires a single time.
    /// </summary>
    [DataField]
    public bool Established;
}
