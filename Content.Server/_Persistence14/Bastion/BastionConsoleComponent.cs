namespace Content.Server._Persistence14.Bastion;

/// <summary>
/// Marks an analysis console as a Bastion console: pressing Extract prints the points onto a research
/// point disk instead of crediting a research server (the ruin has none). This is purely the OUTPUT
/// behaviour - the point value/multiplier lives on the artifact
/// (<see cref="Content.Shared._Persistence14.Bastion.ArtifactPointMultiplierComponent"/>), not here.
/// </summary>
[RegisterComponent, Access(typeof(Content.Server.Xenoarchaeology.Equipment.ArtifactAnalyzerSystem))]
public sealed partial class BastionConsoleComponent : Component
{
    /// <summary>The point-disk prototype printed on extract (its Points field is overwritten).</summary>
    [DataField]
    public string DiskPrototype = "ResearchDisk";
}
