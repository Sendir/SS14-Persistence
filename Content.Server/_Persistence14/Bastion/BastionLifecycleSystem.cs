using Content.Shared._Persistence14.Bastion;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Persistence14.Bastion;

/// <summary>
/// Tears a Bastion Ruin down when its Paragon is fully unlocked (after a grace window so players can read
/// the console countdown) or when its lifetime elapses. Players on the grid are moved onto the map first so
/// the teardown never deletes them. Common to all Paragon variants via <see cref="BastionLifecycleComponent"/>.
/// </summary>
public sealed class BastionLifecycleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedXenoArtifactSystem _xenoArtifact = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BastionLifecycleComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<BastionLifecycleComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.ExpireTime = _timing.CurTime + ent.Comp.Lifetime;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<BastionLifecycleComponent, XenoArtifactComponent>();
        while (query.MoveNext(out var uid, out var life, out var xeno))
        {
            if (life.TearingDown)
            {
                if (now >= life.TeardownTime)
                    Teardown(uid);
                continue;
            }

            // Completion (whole graph unlocked) gets the grace window + console countdown; lifetime expiry
            // tears down at once.
            if (_xenoArtifact.GetUnlockedFraction((uid, xeno)) >= 1f)
                BeginTeardown((uid, life), life.CompletionGrace);
            else if (now >= life.ExpireTime)
                BeginTeardown((uid, life), TimeSpan.Zero);
        }
    }

    private void BeginTeardown(Entity<BastionLifecycleComponent> ent, TimeSpan grace)
    {
        ent.Comp.TearingDown = true;
        ent.Comp.TeardownTime = _timing.CurTime + grace;
        Dirty(ent);
    }

    private void Teardown(EntityUid paragon)
    {
        if (Transform(paragon).GridUid is not { } grid)
        {
            QueueDel(paragon); // no grid to remove; just clear the paragon
            return;
        }

        // Never delete the grid until we can move its mobs/minds to safety - a soft-lock is far worse than
        // a ruin that lingers a few extra ticks. If the map somehow can't be resolved we bail and retry
        // next tick (TearingDown stays set), rather than deleting mobs.
        if (Transform(grid).MapUid is not { } mapUid)
            return;

        EvacuateGrid(grid, mapUid);
        QueueDel(grid);
    }

    /// <summary>
    /// Moves everything off the grid that could be - or could be holding/controlling - a player, so the
    /// teardown can never strand a mind. Deliberately broad rather than enumerating every mind mechanic:
    /// any mob (polymorphed, mind-swapped, NPC), anything that owns or is visited by a mind (bodies, mind
    /// vessels, observers), and anything currently controlled. Ruin infrastructure (artifacts, machines,
    /// junk) has none of these and is deleted with the grid as intended. An entity moved by one pass has
    /// its grid changed, so later passes skip it.
    /// </summary>
    private void EvacuateGrid(EntityUid grid, EntityUid mapUid)
    {
        MoveOffGrid<MobStateComponent>(grid, mapUid);
        MoveOffGrid<MindContainerComponent>(grid, mapUid);
        MoveOffGrid<VisitingMindComponent>(grid, mapUid);
        MoveOffGrid<ActorComponent>(grid, mapUid);
    }

    private void MoveOffGrid<T>(EntityUid grid, EntityUid mapUid) where T : IComponent
    {
        var query = EntityQueryEnumerator<T, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != grid)
                continue;

            // Suppress grid-traversal for this move. Otherwise SetCoordinates re-parents the entity straight
            // back onto the grid (it's still positioned over it) and the grid QueueDel then deletes it anyway
            // - this is exactly what was deleting a ghost sitting on the ruin. Restoring the flag is safe:
            // traversal only re-checks on the next move, by which point the grid is gone.
            var hadTraversal = xform.GridTraversal;
            xform.GridTraversal = false;
            _transform.SetCoordinates(uid, new EntityCoordinates(mapUid, _transform.GetWorldPosition(uid)));
            xform.GridTraversal = hadTraversal;

            // Reparenting grid->map leaves the mover's RelativeEntity pointing at the grid: the parent-change
            // handler only refreshes it on a *map* change and holds the old relative during its lerp window.
            // Once we delete the grid that reference dangles, and PVS state-sending calls GetNetEntity on the
            // dead grid every tick for every evacuated mob (an error flood). Repoint it at the map now.
            if (TryComp<InputMoverComponent>(uid, out var mover) && mover.RelativeEntity == grid)
            {
                mover.RelativeEntity = mapUid;
                Dirty(uid, mover);
            }
        }
    }
}
