using Content.Server.Chat.Systems;
using Content.Shared._Persistence14.ForcedEmote;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Persistence14.ForcedEmote;

/// <summary>
/// Drives the repeat for <see cref="ForcedEmoteStatusEffectComponent"/>: while the status effect is
/// active on a mob, it attempts the emote every interval. Duration and cleanup are handled centrally
/// by <see cref="StatusEffectsSystem"/>, so this system never adds or removes the effect itself.
/// </summary>
public sealed class ForcedEmoteStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ForcedEmoteStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
    }

    private void OnApplied(Entity<ForcedEmoteStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        ent.Comp.NextEmoteTime = _timing.CurTime + ent.Comp.Interval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ForcedEmoteStatusEffectComponent, StatusEffectComponent>();
        while (query.MoveNext(out _, out var forced, out var status))
        {
            if (now < forced.NextEmoteTime)
                continue;

            if (status.AppliedTo is not { } mob)
                continue;

            forced.NextEmoteTime = now + forced.Interval;

            if (!_random.Prob(forced.Chance))
                continue;

            // Non-forced: a mob that cannot perform the emote (wrong species, muzzled, etc.) is simply
            // skipped, matching the previous AutoEmote-driven behavior.
            _chat.TryEmoteWithChat(mob, forced.Emote);
        }
    }
}
