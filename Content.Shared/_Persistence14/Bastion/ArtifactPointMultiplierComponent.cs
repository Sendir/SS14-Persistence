using Robust.Shared.GameStates;

namespace Content.Shared._Persistence14.Bastion;

/// <summary>
/// Multiplies the research points this artifact yields when extracted on an analysis console. It lives
/// on the ARTIFACT because the value is a property of the artifact, not of the machine reading it - so
/// any console (server-crediting or disk-printing) applies it, and the console keeps only the
/// output behaviour. Default 1 = unchanged; the Paragon uses 20. Networked so the console's extract
/// preview reflects it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ArtifactPointMultiplierComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Multiplier = 1f;
}
