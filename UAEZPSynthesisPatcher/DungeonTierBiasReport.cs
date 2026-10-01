using System.Text;

namespace UAEZPSynthesisPatcher;

public static class DungeonTierBiasReport
{
    public static void Append(
        StringBuilder text,
        DungeonTierBiasSummary summary,
        bool? enabled = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(summary);

        text.AppendLine("Dungeon tier bias:");
        if (enabled is not null)
        {
            text.AppendLine(
                $"  Status: {(enabled.Value ? "Enabled" : "Disabled")}");
        }

        foreach (DungeonCategory category in Enum.GetValues<DungeonCategory>()
                     .Where(category => category != DungeonCategory.None))
        {
            summary.AssignmentsByCategory.TryGetValue(category, out int count);
            text.AppendLine($"  {FormatCategory(category)}: {count}");
        }

        text.AppendLine();
        text.AppendLine("Tier shifts:");
        if (summary.TierShifts.Count == 0)
        {
            text.AppendLine("  (none)");
        }
        else
        {
            foreach ((int modifier, int count) in summary.TierShifts
                         .OrderBy(pair => pair.Key))
            {
                text.AppendLine($"  {modifier:+#;-#;0}: {count}");
            }
        }
        text.AppendLine(
            $"  Clamped at tier {EncounterZoneDifficultyResolver.TierCount - 1}: " +
            $"{summary.ClampedAtMaximum}");
        text.AppendLine();
    }

    private static string FormatCategory(DungeonCategory category)
    {
        return category switch
        {
            DungeonCategory.Cave => "Cave",
            DungeonCategory.NordicRuin => "Nordic Ruin",
            DungeonCategory.DwemerRuin => "Dwemer Ruin",
            DungeonCategory.Mine => "Mine",
            DungeonCategory.Fort => "Fort",
            DungeonCategory.OtherDungeon => "Other Dungeon",
            _ => category.ToString(),
        };
    }
}
