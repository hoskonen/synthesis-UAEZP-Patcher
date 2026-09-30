using Mutagen.Bethesda.Plugins;

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
    public void FwmfSettingUsesFriendlyUiName()
    {
        var property = typeof(Settings).GetProperty(
            nameof(Settings.ForwardEncounterZonesThroughFwmf));
        var attribute = property!.CustomAttributes.Single(candidate =>
            candidate.AttributeType.Name == "SynthesisSettingName");

        Assert.AreEqual(
            "Forward encounter zones through FWMF",
            attribute.ConstructorArguments.Single().Value);
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
}
