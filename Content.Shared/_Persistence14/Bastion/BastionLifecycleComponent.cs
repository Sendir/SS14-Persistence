using Robust.Shared.GameStates;

namespace Content.Shared._Persistence14.Bastion;

/// <summary>
/// Auto-teardown clock for a Paragon Artifact. The ruin removes itself when the Paragon is fully unlocked
/// (after a short grace so players can read the console countdown) or when its lifetime elapses. Common to
/// all Paragon variants. Driven server-side; <see cref="TearingDown"/>/<see cref="TeardownTime"/> are
/// networked so the console can show the countdown.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BastionLifecycleComponent : Component
{
    /// <summary>Time from spawn to automatic teardown if the Paragon isn't completed first.</summary>
    [DataField]
    public TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>Grace window between completing the Paragon and the ruin being torn down.</summary>
    [DataField]
    public TimeSpan CompletionGrace = TimeSpan.FromSeconds(10);

    /// <summary>When the lifetime runs out. Set on map init. Server runtime.</summary>
    [DataField]
    public TimeSpan ExpireTime;

    /// <summary>True once teardown has been scheduled; drives the console countdown.</summary>
    [DataField, AutoNetworkedField]
    public bool TearingDown;

    /// <summary>When the ruin is actually deleted (once <see cref="TearingDown"/>).</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TeardownTime;
}
