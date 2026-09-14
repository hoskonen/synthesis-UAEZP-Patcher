using Mutagen.Bethesda.Plugins;

namespace UAEZPSynthesisPatcher;

public static class ProvenanceAnalyzer
{
    public const int SuspiciousCellSampleLimit = 25;

    private static readonly HashSet<string> ExpectedWinningPluginNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Skyrim.esm",
            "Update.esm",
            "Dawnguard.esm",
            "HearthFires.esm",
            "Dragonborn.esm",
            "UAEZP.esp",
        };

    public static ExistingStateProvenance Analyze(
        IReadOnlyList<ValidatedDummyZone> dummyZones,
        IEnumerable<RecordSnapshot> cells,
        IEnumerable<RecordSnapshot> worldspaces,
        IEnumerable<RecordSnapshot> encounterZones)
    {
        ArgumentNullException.ThrowIfNull(dummyZones);
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(worldspaces);
        ArgumentNullException.ThrowIfNull(encounterZones);

        var dummyFormKeys = dummyZones
            .Select(zone => zone.FormKey)
            .ToHashSet();

        return new ExistingStateProvenance(
            AnalyzeLinks(cells, dummyFormKeys, includeSuspiciousSamples: true),
            AnalyzeLinks(worldspaces, dummyFormKeys, includeSuspiciousSamples: false),
            AnalyzeFlags(encounterZones));
    }

    private static ExistingLinkProvenance AnalyzeLinks(
        IEnumerable<RecordSnapshot> records,
        IReadOnlySet<FormKey> dummyFormKeys,
        bool includeSuspiciousSamples)
    {
        int resolved = 0;
        int unresolved = 0;
        int dummyLinks = 0;
        var byWinningPlugin = new Dictionary<ModKey, int>();
        var dummyLinksByWinningPlugin = new Dictionary<ModKey, int>();
        var suspiciousSamples = new List<ExistingLinkSample>();

        foreach (RecordSnapshot record in records)
        {
            if (record.IsDeleted || !record.RequirementSatisfied)
            {
                continue;
            }

            Increment(byWinningPlugin, record.WinningModKey);

            if (record.HasUnresolvedRequirementReference)
            {
                unresolved++;
            }
            else
            {
                resolved++;
            }

            if (record.EncounterZoneTarget is FormKey target &&
                dummyFormKeys.Contains(target))
            {
                dummyLinks++;
                Increment(dummyLinksByWinningPlugin, record.WinningModKey);
            }

            if (includeSuspiciousSamples &&
                !ExpectedWinningPluginNames.Contains(
                    record.WinningModKey.FileName.String))
            {
                suspiciousSamples.Add(new ExistingLinkSample(
                    record.FormKey,
                    record.EditorId,
                    record.WinningModKey,
                    record.EncounterZoneTarget,
                    record.ResolvedEncounterZoneEditorId,
                    !record.HasUnresolvedRequirementReference));
            }
        }

        IReadOnlyList<ExistingLinkSample> orderedSamples = suspiciousSamples
            .OrderBy(sample => sample.WinningModKey.FileName.String,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(sample => sample.FormKey.ModKey.FileName.String,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(sample => sample.FormKey.ID)
            .Take(SuspiciousCellSampleLimit)
            .ToList();

        return new ExistingLinkProvenance(
            resolved,
            unresolved,
            byWinningPlugin,
            dummyLinks,
            dummyLinksByWinningPlugin,
            orderedSamples);
    }

    private static ExistingFlagProvenance AnalyzeFlags(
        IEnumerable<RecordSnapshot> records)
    {
        var byWinningPlugin = new Dictionary<ModKey, int>();

        foreach (RecordSnapshot record in records)
        {
            if (!record.IsDeleted && record.RequirementSatisfied)
            {
                Increment(byWinningPlugin, record.WinningModKey);
            }
        }

        return new ExistingFlagProvenance(byWinningPlugin);
    }

    private static void Increment(IDictionary<ModKey, int> counts, ModKey modKey)
    {
        counts.TryGetValue(modKey, out int current);
        counts[modKey] = current + 1;
    }
}
