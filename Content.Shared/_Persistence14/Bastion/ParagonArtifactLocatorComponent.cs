using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Persistence14.Bastion;

/// <summary>
/// Turns a Paragon Artifact key into a locator: while held, it beeps toward <see cref="Target"/>,
/// faster the closer you are ("hot/cold"), so you can home in on the Bastion Ruin across the map.
///
/// <see cref="Target"/> currently points at the ruin grid; later it will point at the Paragon Artifact
/// itself. Added to the key when it is bound to a ruin (see BastionRuinSystem.BindKey).
/// </summary>
[RegisterComponent]
public sealed partial class ParagonArtifactLocatorComponent : Component
{
    /// <summary>What the locator homes in on. The Bastion Ruin for now; the Paragon Artifact later.</summary>
    [DataField]
    public EntityUid? Target;

    /// <summary>At or beyond this distance (tiles) beeps are slowest; they speed up smoothly to 0.</summary>
    [DataField]
    public float MaxScaleDistance = 500f;

    /// <summary>Fastest beep gap (when right on top of the target).</summary>
    [DataField]
    public TimeSpan MinInterval = TimeSpan.FromSeconds(0.2);

    /// <summary>Slowest beep gap (when far away).</summary>
    [DataField]
    public TimeSpan MaxInterval = TimeSpan.FromSeconds(1.5);

    /// <summary>The beep sound.</summary>
    [DataField]
    public SoundSpecifier BeepSound = new SoundPathSpecifier("/Audio/Items/locator_beep.ogg");

    /// <summary>
    /// Half-angle of the "heading toward the target" cone. The locator only beeps while the holder is
    /// MOVING within this of the direction to the target - i.e. flying roughly the right way. 45° (a
    /// 90° cone) keeps it forgiving enough that a diagonal target still registers while you travel on a
    /// cardinal heading, instead of the beep dying in the gap between directions.
    /// </summary>
    [DataField]
    public Angle HeadingTolerance = Angle.FromDegrees(45);

    /// <summary>
    /// Minimum speed (tiles/sec) before heading can be judged. Below this the holder is effectively
    /// stationary and we can't tell which way they're going, so the locator stays silent - move to get
    /// a reading. (In space you keep drifting, so this rarely trips once you're underway.)
    /// </summary>
    [DataField]
    public float MinSpeed = 0.5f;

    /// <summary>Within this distance (tiles) the heading gate is skipped - you've basically arrived, so it beeps no matter how you're moving.</summary>
    [DataField]
    public float ArrivedDistance = 3f;

    /// <summary>Server-side runtime: when the next beep is due.</summary>
    public TimeSpan NextBeep;
}
