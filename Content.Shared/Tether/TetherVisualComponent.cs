using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared.Tether;

/// <summary>
/// Marks an entity as a purely visual tether connecting two other entities. Geometry (position,
/// rotation, and the curved "S-wiggle" shape) is computed entirely client-side, every frame,
/// directly from Source/Target's current world positions - see
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TetherVisualComponent : Component
{
    /// <summary>One end of the tether. If this entity stops existing, the tether removes itself.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid Source;

    /// <summary>The other end of the tether. If this entity stops existing, the tether removes itself.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid Target;

    /// <summary>
    /// A fixed map position the tether reaches toward while it has NO <see cref="Target"/> entity yet - the
    /// "reach out to empty space, then spawn the thing on arrival" pattern. While this is set (and Target is
    /// invalid) the tether draws to this point and its missing target is NOT treated as "gone". Cleared once a
    /// real Target is attached (see Content.Server.Tether.TetherLinkSystem.AttachTarget).
    /// </summary>
    [DataField, AutoNetworkedField]
    public MapCoordinates? TargetCoords;

    /// <summary>
    /// How many short segments the tether is built from. Each is an individual sprite layer
    /// (declared in the entity's prototype - there must be at least this many layers, extras are
    /// simply left unused), independently positioned/rotated/scaled every client frame to trace
    /// out a curve between Source and Target rather than a single rigid straight line.
    /// </summary>
    [DataField]
    public int SegmentCount = 7;

    /// <summary>
    /// If true, the tether curves in a gentle, continuously animated S-shape instead of sitting
    /// perfectly straight - both endpoints stay exactly anchored to Source and Target regardless
    /// (the curve's sideways displacement is mathematically zero at both ends), only the middle
    /// sways. Purely cosmetic and computed independently by each client using this entity's own
    /// ID as a seed, so it's at least stable per-entity rather than jittering differently every
    /// frame - not networked, since exact sync of a cosmetic wiggle across observers doesn't
    /// matter.
    /// </summary>
    [DataField]
    public bool Wiggle = true;

    /// <summary>How far, in tiles, the curve's midpoint sways side to side at its peak.</summary>
    [DataField]
    public float WiggleAmplitude = 0.25f;

    /// <summary>How fast the curve's sway oscillates, in cycles per second.</summary>
    [DataField]
    public float WiggleSpeed = 1.2f;

    /// <summary>
    /// How many full S-bends appear along the tether's length at once. 1 gives a single gentle
    /// bow shape; higher values give a more snake-like series of curves.
    /// </summary>
    [DataField]
    public float WiggleWaves = 1f;

    /// <summary>
    /// How far, in tiles, to pull the tether's starting point away from Source's exact center,
    /// always along the direction toward Target - so for something like an eye-shaped sprite,
    /// the tether can start near its edge instead of dead in the middle (covering up a pupil,
    /// for example) regardless of which direction the target happens to be in. 0 (default)
    /// starts exactly at Source's center, matching the original behavior.
    /// </summary>
    [DataField]
    public float SourceOffsetDistance;

    /// <summary>
    /// Same as SourceOffsetDistance, but pulls the tether's ending point in from Target's exact
    /// center instead, along the direction back toward Source. 0 (default) ends exactly at
    /// Target's center, matching the original behavior.
    /// </summary>
    [DataField]
    public float TargetOffsetDistance;

    /// <summary>
    /// How long the tether takes to visually extend from Source out to Target after spawning -
    /// the "reaching out" travel time. Zero (default) makes the connection instant, matching
    /// the original behavior. Animated entirely client-side off each client's own view of when
    /// the tether appeared, so it needs no extra networking.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ConnectDuration = TimeSpan.Zero;

    /// <summary>
    /// How long the tether takes to visually retract from Target back into Source once
    /// disconnecting begins (see DisconnectStartedAt), before the server deletes it. Zero
    /// (default) deletes instantly, matching the original behavior.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DisconnectDuration = TimeSpan.Zero;

    /// <summary>
    /// Set (once) by the server the moment this tether is spawned - the authoritative start of
    /// the connect ("reach out") animation, exactly symmetric with DisconnectStartedAt. Clients
    /// compare it against their own clock (which shares the server's tick timeline) to drive the
    /// extend animation. A client that only comes into view long after this simply computes
    /// progress >= 1 and sees the tether fully extended, with no replay.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? ConnectStartedAt;

    /// <summary>
    /// Set (once) by the server the moment this tether starts disconnecting - see
    /// Content.Server.Tether.TetherVisualSystem.BeginDisconnect. Networked so every client can
    /// play the retract animation from the same authoritative starting moment; the server
    /// deletes the entity once DisconnectDuration has elapsed past this.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? DisconnectStartedAt;

    /// <summary>
    /// Target's last-known map position, kept continuously up to date SERVER-side (see
    /// Content.Server.Tether.TetherVisualSystem) and networked. While the tether is connected it's
    /// overwritten every tick with Target's live position, then it stops updating the moment disconnection
    /// begins - so it naturally freezes at where the target was when the tether broke (the retract anchors
    /// here rather than chasing a victim who keeps moving). Crucially it also survives the target ENTITY
    /// being deleted (an anomaly vaporizing itself in a supercritical): the last value stays as a fallback,
    /// and the server clears the now-dangling <see cref="Target"/> ref to <see cref="EntityUid.Invalid"/> on
    /// disconnect (so PVS never tries to network a deleted entity - the "can't resolve MetaDataComponent"
    /// spam), leaving the client to anchor the retract to this snapshot instead of the gone entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public MapCoordinates? LastKnownTargetCoords;
}
