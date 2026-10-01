using System.Text;

namespace UAEZPSynthesisPatcher;

public static class ApplyReport
{
    public static string Render(PatchPlan plan, ApplyResult result)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(result);

        var text = new StringBuilder();
        text.AppendLine("============================================================");
        text.AppendLine(" UAEZP Synthesis Patcher - Apply");
        text.AppendLine("============================================================");
        text.AppendLine();
        text.AppendLine(
            $"Difficulty profile: {plan.DifficultyProfileDisplayName}");
        text.AppendLine();
        text.AppendLine("Planned overrides:");
        text.AppendLine($"  CELL: {plan.Cells.PlannedOverrides}");
        text.AppendLine($"  WRLD: {plan.Worldspaces.PlannedOverrides}");
        text.AppendLine($"  ECZN: {plan.EncounterZones.PlannedOverrides}");
        text.AppendLine($"  Total: {plan.TotalPlannedOverrides}");
        text.AppendLine();
        text.AppendLine("Planned FWMF XEZN forwards:");
        text.AppendLine($"  CELL: {plan.FwmfForwards.Cells}");
        text.AppendLine($"  WRLD: {plan.FwmfForwards.Worldspaces}");
        text.AppendLine($"  Total: {plan.FwmfForwards.Total}");
        text.AppendLine();
        text.AppendLine("Applied/verified overrides:");
        text.AppendLine($"  CELL: {result.CellsApplied}");
        text.AppendLine($"  WRLD: {result.WorldspacesApplied}");
        text.AppendLine($"  ECZN: {result.EncounterZonesApplied}");
        text.AppendLine($"  Total: {result.TotalApplied}");
        text.AppendLine();
        text.AppendLine("Applied/verified FWMF XEZN forwards:");
        text.AppendLine(
            $"  CELL: {result.CellsForwardedThroughFwmf}");
        text.AppendLine(
            $"  WRLD: {result.WorldspacesForwardedThroughFwmf}");
        text.AppendLine(
            $"  Total: " +
            $"{result.CellsForwardedThroughFwmf + result.WorldspacesForwardedThroughFwmf}");
        text.AppendLine();
        text.AppendLine("ECZN difficulty changes:");
        text.AppendLine($"  Planned: {plan.EncounterZoneDifficultyChanges}");
        text.AppendLine(
            $"  Applied: {result.EncounterZoneDifficultyChangesApplied}");
        text.AppendLine();
        text.AppendLine("ECZN combat-boundary changes:");
        text.AppendLine($"  Planned: {plan.CombatBoundaryChanges}");
        text.AppendLine($"  Applied: {result.CombatBoundaryChangesApplied}");
        text.AppendLine();
        text.AppendLine("Apply complete.");
        return text.ToString();
    }
}
