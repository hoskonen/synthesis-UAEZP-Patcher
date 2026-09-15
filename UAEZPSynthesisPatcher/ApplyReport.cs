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
        text.AppendLine("Planned overrides:");
        text.AppendLine($"  CELL: {plan.Cells.PlannedOverrides}");
        text.AppendLine($"  WRLD: {plan.Worldspaces.PlannedOverrides}");
        text.AppendLine($"  ECZN: {plan.EncounterZones.PlannedOverrides}");
        text.AppendLine($"  Total: {plan.TotalPlannedOverrides}");
        text.AppendLine();
        text.AppendLine("Applied/verified overrides:");
        text.AppendLine($"  CELL: {result.CellsApplied}");
        text.AppendLine($"  WRLD: {result.WorldspacesApplied}");
        text.AppendLine($"  ECZN: {result.EncounterZonesApplied}");
        text.AppendLine($"  Total: {result.TotalApplied}");
        text.AppendLine();
        text.AppendLine("Apply complete.");
        return text.ToString();
    }
}
