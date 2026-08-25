using Content.Server._Persistence14.Bastion;
using Content.Shared._Persistence14.Bastion;
using Content.Server.Research.Disk;
using Content.Server.Research.Systems;
using Content.Server.Xenoarchaeology.Artifact;
using Content.Shared.Popups;
using Content.Shared.Xenoarchaeology.Artifact.Components;
using Content.Shared.Xenoarchaeology.Equipment;
using Content.Shared.Xenoarchaeology.Equipment.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Xenoarchaeology.Equipment;

/// <inheritdoc />
public sealed class ArtifactAnalyzerSystem : SharedArtifactAnalyzerSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly XenoArtifactSystem _xenoArtifact = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AnalysisConsoleComponent, AnalysisConsoleExtractButtonPressedMessage>(OnExtractButtonPressed);
        SubscribeLocalEvent<AnalysisConsoleComponent, AnalysisConsoleCycleArtifactMessage>(OnCycleArtifact);
    }

    private void OnExtractButtonPressed(Entity<AnalysisConsoleComponent> ent, ref AnalysisConsoleExtractButtonPressedMessage args)
    {
        // Extracts from every artifact on the analyzer. A regular analyzer only ever has one;
        // an advanced analyzer extracts from all placed artifacts at once.
        if (!TryGetArtifactsFromConsole(ent, out var artifacts))
            return;

        // Bastion console: print the points onto a disk instead of crediting a research server.
        if (TryComp<BastionConsoleComponent>(ent, out var bastion))
        {
            ExtractToDisk(ent, bastion, artifacts);
            return;
        }

        if (!_research.TryGetClientServer(ent, out var server, out var serverComponent))
            return;

        var sumResearch = SumAndConsumeResearch(artifacts);

        // 4-16-25: It's a sad day when a scientist makes negative 5k research
        if (sumResearch <= 0)
            return;

        _research.ModifyServerPoints(server.Value, ClampToInt(sumResearch), serverComponent);

        // Only play feedback once, on the artifact currently shown on the console - an advanced
        // analyzer could hold a hundred artifacts and we don't want a hundred sounds/popups.
        if (TryGetArtifactFromConsole(ent, out var selectedArtifact))
        {
            _audio.PlayPvs(ent.Comp.ExtractSound, selectedArtifact.Value);
            _popup.PopupEntity(Loc.GetString("analyzer-artifact-extract-popup"), selectedArtifact.Value, PopupType.Large);
        }
    }

    /// <summary>
    /// Bastion-console extraction: sum the research the same way, multiply it, and print it onto a
    /// research point disk spawned at the console - instead of crediting a research server.
    /// </summary>
    private void ExtractToDisk(Entity<AnalysisConsoleComponent> ent, BastionConsoleComponent bastion, List<Entity<XenoArtifactComponent>> artifacts)
    {
        var sumResearch = SumAndConsumeResearch(artifacts);
        if (sumResearch <= 0)
            return;

        var points = ClampToInt(sumResearch);
        var disk = Spawn(bastion.DiskPrototype, Transform(ent).Coordinates);
        EnsureComp<ResearchDiskComponent>(disk).Points = points;

        // The stock disk is always named/described "(1000)" regardless of value - rewrite both so the
        // printed disk actually shows the (x-multiplier) payout it carries.
        _metaData.SetEntityName(disk, Loc.GetString("bastion-research-disk-name", ("points", points)));
        _metaData.SetEntityDescription(disk, Loc.GetString("bastion-research-disk-desc", ("points", points)));

        _audio.PlayPvs(ent.Comp.ExtractSound, ent);
        _popup.PopupEntity(Loc.GetString("analyzer-artifact-extract-popup"), ent, PopupType.Large);
    }

    /// <summary>Research points are int; the x-multiplier payout can exceed that, so clamp instead of overflowing.</summary>
    private static int ClampToInt(long value) => (int) Math.Clamp(value, 0, int.MaxValue);

    /// <summary>
    /// Sums (and consumes) the research across every artifact, applying each artifact's own point
    /// multiplier (<see cref="ArtifactPointMultiplierComponent"/>, default 1) - the value is a property
    /// of the artifact, so both the disk and server-credit paths get it.
    /// </summary>
    private long SumAndConsumeResearch(List<Entity<XenoArtifactComponent>> artifacts)
    {
        long total = 0;
        foreach (var artifact in artifacts)
        {
            long artifactSum = 0;
            foreach (var node in _xenoArtifact.GetAllNodes(artifact))
            {
                var research = _xenoArtifact.GetResearchValue(node);
                _xenoArtifact.SetConsumedResearchValue(node, node.Comp.ConsumedResearchValue + research);
                artifactSum += research;
            }

            var multiplier = TryComp<ArtifactPointMultiplierComponent>(artifact, out var mult) ? mult.Multiplier : 1f;
            total += (long) (artifactSum * multiplier);
        }

        return total;
    }

    private void OnCycleArtifact(Entity<AnalysisConsoleComponent> ent, ref AnalysisConsoleCycleArtifactMessage args)
    {
        if (!TryGetAnalyzer(ent, out var analyzer))
            return;

        CycleArtifact(analyzer.Value, args.Forward);
    }
}

