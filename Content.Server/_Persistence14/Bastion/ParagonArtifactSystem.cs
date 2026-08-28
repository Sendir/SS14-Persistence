using Content.Shared._Persistence14.Bastion;
using Content.Shared._Persistence14.PersistentIdentifier;
using Content.Shared.Popups;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Robust.Shared.Containers;

namespace Content.Server._Persistence14.Bastion;

/// <summary>
/// Handles the Paragon Artifact's lock/unlock. It spawns with its graph suppressed, and slotting the
/// ONE key bound to this Paragon's own ruin permanently unsuppresses it. A wrong key - even another
/// valid Paragon key from a different ruin - is refused. The correct key is consumed (deleted) on
/// insert, so it can't be pulled back out and the graph stays unlocked forever.
/// </summary>
public sealed class ParagonArtifactSystem : EntitySystem
{
    [Dependency] private readonly SharedXenoArtifactSystem _xenoArtifact = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PersistentIdentifierSystem _pid = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ParagonArtifactComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<ParagonArtifactComponent, EntInsertedIntoContainerMessage>(OnInserted);
    }

    /// <summary>Refuse anything going into the key slot that isn't THIS Paragon's own key.</summary>
    private void OnInsertAttempt(Entity<ParagonArtifactComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != ent.Comp.KeySlotId)
            return;

        if (!IsCorrectKey(ent, args.EntityUid))
        {
            args.Cancel();
            _popup.PopupEntity(Loc.GetString("paragon-key-wrong"), ent);
        }
    }

    private void OnInserted(Entity<ParagonArtifactComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.KeySlotId || ent.Comp.KeyInserted)
            return;

        // Wrong keys are already blocked by OnInsertAttempt; this is a belt-and-braces guard.
        if (!IsCorrectKey(ent, args.Entity))
            return;

        ent.Comp.KeyInserted = true;

        if (TryComp<XenoArtifactComponent>(ent, out var xeno))
            _xenoArtifact.SetSuppressed((ent, xeno), false);

        // The key is consumed - it can't be removed, and the unlock is permanent.
        QueueDel(args.Entity);

        _popup.PopupEntity(Loc.GetString("paragon-key-inserted"), ent, PopupType.Large);
    }

    /// <summary>
    /// A key is correct only if it is a Paragon key bound to the very ruin that contains this Paragon
    /// (ruin.Paragon == this). This is what stops a key from another ruin unlocking the wrong Paragon.
    /// </summary>
    private bool IsCorrectKey(Entity<ParagonArtifactComponent> paragon, EntityUid key)
    {
        if (!TryComp<ParagonArtifactKeyComponent>(key, out var keyComp))
            return false;
        if (!_pid.TryResolveId(keyComp.BastionRuin, out var ruinEnt))
            return false;
        return TryComp<BastionRuinComponent>(ruinEnt.Owner, out var ruinComp)
            && _pid.CompareId(ruinComp.Paragon, paragon.Owner);
    }
}
