namespace UAEZPSynthesisPatcher;

public sealed record DungeonTierSelection(
    int BaseTier,
    int Modifier,
    int FinalTier,
    bool ClampedAtMaximum);

public static class DungeonTierBias
{
    public static DungeonTierSelection Apply(
        DungeonDifficultySettings settings,
        DungeonCategory category,
        int baseTier,
        int tierCount)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (tierCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tierCount));
        }
        if (baseTier < 0 || baseTier >= tierCount)
        {
            throw new ArgumentOutOfRangeException(nameof(baseTier));
        }

        int modifier = settings.EnableDungeonTierBias
            ? GetOffset(settings, category)
            : 0;
        int unboundedTier = baseTier + modifier;
        int finalTier = Math.Clamp(unboundedTier, 0, tierCount - 1);
        return new DungeonTierSelection(
            baseTier,
            modifier,
            finalTier,
            unboundedTier > tierCount - 1);
    }

    public static int GetOffset(
        DungeonDifficultySettings settings,
        DungeonCategory category)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return category switch
        {
            DungeonCategory.None => 0,
            DungeonCategory.Cave => settings.CaveTierOffset,
            DungeonCategory.NordicRuin => settings.NordicRuinTierOffset,
            DungeonCategory.DwemerRuin => settings.DwemerRuinTierOffset,
            DungeonCategory.Mine => settings.MineTierOffset,
            DungeonCategory.Fort => settings.FortTierOffset,
            DungeonCategory.OtherDungeon => settings.OtherDungeonTierOffset,
            _ => throw new InvalidOperationException(
                $"Unsupported dungeon category: {category}."),
        };
    }
}
