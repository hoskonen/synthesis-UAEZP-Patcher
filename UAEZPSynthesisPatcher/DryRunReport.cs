using System.Text;

namespace UAEZPSynthesisPatcher;

public static class DryRunReport
{
    private const int PerPluginLimit = 10;

    public static string Render(PlanningRun run, Settings settings)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(settings);

        var text = new StringBuilder();
        text.AppendLine("============================================================");
        text.AppendLine(" UAEZP Synthesis Patcher - Dry Run");
        text.AppendLine("============================================================");
        text.AppendLine();
        text.AppendLine($"Source plugin: {run.SourcePlugin.ModKey.FileName.String}");
        text.AppendLine($"Source variant: {run.SourcePlugin.Variant}");
        text.AppendLine(
            $"Algorithm version: {DeterministicDummyZoneSelector.AlgorithmVersion}");
        text.AppendLine($"Dummy-zone mode: {FormatMode(settings.DummyZoneMode)}");
        text.AppendLine($"Seed: {settings.Seed}");
        text.AppendLine();
        text.AppendLine(
            $"Dummy zones discovered: {run.SourcePlugin.DummyZones.Count}");

        foreach (ValidatedDummyZone zone in run.SourcePlugin.DummyZones)
        {
            text.AppendLine(
                $"  {zone.FormKey} | {zone.EditorId} | " +
                $"min {zone.MinLevel} | max {zone.MaxLevel} | flags {zone.Flags}");
        }

        text.AppendLine();
        AppendSummary(text, "CELL", run.Plan.Cells, "XEZN");
        AppendSummary(text, "WRLD", run.Plan.Worldspaces, "XEZN");
        AppendSummary(
            text,
            "ECZN",
            run.Plan.EncounterZones,
            "Disable Combat Boundary");

        AppendExistingStateProvenance(
            text,
            run.Plan.ExistingStateProvenance);

        text.AppendLine("Planned overrides:");
        text.AppendLine($"  CELL: {run.Plan.Cells.PlannedOverrides}");
        text.AppendLine($"  WRLD: {run.Plan.Worldspaces.PlannedOverrides}");
        text.AppendLine($"  ECZN: {run.Plan.EncounterZones.PlannedOverrides}");
        text.AppendLine($"  Total: {run.Plan.TotalPlannedOverrides}");
        text.AppendLine();

        text.AppendLine("Dummy-zone assignment distribution:");
        foreach (ValidatedDummyZone zone in run.SourcePlugin.DummyZones)
        {
            text.AppendLine(
                $"  {zone.EditorId} ({zone.FormKey}, min {zone.MinLevel}): " +
                run.Plan.AssignmentDistribution[zone.FormKey]);
        }

        AppendPerPluginSummary(text, run.Plan);
        text.AppendLine();
        text.AppendLine("Dry run complete. No Skyrim records were modified.");
        return text.ToString();
    }

    private static void AppendSummary(
        StringBuilder text,
        string signature,
        RecordPlanSummary summary,
        string requirement)
    {
        text.AppendLine($"{signature}:");
        text.AppendLine(
            $"  Winning records scanned: {summary.WinningRecordsScanned}");
        text.AppendLine(
            $"  Missing {requirement}: {summary.MissingRequirement}");
        text.AppendLine(
            $"  Existing {requirement}: {summary.ExistingRequirement}");
        text.AppendLine($"  Deleted skipped: {summary.DeletedSkipped}");
        if (summary.UnresolvedExistingReferences > 0)
        {
            text.AppendLine(
                $"  Existing non-null links that do not resolve (preserved): " +
                summary.UnresolvedExistingReferences);
        }
        text.AppendLine();
    }

    private static void AppendPerPluginSummary(
        StringBuilder text,
        PatchPlan plan)
    {
        if (plan.CellAndWorldspaceChangesByOriginPlugin.Count == 0)
        {
            return;
        }

        text.AppendLine();
        text.AppendLine(
            $"CELL/WRLD plans by origin plugin (top {PerPluginLimit}):");

        foreach ((var modKey, int count) in
                 plan.CellAndWorldspaceChangesByOriginPlugin
                     .OrderByDescending(pair => pair.Value)
                     .ThenBy(
                         pair => pair.Key.FileName.String,
                         StringComparer.OrdinalIgnoreCase)
                     .Take(PerPluginLimit))
        {
            text.AppendLine($"  {modKey.FileName.String}: {count}");
        }
    }

    private static void AppendExistingStateProvenance(
        StringBuilder text,
        ExistingStateProvenance provenance)
    {
        text.AppendLine("Existing-state provenance:");
        text.AppendLine();

        AppendLinkProvenance(text, "CELL", provenance.Cells);
        AppendLinkProvenance(text, "WRLD", provenance.Worldspaces);

        text.AppendLine(
            "ECZN Disable Combat Boundary by winning plugin:");
        AppendPluginCounts(text, provenance.EncounterZones.ByWinningPlugin, "  ");
        text.AppendLine();

        text.AppendLine(
            $"Suspicious existing CELL XEZN sample " +
            $"(non-base winning plugins, max " +
            $"{ProvenanceAnalyzer.SuspiciousCellSampleLimit}):");

        if (provenance.Cells.SuspiciousSamples.Count == 0)
        {
            text.AppendLine("  (none)");
        }
        else
        {
            foreach (ExistingLinkSample sample in
                     provenance.Cells.SuspiciousSamples)
            {
                string target = sample.EncounterZoneTarget?.ToString() ??
                    "<unavailable>";
                string targetEditorId =
                    sample.ResolvedEncounterZoneEditorId ??
                    (sample.IsResolved ? "<no EditorID>" : "<unresolved>");

                text.AppendLine(
                    $"  {sample.FormKey} | " +
                    $"{sample.EditorId ?? "<no EditorID>"} | " +
                    $"winner {sample.WinningModKey.FileName.String} | " +
                    $"XEZN -> {target} {targetEditorId}");
            }
        }

        text.AppendLine();
    }

    private static void AppendLinkProvenance(
        StringBuilder text,
        string signature,
        ExistingLinkProvenance provenance)
    {
        text.AppendLine($"{signature} XEZN by winning plugin:");
        text.AppendLine($"  Resolved: {provenance.ResolvedLinks}");
        text.AppendLine($"  Unresolved: {provenance.UnresolvedLinks}");
        AppendPluginCounts(text, provenance.ByWinningPlugin, "  ");
        text.AppendLine();

        text.AppendLine(
            $"{signature} XEZN targeting UAEZP dummy zones:");
        text.AppendLine($"  Total: {provenance.UaeZpDummyZoneLinks}");
        text.AppendLine("  By winning plugin:");
        AppendPluginCounts(
            text,
            provenance.UaeZpDummyZoneLinksByWinningPlugin,
            "    ");
        text.AppendLine();
    }

    private static void AppendPluginCounts(
        StringBuilder text,
        IReadOnlyDictionary<Mutagen.Bethesda.Plugins.ModKey, int> counts,
        string indentation)
    {
        if (counts.Count == 0)
        {
            text.AppendLine($"{indentation}(none)");
            return;
        }

        foreach ((var modKey, int count) in counts
                     .OrderByDescending(pair => pair.Value)
                     .ThenBy(
                         pair => pair.Key.FileName.String,
                         StringComparer.OrdinalIgnoreCase)
                     .ThenBy(
                         pair => pair.Key.FileName.String,
                         StringComparer.Ordinal)
                     .Take(PerPluginLimit))
        {
            text.AppendLine(
                $"{indentation}{modKey.FileName.String}: {count}");
        }
    }

    private static string FormatMode(DummyZoneMode mode)
    {
        return mode switch
        {
            DummyZoneMode.DeterministicRandom => "Deterministic Random",
            DummyZoneMode.First => "First / Fixed",
            _ => mode.ToString(),
        };
    }
}
