using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;

namespace Content.Server._Persistence14.Bastion;

/// <summary>
/// The Bastion encounter keys everything - defense-pulse intensity, the Defense B anomaly ramp, and the
/// teardown-on-completion check - off a single measure: the fraction of the Paragon's nodes that are
/// unlocked, 0 (sealed) .. 1 (fully cracked). This is that measure, in one place, so those three consumers
/// can't drift apart (see the design doc's "severity = fraction unlocked, computed once" intent).
/// </summary>
public static class BastionArtifactExtensions
{
    /// <summary>Fraction of the artifact's nodes that are unlocked, 0..1 (0 if it has no nodes).</summary>
    public static float GetUnlockedFraction(this SharedXenoArtifactSystem xenoArtifact, Entity<XenoArtifactComponent> ent)
    {
        var total = 0;
        var unlocked = 0;
        foreach (var node in xenoArtifact.GetAllNodes(ent))
        {
            total++;
            if (!node.Comp.Locked)
                unlocked++;
        }

        return total == 0 ? 0f : (float)unlocked / total;
    }
}
