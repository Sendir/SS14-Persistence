namespace Content.Shared._Persistence14.Bastion;

/// <summary>
/// Which defense a Bastion Ruin's Paragon Artifact uses. Each type has its own Paragon sprite variant
/// and its own defense system. Behaviour is not implemented yet - the framework is wired but empty.
/// </summary>
public enum BastionDefenseType : byte
{
    /// <summary>Spawns partially-unlocked artifacts, tethered out around the Paragon.</summary>
    Artifacts,

    /// <summary>Spawns anomalies tethered to the Paragon, ramping toward crit.</summary>
    Anomalies,

    /// <summary>Spawns waves of hostile mobs.</summary>
    Mobs,
}
