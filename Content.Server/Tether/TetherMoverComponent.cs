using Robust.Shared.Physics;

namespace Content.Server.Tether;

/// <summary>
/// Active movement layer for a tether: drives the tether's <em>target</em> to a point at
/// <see cref="Distance"/> from the source, along <see cref="Direction"/> (or, with
/// <see cref="AutoDirection"/>, straight along the current source→target line).
///
/// Movement is a proper <b>physics velocity drive</b>: while a drive is set up the target is forced to a
/// <see cref="BodyType.Dynamic"/> body and each tick its velocity is steered toward the destination - capped
/// at <see cref="Speed"/> and ramped by <see cref="Strength"/> (acceleration). Because it moves by velocity
/// through the physics solver, it collides and drags naturally: a collidable target stops when it hits
/// something solid (which fires <see cref="TetherMoveOutcome.Collided"/>), while a phasing target reels
/// straight through. Reaching the destination raises <see cref="TetherMoveEndedEvent"/> without breaking the
/// tether, so a consumer can drive it again (e.g. fling out, then reel back) on the same continuous tether.
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

    /// <summary>Max drive speed - the velocity cap the target is reeled/pushed at, in tiles per second.</summary>
    [DataField]
    public float Speed = 12f;

    /// <summary>
    /// How hard the tether pulls: the acceleration (tiles/s²) it ramps the target's velocity toward the
    /// drive velocity with. Higher = snappier/stronger (reacts to a fling or a change of target faster); very
    /// high is effectively an instant, unstoppable reel. Speed still caps the final drag speed.
    /// </summary>
    [DataField]
    public float Strength = 60f;

    /// <summary>
    /// Runtime: the drive speed we're currently commanding, ramped toward the target speed by
    /// <see cref="Strength"/>. Tracked here rather than read back from the body so that damping or bumping
    /// other mobs can't bleed the reel speed away - every target reels at the same rate regardless of mass.
    /// </summary>
    [DataField]
    public float CurrentSpeed;

    /// <summary>How close (tiles) the target must get to the destination to count as "reached".</summary>
    [DataField]
    public float ArriveTolerance = 0.05f;

    /// <summary>The target's body type before we forced it dynamic; restored on teardown.</summary>
    [DataField]
    public BodyType OriginalBodyType;

    /// <summary>Set once the first drive has forced the target dynamic, so we only do it once.</summary>
    [DataField]
    public bool Setup;
}
