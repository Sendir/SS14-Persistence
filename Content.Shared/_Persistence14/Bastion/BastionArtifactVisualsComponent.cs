using Robust.Shared.GameStates;

namespace Content.Shared._Persistence14.Bastion;

/// <summary>
/// Drives the unlock/activation sprite effects on a Bastion artifact (the Paragon variants) that has the
/// effect layers but NOT <c>RandomArtifactSprite</c> - the standard artifact visualizer only runs on
/// RandomArtifactSprite entities, so without this the Paragon never glows on unlock or flashes on
/// activation. The server side sets the appearance data on the unlock/activation events; the client side
/// toggles the layer visibility. The layers themselves are mapped in the prototype's Sprite.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BastionArtifactVisualsComponent : Component
{
    /// <summary>How long the activation flash stays visible after a node activates.</summary>
    [DataField]
    public TimeSpan ActivationTime = TimeSpan.FromSeconds(2);

    /// <summary>Server runtime: when the current activation flash started (null = not flashing).</summary>
    public TimeSpan? ActivationStart;
}
