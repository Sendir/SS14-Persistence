using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DeviceLinking.Systems;
using Content.Shared._Persistence14.Bastion;
using Content.Shared._Persistence14.PersistentIdentifier;
using Content.Shared.Atmos.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Xenoarchaeology.Equipment;
using Content.Shared.Xenoarchaeology.Equipment.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Persistence14.Bastion;

/// <summary>
/// Spawns a Bastion Ruin and binds it to its Paragon Artifact key. It builds an asteroid platform (with a
/// simulated atmosphere) at a random spot in open space clear of every other grid, places the pad + Paragon
/// + console on it, and records the two-way key↔ruin link (turning the key into a locator). The Paragon's
/// defense and self-teardown then run from their own systems. All the ruin's entity links are stored as
/// persistent references so it survives a world save/reload.
/// </summary>
public sealed class BastionRuinSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PersistentIdentifierSystem _pid = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedGodmodeSystem _godmode = default!;
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly SharedArtifactAnalyzerSystem _analyzer = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    private const string PadProto = "MachineParagonAnalyzerPad";
    private const string ConsoleProto = "MachineParagonAnalysisConsole";

    /// <summary>The Paragon Artifact prototype for each defense type (each has its own sprite variant).</summary>
    private static string ParagonProtoFor(BastionDefenseType type) => type switch
    {
        BastionDefenseType.Artifacts => "StructureParagonArtifactArtifacts",
        BastionDefenseType.Anomalies => "StructureParagonArtifactAnomalies",
        BastionDefenseType.Mobs => "StructureParagonArtifactMobs",
        _ => "StructureParagonArtifactArtifacts",
    };

    /// <summary>Half-width of the asteroid platform, in tiles (so the ruin is ~(2N+1) square).</summary>
    private const int RuinHalfSize = 9;

    /// <summary>The floor the platform is made of.</summary>
    private const string RuinFloorTile = "FloorAsteroidSand";

    /// <summary>Tiles of slack added around the map's inhabited region, so the ruin can also land a bit out in the void beyond the outermost grids.</summary>
    private const float SpawnRegionMargin = 100f;

    /// <summary>Half-size of the fallback sample area around origin, used only if the map has no other grids at all.</summary>
    private const float FallbackExtent = 250f;

    /// <summary>How many random points to try before giving up. In open space a point is almost always free on the first try.</summary>
    private const int MaxPlacementAttempts = 5;

    /// <summary>
    /// Tries to spawn an empty Bastion Ruin at a random location anywhere on <paramref name="mapId"/>,
    /// clear of every existing grid. Returns false (and spawns nothing) if no free spot is found.
    /// </summary>
    public bool TrySpawnRuin(MapId mapId, BastionDefenseType defenseType, out EntityUid ruin)
    {
        ruin = EntityUid.Invalid;

        if (!TryGetRuinLocation(mapId, out var coords))
            return false;

        var gridEnt = _mapManager.CreateGridEntity(mapId);
        ruin = gridEnt.Owner;

        // Give the grid a simulated atmosphere BEFORE laying tiles, otherwise gas/heat effects just sit on
        // one tile: a GridAtmosphere added after the tiles exist has no TileAtmosphere for any of them, so
        // gas has nowhere to equalize into. With it present first, each SetTile registers its tile, and we
        // also invalidate them explicitly so the sim picks them all up (then gas disperses and vents to
        // space at the open platform's edges). Foam is entity-based, which is why it already worked.
        EnsureComp<GridAtmosphereComponent>(gridEnt.Owner);

        // Tiles span [-RuinHalfSize, RuinHalfSize) so the platform's world extent is symmetric about the
        // tile corner (0,0) - that corner is the field's dead centre, where the 2x2 Paragon goes.
        var floor = new Tile(_tileDefManager[RuinFloorTile].TileId);
        for (var x = -RuinHalfSize; x < RuinHalfSize; x++)
        {
            for (var y = -RuinHalfSize; y < RuinHalfSize; y++)
            {
                var tileCoords = new Vector2i(x, y);
                _map.SetTile(gridEnt.Owner, gridEnt.Comp, tileCoords, floor);
                _atmosphere.InvalidateTile(gridEnt.Owner, tileCoords);
            }
        }

        _transform.SetWorldPosition(gridEnt.Owner, coords);
        var ruinComp = EnsureComp<BastionRuinComponent>(ruin);
        ruinComp.DefenseType = defenseType;

        SpawnRuinContents(gridEnt.Owner, ruinComp);

        var paragonStr = _pid.TryResolveId(ruinComp.Paragon, out var paragonEnt) ? ToPrettyString(paragonEnt.Owner) : "none";
        Log.Info($"Spawned Bastion Ruin {ToPrettyString(ruin)} (paragon {paragonStr}) at {coords} on map {mapId}.");
        return true;
    }

    /// <summary>
    /// Places the fixed structures at the ruin's centre: the analysis pad, the Paragon Artifact on top
    /// of it, and a console beside them. All three are un-unanchorable; the pad + console are godmoded
    /// (the Paragon deliberately is not - see below), the console is device-linked to the pad, and the
    /// pad is pointed at the Paragon.
    /// </summary>
    private void SpawnRuinContents(EntityUid grid, BastionRuinComponent ruinComp)
    {
        // Dead centre of the field is the tile corner (0,0); the 2x2 pieces sit there (their -1..1
        // fixtures cover tiles (-1,-1)-(0,0), centred on the platform). The console sits clear of that.
        var centre = new EntityCoordinates(grid, new Vector2(0f, 0f));
        var consoleAt = new EntityCoordinates(grid, new Vector2(3.5f, 0.5f));

        var pad = Spawn(PadProto, centre);
        var paragon = Spawn(ParagonProtoFor(ruinComp.DefenseType), centre);
        var console = Spawn(ConsoleProto, consoleAt);
        _pid.AssignIdReference(ref ruinComp.Paragon, paragon);

        // Godmode the pad + console (fully invulnerable machines). NOT the Paragon: godmode cancels all
        // damage, which would block its damage-based unlock triggers (radiation, brute, etc.). It has no
        // destruction path (Damageable but no Destructible/MobState) so it can't be destroyed anyway - it
        // just needs to register damage for those triggers to fire.
        _godmode.EnableGodmode(pad);
        _godmode.EnableGodmode(console);

        // Impossible to unanchor - "powered by the artifact", part of the ruin.
        foreach (var structure in new[] { pad, paragon, console })
        {
            RemComp<AnchorableComponent>(structure);
        }

        // Link the console to the pad, and point the pad at the Paragon (a fixed structure never goes
        // through the ItemPlacer path that would normally set this).
        _deviceLink.LinkDefaults(null, console, pad);
        if (TryComp<ArtifactAnalyzerComponent>(pad, out var analyzer))
            _analyzer.SetCurrentArtifact((pad, analyzer), paragon);
    }

    /// <summary>Records the two-way key↔ruin binding and turns the key into a locator for the ruin.</summary>
    public void BindKey(EntityUid key, EntityUid ruin)
    {
        var keyComp = EnsureComp<ParagonArtifactKeyComponent>(key);
        _pid.AssignIdReference(ref keyComp.BastionRuin, ruin);

        if (TryComp<BastionRuinComponent>(ruin, out var ruinComp))
        {
            _pid.AssignIdReference(ref ruinComp.Key, key);

            // Point the Paragon at its key so the console's locked screen can show the key's sprite.
            if (_pid.TryResolveId(ruinComp.Paragon, out var paragonEnt))
            {
                var display = EnsureComp<ParagonKeyDisplayComponent>(paragonEnt.Owner);
                _pid.AssignIdReference(ref display.Key, key);
                Dirty(paragonEnt.Owner, display);
            }
        }

        // Make the key beep toward the ruin while held. Target is the ruin for now; later it will be
        // the Paragon Artifact itself.
        var locator = EnsureComp<ParagonArtifactLocatorComponent>(key);
        _pid.AssignIdReference(ref locator.Target, ruin);
    }

    /// <summary>
    /// Convenience used by both the resync effect and the test command: spawn the ruin somewhere on the
    /// map and bind the given key to it in one call.
    /// </summary>
    public bool TrySpawnRuinForKey(EntityUid key, MapId mapId, BastionDefenseType defenseType, out EntityUid ruin)
    {
        if (!TrySpawnRuin(mapId, defenseType, out ruin))
            return false;

        BindKey(key, ruin);
        return true;
    }

    /// <summary>
    /// Picks a random spot anywhere on the map whose footprint overlaps no existing grid (station,
    /// asteroid, anything). "The map" is taken to be the union of all its grids' bounds, enlarged by a
    /// margin - space maps are otherwise unbounded, so there is no other finite area to sample from.
    ///
    /// Runs only on a ruin spawn (a rare, one-shot event), never per tick: the grid scan is O(grids)
    /// once, and each attempt is a single broadphase query. In open space a candidate is almost always
    /// clear on the first try, so the loop rarely iterates.
    /// </summary>
    private bool TryGetRuinLocation(MapId mapId, out Vector2 result)
    {
        // Build the map's inhabited region from all its grids.
        Box2? inhabited = null;
        foreach (var grid in _mapManager.GetAllGrids(mapId))
        {
            var aabb = _lookup.GetWorldAABB(grid.Owner);
            inhabited = inhabited is { } b ? b.Union(aabb) : aabb;
        }

        var region = inhabited is { } bounds
            ? bounds.Enlarged(SpawnRegionMargin)
            : Box2.CenteredAround(Vector2.Zero, new Vector2(FallbackExtent * 2f, FallbackExtent * 2f));

        // A little larger than the platform so the ruin never spawns flush against another grid.
        var footprint = (RuinHalfSize + 3) * 2f;
        var size = new Vector2(footprint, footprint);

        for (var i = 0; i < MaxPlacementAttempts; i++)
        {
            var candidate = new Vector2(
                _random.NextFloat(region.Left, region.Right),
                _random.NextFloat(region.Bottom, region.Top));

            var boxRot = new Box2Rotated(Box2.CenteredAround(candidate, size), Angle.Zero, candidate);
            if (_mapManager.FindGridsIntersecting(mapId, boxRot).Any())
                continue;

            result = candidate;
            return true;
        }

        result = Vector2.Zero;
        return false;
    }
}
