using Content.Shared._Persistence14.Bastion;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Persistence14.Bastion.Defenses;

/// <summary>
/// Drives the Paragon's defense-pulse clock: counts down only while a player is within range (dormant
/// otherwise), updates the console descriptor for the current band, and fires <see cref="BastionDefensePulseEvent"/>
/// on timeout - or early, if the current band allows an early discharge. The pulse severity scales with how
/// much of the Paragon's graph is unlocked; the active defense (A/B/C) handles the event. Once the Paragon
/// is fully unlocked the defense shuts off for good and the timer parks.
/// </summary>
public sealed class BastionPulseSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedXenoArtifactSystem _xenoArtifact = default!;

    private readonly HashSet<Entity<ActorComponent>> _nearby = new();

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<BastionPulseComponent, XenoArtifactComponent>();
        while (query.MoveNext(out var uid, out var pulse, out var xeno))
        {
            var severity = _xenoArtifact.GetUnlockedFraction((uid, xeno));

            // Fully unlocked: the Paragon is beaten, so the defense shuts off for good. Park the timer.
            if (severity >= 1f)
            {
                pulse.NextPulse = now + NextInterval(pulse);
                SetReadout(uid, pulse, pulse.CompletedDescriptor, dormant: true);
                continue;
            }

            if (!AnyPlayerNear(uid, pulse.ActivationRange))
            {
                // Dormant: keep the timer topped up so it starts fresh the moment players arrive.
                pulse.NextPulse = now + NextInterval(pulse);
                SetReadout(uid, pulse, pulse.DormantDescriptor, dormant: true);
                continue;
            }

            var remaining = (float)(pulse.NextPulse - now).TotalSeconds;
            var band = GetBand(pulse, remaining);

            // Fire on timeout, or roll an early discharge if the current band allows one.
            var earlyFire = band is { EarlyPulseChance: > 0f } b
                            && _random.Prob(Math.Clamp(b.EarlyPulseChance * frameTime, 0f, 1f));

            if (remaining <= 0f || earlyFire)
            {
                var ev = new BastionDefensePulseEvent(uid, severity);
                RaiseLocalEvent(uid, ref ev);

                pulse.NextPulse = now + NextInterval(pulse);
                band = GetBand(pulse, (float)pulse.PulseInterval.TotalSeconds);
            }

            SetReadout(uid, pulse, band?.Descriptor, dormant: false);
        }
    }

    /// <summary>The interval until the next pulse: a random value in [min, max] if both are set, else the fixed one.</summary>
    private TimeSpan NextInterval(BastionPulseComponent pulse)
    {
        if (pulse.PulseIntervalMin is { } min && pulse.PulseIntervalMax is { } max && max > min)
            return TimeSpan.FromSeconds(_random.NextFloat((float)min.TotalSeconds, (float)max.TotalSeconds));

        return pulse.PulseInterval;
    }

    private static BastionPulseBand? GetBand(BastionPulseComponent pulse, float remaining)
    {
        foreach (var band in pulse.Bands)
        {
            if (remaining >= band.From && remaining < band.To)
                return band;
        }

        return null;
    }

    private bool AnyPlayerNear(EntityUid uid, float range)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(uid), range, _nearby);
        return _nearby.Count > 0;
    }

    private void SetReadout(EntityUid uid, BastionPulseComponent pulse, string? descriptor, bool dormant)
    {
        if (pulse.Dormant == dormant && pulse.CurrentDescriptor == descriptor)
            return;

        pulse.Dormant = dormant;
        pulse.CurrentDescriptor = descriptor;
        Dirty(uid, pulse);
    }
}
