using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace UAEZPSynthesisPatcher;

public static class SourcePluginValidator
{
    public const string SupportedPluginFileName = "UAEZP.esp";
    public const int ExpectedDummyZoneCount = 9;
    public const string DummyEditorIdPrefix = "DummyEncounterZone";

    private static readonly byte[] EasyMinimumLevels =
        [3, 5, 7, 9, 11, 11, 13, 15, 17];

    private static readonly byte[] HardMinimumLevels =
        [10, 15, 20, 25, 30, 35, 40, 45, 50];

    private const EncounterZone.Flag ExpectedFlags =
        EncounterZone.Flag.NeverResets |
        EncounterZone.Flag.DisableCombatBoundary;

    public static ValidatedSourcePlugin Validate(
        IEnumerable<SourcePluginCandidate> activeCandidates)
    {
        ArgumentNullException.ThrowIfNull(activeCandidates);

        List<SourcePluginCandidate> matches = activeCandidates
            .Where(candidate => string.Equals(
                candidate.ModKey.FileName.String,
                SupportedPluginFileName,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"Required source plugin '{SupportedPluginFileName}' is not active. " +
                "Install and enable exactly one supported UAEZP Easy or Hard variant.");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Found {matches.Count} active '{SupportedPluginFileName}' listings. " +
                "The Easy and Hard variants are mutually exclusive; enable exactly one.");
        }

        SourcePluginCandidate source = matches[0];
        IReadOnlyList<DummyZoneDefinition> zones = source.DummyZones;

        if (zones.Count != ExpectedDummyZoneCount)
        {
            throw new InvalidOperationException(
                $"'{SupportedPluginFileName}' has {zones.Count} records whose " +
                $"EditorID starts with '{DummyEditorIdPrefix}', but the supported " +
                $"Easy/Hard schema requires exactly {ExpectedDummyZoneCount}.");
        }

        if (zones.Select(zone => zone.EditorId)
            .Distinct(StringComparer.Ordinal).Count() != zones.Count)
        {
            throw new InvalidOperationException(
                $"'{SupportedPluginFileName}' contains duplicate dummy-zone EditorIDs.");
        }

        if (zones.Select(zone => zone.FormKey).Distinct().Count() != zones.Count)
        {
            throw new InvalidOperationException(
                $"'{SupportedPluginFileName}' contains duplicate dummy-zone FormKeys.");
        }

        List<DummyZoneDefinition> ordered = zones
            .OrderBy(zone => zone.FormKey.ID)
            .ToList();

        for (int index = 0; index < ordered.Count; index++)
        {
            DummyZoneDefinition zone = ordered[index];
            uint expectedId = 0x800u + (uint)index;
            string expectedEditorId = $"{DummyEditorIdPrefix}{index}";

            if (zone.FormKey.ModKey != source.ModKey ||
                zone.FormKey.ID != expectedId ||
                !string.Equals(zone.EditorId, expectedEditorId, StringComparison.Ordinal) ||
                zone.IsDeleted ||
                zone.MaxLevel != 0 ||
                zone.Rank != 0 ||
                !zone.Owner.IsNull ||
                !zone.Location.IsNull ||
                zone.Flags != ExpectedFlags)
            {
                throw new InvalidOperationException(
                    $"Dummy-zone schema mismatch at stable index {index}. " +
                    $"Expected {expectedEditorId} at local FormID {expectedId:X6}, " +
                    "non-deleted, MaxLevel=0, Rank=0, null owner/location, and " +
                    $"flags '{ExpectedFlags}'; found {Describe(zone)}.");
            }
        }

        byte[] minimumLevels = ordered.Select(zone => zone.MinLevel).ToArray();
        string variant = minimumLevels.SequenceEqual(EasyMinimumLevels)
            ? "Easy"
            : minimumLevels.SequenceEqual(HardMinimumLevels)
                ? "Hard"
                : throw new InvalidOperationException(
                    $"'{SupportedPluginFileName}' dummy-zone minimum levels do not " +
                    "match the audited Easy or Hard schema. Found: " +
                    string.Join(", ", minimumLevels));

        IReadOnlyList<ValidatedDummyZone> validated = ordered
            .Select(zone => new ValidatedDummyZone(
                zone.FormKey,
                zone.EditorId,
                zone.MinLevel,
                zone.MaxLevel,
                zone.Flags))
            .ToList();

        return new ValidatedSourcePlugin(source.ModKey, variant, validated);
    }

    private static string Describe(DummyZoneDefinition zone)
    {
        return $"{zone.EditorId} at {zone.FormKey}, Deleted={zone.IsDeleted}, " +
               $"MinLevel={zone.MinLevel}, MaxLevel={zone.MaxLevel}, " +
               $"Rank={zone.Rank}, Owner={zone.Owner}, Location={zone.Location}, " +
               $"Flags={zone.Flags}";
    }
}
