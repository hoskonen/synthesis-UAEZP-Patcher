namespace UAEZPSynthesisPatcher;

public static class ExistingEncounterZoneDifficultyResolver
{
    public static byte Resolve(
        byte existingMinimumLevel,
        EncounterZoneDifficultyProfile profile,
        int tierOffset)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (tierOffset == 0)
        {
            return existingMinimumLevel;
        }

        byte[] breakpoints = profile.Tiers
            .Select(tier => tier.MinimumLevel)
            .Distinct()
            .Order()
            .ToArray();
        if (breakpoints.Length == 0)
        {
            throw new InvalidOperationException(
                "Encounter-zone difficulty profile has no tier breakpoints.");
        }

        byte current = existingMinimumLevel;
        int steps = Math.Abs(tierOffset);
        for (int step = 0; step < steps; step++)
        {
            byte? next = tierOffset > 0
                ? breakpoints.Cast<byte?>().FirstOrDefault(level => level > current)
                : breakpoints.Cast<byte?>().LastOrDefault(level => level < current);
            if (next is null)
            {
                break;
            }

            current = next.Value;
        }

        if ((tierOffset > 0 && current < existingMinimumLevel) ||
            (tierOffset < 0 && current > existingMinimumLevel))
        {
            throw new InvalidOperationException(
                "Encounter-zone breakpoint resolution moved in the wrong direction.");
        }

        return current;
    }
}
