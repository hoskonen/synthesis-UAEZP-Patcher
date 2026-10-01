namespace UAEZPSynthesisPatcher;

public enum EncounterZoneDifficultyProfileSelection
{
    MatchValidatedSource,
    UAEZPEasy,
    UAEZPHard,
    Custom,
}

public sealed record EncounterZoneDifficultyTier(
    int Index,
    byte MinimumLevel);

public sealed class EncounterZoneDifficultyProfile
{
    internal EncounterZoneDifficultyProfile(
        string displayName,
        string sourceVariant,
        IReadOnlyList<EncounterZoneDifficultyTier> tiers)
    {
        DisplayName = displayName;
        SourceVariant = sourceVariant;
        Tiers = tiers;
    }

    public string DisplayName { get; }
    public string SourceVariant { get; }
    public IReadOnlyList<EncounterZoneDifficultyTier> Tiers { get; }
}

public static class EncounterZoneDifficultyResolver
{
    public const int TierCount = 9;

    public static EncounterZoneDifficultyProfile Easy { get; } = Create(
        "UAEZP Easy",
        "Easy",
        [3, 5, 7, 9, 11, 11, 13, 15, 17]);

    public static EncounterZoneDifficultyProfile Hard { get; } = Create(
        "UAEZP Hard",
        "Hard",
        [10, 15, 20, 25, 30, 35, 40, 45, 50]);

    public static EncounterZoneDifficultyProfile Resolve(
        EncounterZoneDifficultyProfileSelection selection,
        ValidatedSourcePlugin source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Resolve(
            selection,
            source.DummyZones.Select(zone => zone.MinLevel));
    }

    public static EncounterZoneDifficultyProfile Resolve(
        EncounterZoneDifficultyProfileSelection selection,
        IEnumerable<byte> sourceMinimumLevels)
    {
        ArgumentNullException.ThrowIfNull(sourceMinimumLevels);

        EncounterZoneDifficultyProfile sourceProfile = MatchValidatedSource(
            sourceMinimumLevels);
        EncounterZoneDifficultyProfile selected = selection switch
        {
            EncounterZoneDifficultyProfileSelection.MatchValidatedSource =>
                sourceProfile,
            EncounterZoneDifficultyProfileSelection.UAEZPEasy => Easy,
            EncounterZoneDifficultyProfileSelection.UAEZPHard => Hard,
            EncounterZoneDifficultyProfileSelection.Custom =>
                throw new InvalidOperationException(
                    "Custom difficulty profile data is not configured. " +
                    "Use Match Validated Source, UAEZP Easy, or UAEZP Hard."),
            _ => throw new InvalidOperationException(
                $"Unsupported difficulty profile value: {(int)selection}."),
        };

        return selected;
    }

    public static EncounterZoneDifficultyProfile MatchValidatedSource(
        IEnumerable<byte> minimumLevels)
    {
        ArgumentNullException.ThrowIfNull(minimumLevels);
        byte[] levels = minimumLevels.ToArray();

        if (levels.SequenceEqual(
                Easy.Tiers.Select(tier => tier.MinimumLevel)))
        {
            return Easy;
        }

        if (levels.SequenceEqual(
                Hard.Tiers.Select(tier => tier.MinimumLevel)))
        {
            return Hard;
        }

        throw new InvalidOperationException(
            "Dummy-zone minimum levels do not match a predefined difficulty " +
            "profile. Found: " + string.Join(", ", levels));
    }

    public static EncounterZoneDifficultyProfile CreateCustom(
        IEnumerable<int> minimumLevels)
    {
        ArgumentNullException.ThrowIfNull(minimumLevels);
        return Create("Custom", "Custom", minimumLevels.ToArray());
    }

    private static EncounterZoneDifficultyProfile Create(
        string displayName,
        string sourceVariant,
        IReadOnlyList<int> minimumLevels)
    {
        if (minimumLevels.Count != TierCount)
        {
            throw new InvalidOperationException(
                $"Difficulty profile '{displayName}' must define exactly " +
                $"{TierCount} ordered tiers; found {minimumLevels.Count}.");
        }

        for (int index = 0; index < minimumLevels.Count; index++)
        {
            int level = minimumLevels[index];
            if (level is < 1 or > byte.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Difficulty profile '{displayName}' tier {index} has " +
                    $"invalid minimum level {level}; expected 1-255.");
            }

            if (index > 0 && level < minimumLevels[index - 1])
            {
                throw new InvalidOperationException(
                    $"Difficulty profile '{displayName}' tiers must be in " +
                    "nondecreasing minimum-level order.");
            }
        }

        IReadOnlyList<EncounterZoneDifficultyTier> tiers = minimumLevels
            .Select((level, index) => new EncounterZoneDifficultyTier(
                index,
                checked((byte)level)))
            .ToList();
        return new EncounterZoneDifficultyProfile(
            displayName,
            sourceVariant,
            tiers);
    }
}
