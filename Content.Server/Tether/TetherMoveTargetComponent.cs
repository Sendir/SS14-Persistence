namespace Content.Server.Tether;

/// <summary>
/// Marker placed on the entity a <see cref="TetherMoverComponent"/> is currently driving, pointing
/// back at the tether entity. It exists so <see cref="TetherLinkSystem"/> can catch the target's own
/// <c>StartCollideEvent</c> (which is directed at the target, not the tether) and turn a solid hit into a
/// <see cref="TetherMoveEndedEvent"/>. Added when a drive starts, removed when the tether breaks.
/// </summary>
[RegisterComponent, Access(typeof(TetherLinkSystem))]
public sealed partial class TetherMoveTargetComponent : Component
{
    [DataField]
    public EntityUid Tether;
}
