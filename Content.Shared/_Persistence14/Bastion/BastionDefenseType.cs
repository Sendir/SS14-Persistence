namespace Content.Shared._Persistence14.Bastion;

/// <summary>
/// Which defense a Bastion Ruin's Paragon Artifact uses. Each type has its own Paragon sprite variant and
/// its own defense system, selected on the Paragon prototype by the matching defense component.
/// </summary>
public enum BastionDefenseType : byte
{
    /// <summary>Defense A: spawns partially-unlocked artifacts, flings them out on tethers, pulses and reels them back in.</summary>
    Artifacts,

    /// <summary>Defense B: reaches tethers out and grows anomalies in at the endpoints, ramping each toward supercritical.</summary>
    Anomalies,

    /// <summary>Defense C: summons waves of hostile guardian mobs and reels home any that drift into space.</summary>
    Mobs,
}
