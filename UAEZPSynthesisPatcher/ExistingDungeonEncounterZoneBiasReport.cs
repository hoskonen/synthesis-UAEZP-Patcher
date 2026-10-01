using System.Text;

namespace UAEZPSynthesisPatcher;

public static class ExistingDungeonEncounterZoneBiasReport
{
    public static void Append(
        StringBuilder text,
        ExistingDungeonEncounterZoneBiasSummary summary,
        int? appliedChanges = null,
        bool includeSamples = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(summary);

        text.AppendLine("Existing dungeon encounter-zone bias:");
        foreach (DungeonCategory category in Enum.GetValues<DungeonCategory>()
                     .Where(category => category != DungeonCategory.None))
        {
            summary.EncounterZonesByCategory.TryGetValue(category, out int count);
            text.AppendLine(
                $"  {DungeonTierBiasReport.FormatCategory(category)} ECZNs: {count}");
        }
        text.AppendLine();
        text.AppendLine("Existing ECZN MinLevel changes:");
        text.AppendLine($"  Planned: {summary.PlannedChanges}");
        if (appliedChanges is not null)
        {
            text.AppendLine($"  Applied: {appliedChanges.Value}");
        }
        text.AppendLine(
            $"  Unchanged at profile ceiling/floor: " +
            $"{summary.UnchangedAtProfileBoundary}");
        text.AppendLine(
            $"  Shared-zone conflicts resolved: " +
            $"{summary.SharedZoneConflictsResolved}");
        text.AppendLine();

        if (!includeSamples)
        {
            return;
        }

        text.AppendLine(
            $"Existing dungeon ECZN sample " +
            $"(max {ReadOnlyPlanner.ExistingDungeonEncounterZoneSampleLimit}):");
        if (summary.Samples.Count == 0)
        {
            text.AppendLine("  (none)");
            text.AppendLine();
            return;
        }

        foreach (ExistingDungeonEncounterZoneSample sample in summary.Samples)
        {
            text.AppendLine(
                $"  CELL {sample.CellFormKey} / " +
                $"{sample.CellEditorId ?? "<no EditorID>"}");
            text.AppendLine(
                $"    Location {FormatLocation(sample.LocationFormKey, sample.LocationEditorId)}");
            text.AppendLine(
                $"    Category: {DungeonTierBiasReport.FormatCategory(sample.Category)}");
            text.AppendLine(
                $"    ECZN {sample.EncounterZoneFormKey} / " +
                $"{sample.EncounterZoneEditorId ?? "<no EditorID>"}");
            text.AppendLine(
                $"    MinLevel: {sample.ExistingMinimumLevel} " +
                $"{sample.TierOffset:+#;-#;0} -> {sample.DesiredMinimumLevel}");
        }
        text.AppendLine();
    }

    private static string FormatLocation(
        Mutagen.Bethesda.Plugins.FormKey? formKey,
        string? editorId)
    {
        return formKey is null
            ? "(none)"
            : $"{formKey} / {editorId ?? "<unresolved or no EditorID>"}";
    }
}
