using System.Numerics;
using Content.Server.Administration;
using Content.Shared._Persistence14.Bastion;
using Content.Shared.Administration;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Robust.Shared.Console;
using Robust.Shared.Random;

namespace Content.Server._Persistence14.Bastion;

/// <summary>
/// Shared logic for the Bastion test-spawn commands: spawn a ready single-node Paragon key at the
/// caller's feet and an active Bastion Ruin - of the requested defense type, or random - bound to it,
/// then report the ruin's coordinates. Subclasses only pick the defense type and the command name.
/// </summary>
public abstract class BaseSpawnBastionRuinCommand : LocalizedEntityCommands
{
    [Dependency] private readonly BastionRuinSystem _bastionRuin = default!;
    [Dependency] private readonly SharedXenoArtifactSystem _xenoArtifact = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const string KeyArtifactProto = "ParagonArtifactKeyTest";

    private static readonly BastionDefenseType[] AllTypes =
        { BastionDefenseType.Artifacts, BastionDefenseType.Anomalies, BastionDefenseType.Mobs };

    /// <summary>The defense type to spawn, or null to pick one at random.</summary>
    protected abstract BastionDefenseType? Defense { get; }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } player)
        {
            shell.WriteError(Loc.GetString("cmd-spawnbastionruin-no-player"));
            return;
        }

        var entCoords = EntityManager.GetComponent<TransformComponent>(player).Coordinates;
        var mapId = _transform.GetMapCoordinates(player).MapId;
        var defense = Defense ?? _random.Pick(AllTypes);

        var artifact = EntityManager.SpawnEntity(KeyArtifactProto, entCoords);

        // Unlock the artifact's single node so the key is immediately usable.
        if (EntityManager.TryGetComponent<XenoArtifactComponent>(artifact, out var xeno))
        {
            foreach (var node in _xenoArtifact.GetAllNodes((artifact, xeno)))
                _xenoArtifact.SetNodeUnlocked((artifact, xeno), node);
        }

        if (!_bastionRuin.TrySpawnRuinForKey(artifact, mapId, defense, out var ruin))
        {
            EntityManager.DeleteEntity(artifact);
            shell.WriteError(Loc.GetString("cmd-spawnbastionruin-no-space"));
            return;
        }

        var pos = _transform.GetWorldPosition(ruin);
        shell.WriteLine(Loc.GetString("cmd-spawnbastionruin-success",
            ("defense", defense.ToString()),
            ("artifact", EntityManager.GetNetEntity(artifact)),
            ("ruin", EntityManager.GetNetEntity(ruin)),
            ("x", MathF.Round(pos.X, 1)),
            ("y", MathF.Round(pos.Y, 1))));
    }
}

/// <summary>Spawns a Bastion Ruin with a random defense.</summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class SpawnBastionRuinRandomCommand : BaseSpawnBastionRuinCommand
{
    public override string Command => "spawnbastionruinrandom";
    protected override BastionDefenseType? Defense => null;
}

/// <summary>Spawns a Bastion Ruin with the Artifacts defense.</summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class SpawnBastionRuinArtifactsCommand : BaseSpawnBastionRuinCommand
{
    public override string Command => "spawnbastionruinartifacts";
    protected override BastionDefenseType? Defense => BastionDefenseType.Artifacts;
}

/// <summary>Spawns a Bastion Ruin with the Anomalies defense.</summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class SpawnBastionRuinAnomaliesCommand : BaseSpawnBastionRuinCommand
{
    public override string Command => "spawnbastionruinanomalies";
    protected override BastionDefenseType? Defense => BastionDefenseType.Anomalies;
}

/// <summary>Spawns a Bastion Ruin with the Mobs defense.</summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class SpawnBastionRuinMobsCommand : BaseSpawnBastionRuinCommand
{
    public override string Command => "spawnbastionruinmobs";
    protected override BastionDefenseType? Defense => BastionDefenseType.Mobs;
}
