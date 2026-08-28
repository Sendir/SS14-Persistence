using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Persistence14.Bastion;

/// <summary>
/// Auto-teardown clock for a Paragon Artifact. The ruin removes itself when the Paragon is fully unlocked
/// (after a short grace so players can read the console countdown) or when its lifetime elapses. Common to
/// all Paragon variants. Driven server-side; <see cref="TearingDown"/>/<see cref="TeardownTime"/> are
/// networked so the console can show the countdown.
///
/// The two absolute deadlines (<see cref="ExpireTime"/>, <see cref="TeardownTime"/>) use
/// <see cref="TimeOffsetSerializer"/> + <c>AutoPausedField</c> so they survive a world save/reload and any
/// map pause intact - a raw timestamp would otherwise jump relative to the clock and fire immediately (or
/// never) on load. The two config values above are relative durations and need no such handling.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BastionLifecycleComponent : Component
{
    /// <summary>Time from spawn to automatic teardown if the Paragon isn't completed first. Relative duration.</summary>
    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>Grace window between completing the Paragon and the ruin being torn down. Relative duration.</summary>
    [DataField]
    public TimeSpan CompletionGrace = TimeSpan.FromSeconds(10);

    /// <summary>Absolute time the lifetime runs out. Set on map init. Server runtime.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ExpireTime;

    /// <summary>True once teardown has been scheduled; drives the console countdown.</summary>
    [DataField, AutoNetworkedField]
    public bool TearingDown;

    /// <summary>Absolute time the ruin is actually deleted (once <see cref="TearingDown"/>).</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan TeardownTime;
}
