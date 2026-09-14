using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace UAEZPSynthesisPatcher.Tests;

internal static class TestData
{
    public static readonly ModKey SourceModKey =
        ModKey.FromFileName("UAEZP.esp");

    public static readonly ModKey SkyrimModKey =
        ModKey.FromFileName("Skyrim.esm");

    public static SourcePluginCandidate EasySource()
    {
        return Source([3, 5, 7, 9, 11, 11, 13, 15, 17]);
    }

    public static SourcePluginCandidate HardSource()
    {
        return Source([10, 15, 20, 25, 30, 35, 40, 45, 50]);
    }

    public static IReadOnlyList<ValidatedDummyZone> ValidatedZones()
    {
        return SourcePluginValidator.Validate([EasySource()]).DummyZones;
    }

    public static RecordSnapshot Record(
        PlannedRecordType type,
        uint id,
        bool satisfied = false,
        bool deleted = false,
        ModKey? modKey = null,
        ModKey? winningModKey = null)
    {
        ModKey key = modKey ?? SkyrimModKey;
        return new RecordSnapshot(
            type,
            new FormKey(key, id),
            $"Record{id:X6}",
            winningModKey ?? key,
            deleted,
            satisfied);
    }

    private static SourcePluginCandidate Source(IReadOnlyList<byte> minimumLevels)
    {
        EncounterZone.Flag flags =
            EncounterZone.Flag.NeverResets |
            EncounterZone.Flag.DisableCombatBoundary;

        IReadOnlyList<DummyZoneDefinition> zones = Enumerable.Range(0, 9)
            .Select(index => new DummyZoneDefinition(
                new FormKey(SourceModKey, 0x800u + (uint)index),
                $"DummyEncounterZone{index}",
                minimumLevels[index],
                0,
                0,
                FormKey.Null,
                FormKey.Null,
                flags,
                false))
            .ToList();

        return new SourcePluginCandidate(SourceModKey, zones);
    }
}
