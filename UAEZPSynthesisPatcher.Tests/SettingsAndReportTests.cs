using Mutagen.Bethesda.Plugins;
using Newtonsoft.Json;

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
        Assert.AreEqual(DummyZoneMode.DeterministicRandom, settings.DummyZoneMode);
        Assert.AreEqual(38174, settings.Seed);
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
    public void UiSectionsPreserveFlatSettingsJsonContract()
    {
        var settings = new Settings
        {
            AssignMissingCellEncounterZones = false,
            AssignMissingWorldspaceEncounterZones = false,
            DisableCombatBoundaries = false,
            ForwardEncounterZonesThroughFwmf = true,
            DummyZoneMode = DummyZoneMode.First,
            Seed = 42,
            DryRun = false,
        };

        string json = JsonConvert.SerializeObject(settings);
        Settings roundTrip = JsonConvert.DeserializeObject<Settings>(json)!;

        Assert.IsFalse(json.Contains(
            "EncounterZoneAssignment",
            StringComparison.Ordinal));
        Assert.IsFalse(json.Contains(
            "EncounterZoneBehavior",
            StringComparison.Ordinal));
        Assert.IsFalse(json.Contains(
            "Compatibility",
            StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("Testing", StringComparison.Ordinal));
        StringAssert.Contains(
            json,
            "\"ForwardEncounterZonesThroughFwmf\":true");
        Assert.IsFalse(roundTrip.AssignMissingCellEncounterZones);
        Assert.IsFalse(roundTrip.AssignMissingWorldspaceEncounterZones);
        Assert.IsFalse(roundTrip.DisableCombatBoundaries);
        Assert.IsTrue(roundTrip.ForwardEncounterZonesThroughFwmf);
        Assert.AreEqual(DummyZoneMode.First, roundTrip.DummyZoneMode);
        Assert.AreEqual(42, roundTrip.Seed);
        Assert.IsFalse(roundTrip.DryRun);
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
            nameof(Settings.DummyZoneMode),
            nameof(Settings.Seed),
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
