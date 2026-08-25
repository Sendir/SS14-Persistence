using Robust.Shared.Physics;

namespace Content.Server.Tether;

/// <summary>
/// Active movement layer for a tether: drives the tether's <em>target</em> to a point at
/// <see cref="Distance"/> from the source, along <see cref="Direction"/> (or, with
/// <see cref="AutoDirection"/>, straight along the current source→target line).
///
/// Movement is <b>kinematic</b>: the target's position is set directly each tick, and while a drive is set
/// up the target is forced to a <see cref="BodyType.Kinematic"/> body so that <em>nothing else</em> - gravity,
/// explosions, throws, other impulses - can move it. It moves only because the tether moves it. Reaching the
/// destination raises <see cref="TetherMoveEndedEvent"/> without breaking the tether, so a consumer can drive
/// it again (e.g. fling out, then reel back) on the same continuous tether.
///
/// Created/updated via <see cref="TetherLinkSystem.TetherDrive"/>.
/// </summary>
[RegisterComponent, Access(typeof(TetherLinkSystem))]
public sealed partial class TetherMoverComponent : Component
{
    /// <summary>Desired distance from the source, in tiles.</summary>
    [DataField]
    public float Distance;

    /// <summary>
    /// Direction from the source to drive toward, used when <see cref="AutoDirection"/> is false
    /// (e.g. flinging an artifact out along a random angle).
    /// </summary>
    [DataField]
    public Angle Direction;

    /// <summary>
    /// If true, the destination direction is recomputed each tick as the current source→target
    /// bearing - i.e. pull the target straight in (or push straight out) along the existing line.
    /// </summary>
    [DataField]
    public bool AutoDirection;

    /// <summary>Whether the drive is currently running. Set false on arrival; a new <see cref="TetherLinkSystem.TetherDrive"/> re-activates it.</summary>
    [DataField]
    public bool Active;

    /// <summary>How fast the target is moved toward the destination, in tiles per second.</summary>
    [DataField]
    public float Speed = 12f;

    /// <summary>How close (tiles) the target must get to the destination to count as "reached".</summary>
    [DataField]
    public float ArriveTolerance = 0.05f;

    /// <summary>The target's body type before we forced it kinematic; restored on teardown.</summary>
    [DataField]
    public BodyType OriginalBodyType;

    /// <summary>Set once the first drive has forced the target kinematic, so we only do it once.</summary>
    [DataField]
    public bool Setup;
}
