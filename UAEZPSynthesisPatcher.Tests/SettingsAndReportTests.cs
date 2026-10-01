using Mutagen.Bethesda.Plugins;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace UAEZPSynthesisPatcher.Tests;

[TestClass]
public sealed class SettingsAndReportTests
{
    [TestMethod]
    public void DefaultSettingsMatchReleaseContract()
    {
        var settings = new Settings();

        Assert.IsTrue(settings.AssignMissingCellEncounterZones);
        Assert.IsTrue(settings.AssignMissingWorldspaceEncounterZones);
        Assert.IsTrue(settings.DisableCombatBoundaries);
        Assert.IsFalse(settings.ForwardEncounterZonesThroughFwmf);
        Assert.AreEqual(
            EncounterZoneDifficultyProfileSelection.MatchValidatedSource,
            settings.DifficultyProfile);
        Assert.AreEqual(DummyZoneMode.DeterministicRandom, settings.DummyZoneMode);
        Assert.AreEqual(38174, settings.Seed);
        Assert.IsFalse(settings.EnableDungeonTierBias);
        Assert.AreEqual(1, settings.CaveTierOffset);
        Assert.AreEqual(1, settings.NordicRuinTierOffset);
        Assert.AreEqual(1, settings.DwemerRuinTierOffset);
        Assert.AreEqual(1, settings.MineTierOffset);
        Assert.AreEqual(0, settings.FortTierOffset);
        Assert.AreEqual(0, settings.OtherDungeonTierOffset);
        Assert.IsTrue(settings.DryRun);
    }

    [TestMethod]
    public void DryRunFalseIsAValidSetting()
    {
        var settings = new Settings { DryRun = false };

        SettingsValidator.Validate(settings);
        Assert.IsFalse(settings.DryRun);
    }

    [TestMethod]
    public void GroupedSettingsRoundTripPreservesEveryValue()
    {
        var settings = new Settings
        {
            EncounterZoneAssignment = new EncounterZoneAssignmentSettings
            {
                AssignMissingCellEncounterZones = false,
                AssignMissingWorldspaceEncounterZones = false,
                DummyZoneMode = DummyZoneMode.First,
                Seed = 12345,
            },
            EncounterZoneDifficulty = new EncounterZoneDifficultySettings
            {
                DifficultyProfile =
                    EncounterZoneDifficultyProfileSelection.UAEZPHard,
            },
            EncounterZoneBehavior = new EncounterZoneBehaviorSettings
            {
                DisableCombatBoundaries = false,
            },
            DungeonDifficulty = new DungeonDifficultySettings
            {
                EnableDungeonTierBias = true,
                CaveTierOffset = 2,
                NordicRuinTierOffset = 3,
                DwemerRuinTierOffset = 4,
                MineTierOffset = 5,
                FortTierOffset = 6,
                OtherDungeonTierOffset = 7,
            },
            Compatibility = new CompatibilitySettings
            {
                ForwardEncounterZonesThroughFwmf = true,
            },
            Testing = new TestingSettings
            {
                DryRun = false,
            },
        };

        var serializerSettings = new JsonSerializerSettings();
        serializerSettings.Converters.Add(new StringEnumConverter());
        string json = JsonConvert.SerializeObject(settings, serializerSettings);
        Settings roundTrip = JsonConvert.DeserializeObject<Settings>(
            json,
            serializerSettings)!;
        JObject root = JObject.Parse(json);

        Assert.IsNotNull(root[nameof(Settings.EncounterZoneAssignment)]);
        Assert.IsNotNull(root[nameof(Settings.EncounterZoneDifficulty)]);
        Assert.IsNotNull(root[nameof(Settings.EncounterZoneBehavior)]);
        Assert.IsNotNull(root[nameof(Settings.DungeonDifficulty)]);
        Assert.IsNotNull(root[nameof(Settings.Compatibility)]);
        Assert.IsNotNull(root[nameof(Settings.Testing)]);
        Assert.AreEqual(6, root.Properties().Count());
        Assert.AreEqual(
            "UAEZPHard",
            root[nameof(Settings.EncounterZoneDifficulty)]![
                nameof(EncounterZoneDifficultySettings.DifficultyProfile)]!
                .Value<string>());
        Assert.IsFalse(
            root[nameof(Settings.Testing)]![nameof(TestingSettings.DryRun)]!
                .Value<bool>());
        foreach (string flatProxyName in FlatSettingProxyNames())
        {
            Assert.IsNull(root[flatProxyName]);
        }
        Assert.IsFalse(roundTrip.AssignMissingCellEncounterZones);
        Assert.IsFalse(roundTrip.AssignMissingWorldspaceEncounterZones);
        Assert.IsFalse(roundTrip.DisableCombatBoundaries);
        Assert.IsTrue(roundTrip.ForwardEncounterZonesThroughFwmf);
        Assert.AreEqual(
            EncounterZoneDifficultyProfileSelection.UAEZPHard,
            roundTrip.DifficultyProfile);
        Assert.AreEqual(DummyZoneMode.First, roundTrip.DummyZoneMode);
        Assert.AreEqual(12345, roundTrip.Seed);
        Assert.IsTrue(roundTrip.EnableDungeonTierBias);
        Assert.AreEqual(2, roundTrip.CaveTierOffset);
        Assert.AreEqual(3, roundTrip.NordicRuinTierOffset);
        Assert.AreEqual(4, roundTrip.DwemerRuinTierOffset);
        Assert.AreEqual(5, roundTrip.MineTierOffset);
        Assert.AreEqual(6, roundTrip.FortTierOffset);
        Assert.AreEqual(7, roundTrip.OtherDungeonTierOffset);
        Assert.IsFalse(roundTrip.DryRun);
    }

    [TestMethod]
    public void LegacyFlatJsonMigratesToGroupedSettings()
    {
        const string json = """
            {
              "AssignMissingCellEncounterZones": false,
              "AssignMissingWorldspaceEncounterZones": false,
              "DisableCombatBoundaries": false,
              "ForwardEncounterZonesThroughFwmf": true,
              "DifficultyProfile": "UAEZPHard",
              "DummyZoneMode": "First",
              "Seed": 12345,
              "DryRun": false
            }
            """;

        Settings settings = JsonConvert.DeserializeObject<Settings>(json)!;

        Assert.IsFalse(settings.AssignMissingCellEncounterZones);
        Assert.IsFalse(settings.AssignMissingWorldspaceEncounterZones);
        Assert.AreEqual(DummyZoneMode.First, settings.DummyZoneMode);
        Assert.AreEqual(12345, settings.Seed);
        Assert.AreEqual(
            EncounterZoneDifficultyProfileSelection.UAEZPHard,
            settings.DifficultyProfile);
        Assert.IsFalse(settings.DisableCombatBoundaries);
        Assert.IsTrue(settings.ForwardEncounterZonesThroughFwmf);
        Assert.IsFalse(settings.DryRun);
    }

    [TestMethod]
    public void RuntimeAliasesExposeValuesSelectedInGroupedSections()
    {
        var settings = new Settings();
        settings.EncounterZoneDifficulty.DifficultyProfile =
            EncounterZoneDifficultyProfileSelection.UAEZPHard;
        settings.Testing.DryRun = false;

        Assert.AreEqual(
            EncounterZoneDifficultyProfileSelection.UAEZPHard,
            settings.DifficultyProfile);
        Assert.IsFalse(settings.DryRun);
    }

    [TestMethod]
    public void CanonicalGroupedValuesWinOverLegacyFlatValues()
    {
        const string json = """
            {
              "DifficultyProfile": "UAEZPEasy",
              "DryRun": true,
              "EncounterZoneDifficulty": {
                "DifficultyProfile": "UAEZPHard"
              },
              "Testing": {
                "DryRun": false
              }
            }
            """;

        Settings settings = JsonConvert.DeserializeObject<Settings>(json)!;

        Assert.AreEqual(
            EncounterZoneDifficultyProfileSelection.UAEZPHard,
            settings.DifficultyProfile);
        Assert.IsFalse(settings.DryRun);
    }

    private static IEnumerable<string> FlatSettingProxyNames()
    {
        yield return nameof(Settings.AssignMissingCellEncounterZones);
        yield return nameof(Settings.AssignMissingWorldspaceEncounterZones);
        yield return nameof(Settings.DummyZoneMode);
        yield return nameof(Settings.Seed);
        yield return nameof(Settings.DifficultyProfile);
        yield return nameof(Settings.EnableDungeonTierBias);
        yield return nameof(Settings.CaveTierOffset);
        yield return nameof(Settings.NordicRuinTierOffset);
        yield return nameof(Settings.DwemerRuinTierOffset);
        yield return nameof(Settings.MineTierOffset);
        yield return nameof(Settings.FortTierOffset);
        yield return nameof(Settings.OtherDungeonTierOffset);
        yield return nameof(Settings.DisableCombatBoundaries);
        yield return nameof(Settings.ForwardEncounterZonesThroughFwmf);
        yield return nameof(Settings.DryRun);
    }

    [TestMethod]
    public void SettingsUiUsesNamedSectionsAndTooltips()
    {
        AssertSettingMetadata<Settings>(
            nameof(Settings.EncounterZoneAssignment),
            "Encounter Zone Assignment");
        AssertSettingMetadata<Settings>(
            nameof(Settings.EncounterZoneBehavior),
            "Encounter Zone Behavior");
        AssertSettingMetadata<Settings>(
            nameof(Settings.EncounterZoneDifficulty),
            "Encounter Zone Difficulty");
        AssertSettingMetadata<Settings>(
            nameof(Settings.DungeonDifficulty),
            "Dungeon Difficulty");
        AssertSettingMetadata<Settings>(
            nameof(Settings.Compatibility),
            "Compatibility");
        AssertSettingMetadata<Settings>(
            nameof(Settings.Testing),
            "Testing");

        AssertSettingMetadata<EncounterZoneAssignmentSettings>(
            nameof(EncounterZoneAssignmentSettings
                .AssignMissingCellEncounterZones),
            "Assign Missing Cell Encounter Zones",
            "Assigns UAEZP dummy encounter zones to CELL records that " +
            "currently have no XEZN.");
        AssertSettingMetadata<EncounterZoneAssignmentSettings>(
            nameof(EncounterZoneAssignmentSettings
                .AssignMissingWorldspaceEncounterZones),
            "Assign Missing Worldspace Encounter Zones",
            "Assigns UAEZP dummy encounter zones to WRLD records that " +
            "currently have no XEZN.");
        AssertSettingMetadata<EncounterZoneAssignmentSettings>(
            nameof(EncounterZoneAssignmentSettings.DummyZoneMode),
            "Dummy Zone Mode",
            "Controls how missing encounter zones are selected.");
        AssertSettingMetadata<EncounterZoneAssignmentSettings>(
            nameof(EncounterZoneAssignmentSettings.Seed),
            "Seed",
            "Controls deterministic random assignment. The same seed and " +
            "load order produce stable results.");
        AssertSettingMetadata<EncounterZoneBehaviorSettings>(
            nameof(EncounterZoneBehaviorSettings.DisableCombatBoundaries),
            "Disable Combat Boundaries",
            "Adds Disable Combat Boundary to encounter-zone records while " +
            "preserving existing flags.");
        AssertSettingMetadata<EncounterZoneDifficultySettings>(
            nameof(EncounterZoneDifficultySettings.DifficultyProfile),
            "Difficulty Profile",
            "Defines the numeric minimum levels of the nine ordered UAEZP " +
            "zone tiers. Match Validated Source preserves the active " +
            "UAEZP.esp values.");
        AssertSettingMetadata<DungeonDifficultySettings>(
            nameof(DungeonDifficultySettings.EnableDungeonTierBias),
            "Enable Dungeon Tier Bias",
            "Raises the selected UAEZP dummy-zone tier for classified " +
            "interior dungeons. Existing XEZN assignments are never shifted.");
        AssertSettingMetadata<DungeonDifficultySettings>(
            nameof(DungeonDifficultySettings.CaveTierOffset),
            "Cave Tier Offset");
        AssertSettingMetadata<DungeonDifficultySettings>(
            nameof(DungeonDifficultySettings.NordicRuinTierOffset),
            "Nordic Ruin Tier Offset");
        AssertSettingMetadata<DungeonDifficultySettings>(
            nameof(DungeonDifficultySettings.DwemerRuinTierOffset),
            "Dwemer Ruin Tier Offset");
        AssertSettingMetadata<DungeonDifficultySettings>(
            nameof(DungeonDifficultySettings.MineTierOffset),
            "Mine Tier Offset");
        AssertSettingMetadata<DungeonDifficultySettings>(
            nameof(DungeonDifficultySettings.FortTierOffset),
            "Fort Tier Offset");
        AssertSettingMetadata<DungeonDifficultySettings>(
            nameof(DungeonDifficultySettings.OtherDungeonTierOffset),
            "Other Dungeon Tier Offset");
        AssertSettingMetadata<CompatibilitySettings>(
            nameof(CompatibilitySettings.ForwardEncounterZonesThroughFwmf),
            "Forward encounter zones through FWMF",
            "Restores the nearest earlier valid XEZN when an FWMF-family " +
            "winning override removes it. Intended primarily for a separate " +
            "late Synthesis group placed after FWMF and its patches.");
        AssertSettingMetadata<TestingSettings>(
            nameof(TestingSettings.DryRun),
            "Dry Run",
            "Plans and reports changes without writing functional Skyrim " +
            "overrides.");

        string[] aliases =
        [
            nameof(Settings.AssignMissingCellEncounterZones),
            nameof(Settings.AssignMissingWorldspaceEncounterZones),
            nameof(Settings.DisableCombatBoundaries),
            nameof(Settings.ForwardEncounterZonesThroughFwmf),
            nameof(Settings.DifficultyProfile),
            nameof(Settings.DummyZoneMode),
            nameof(Settings.Seed),
            nameof(Settings.EnableDungeonTierBias),
            nameof(Settings.CaveTierOffset),
            nameof(Settings.NordicRuinTierOffset),
            nameof(Settings.DwemerRuinTierOffset),
            nameof(Settings.MineTierOffset),
            nameof(Settings.FortTierOffset),
            nameof(Settings.OtherDungeonTierOffset),
            nameof(Settings.DryRun),
        ];
        foreach (string alias in aliases)
        {
            Assert.IsTrue(typeof(Settings).GetProperty(alias)!.CustomAttributes
                .Any(attribute =>
                    attribute.AttributeType.Name == "SynthesisIgnoreSetting"));
        }
    }

    [TestMethod]
    public void ReportStatesReadOnlyResultAndIncludesConsistentTotals()
    {
        var settings = new Settings();
        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate([TestData.EasySource()]);
        PatchPlan plan = ReadOnlyPlanner.Build(
            settings,
            source.DummyZones,
            [TestData.Record(PlannedRecordType.Cell, 1)],
            [TestData.Record(PlannedRecordType.Worldspace, 2)],
            [TestData.Record(PlannedRecordType.EncounterZone, 3)]);

        string report = DryRunReport.Render(new PlanningRun(source, plan), settings);

        StringAssert.Contains(report, "Source variant: Easy");
        StringAssert.Contains(report, "Difficulty profile: UAEZP Easy");
        StringAssert.Contains(report, "Algorithm version: v1");
        StringAssert.Contains(report, "Total: 3");
        StringAssert.Contains(report, "No Skyrim records were modified");
        Assert.AreEqual(2, plan.AssignmentDistribution.Values.Sum());
    }

    [TestMethod]
    public void ReportsShowSeparateFwmfForwardCounts()
    {
        ModKey fwmf = ModKey.FromFileName("FWMF.esp");
        FormKey target = new(TestData.SkyrimModKey, 0x1234);
        RecordSnapshot cell = TestData.Record(
            PlannedRecordType.Cell,
            1,
            winningModKey: fwmf) with
        {
            EarlierResolvableEncounterZoneTarget = target,
        };
        var settings = new Settings
        {
            ForwardEncounterZonesThroughFwmf = true,
        };
        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate([TestData.EasySource()]);
        PatchPlan plan = ReadOnlyPlanner.Build(
            settings,
            source.DummyZones,
            [cell],
            [],
            []);

        string dryRun = DryRunReport.Render(
            new PlanningRun(source, plan),
            settings);
        string apply = ApplyReport.Render(
            plan,
            new ApplyResult(1, 0, 0)
            {
                CellsForwardedThroughFwmf = 1,
            });

        StringAssert.Contains(dryRun, "FWMF XEZN forwards:");
        StringAssert.Contains(dryRun, "  CELL: 1");
        StringAssert.Contains(apply, "Planned FWMF XEZN forwards:");
        StringAssert.Contains(
            apply,
            "Applied/verified FWMF XEZN forwards:");
    }

    private static void AssertSettingMetadata<T>(
        string propertyName,
        string expectedName,
        string? expectedTooltip = null)
    {
        var attributes = typeof(T).GetProperty(propertyName)!.CustomAttributes;
        var name = attributes.Single(attribute =>
            attribute.AttributeType.Name == "SynthesisSettingName");
        Assert.AreEqual(
            expectedName,
            name.ConstructorArguments.Single().Value);

        if (expectedTooltip is null)
        {
            return;
        }

        var tooltip = attributes.Single(attribute =>
            attribute.AttributeType.Name == "SynthesisTooltip");
        Assert.AreEqual(
            expectedTooltip,
            tooltip.ConstructorArguments.Single().Value);
    }
}
