namespace UAEZPSynthesisPatcher.Tests;

[TestClass]
public sealed class SettingsAndReportTests
{
    [TestMethod]
    public void DefaultSettingsMatchMilestoneContract()
    {
        var settings = new Settings();

        Assert.IsTrue(settings.AssignMissingCellEncounterZones);
        Assert.IsTrue(settings.AssignMissingWorldspaceEncounterZones);
        Assert.IsTrue(settings.DisableCombatBoundaries);
        Assert.AreEqual(DummyZoneMode.DeterministicRandom, settings.DummyZoneMode);
        Assert.AreEqual(38174, settings.Seed);
        Assert.IsTrue(settings.DryRun);
    }

    [TestMethod]
    public void DryRunFalseRefusesMutationMilestone()
    {
        var settings = new Settings { DryRun = false };

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => MilestoneGuard.EnsureReadOnly(settings));

        StringAssert.Contains(error.Message, "DryRun=false is not supported");
        StringAssert.Contains(error.Message, "No records were modified");
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
}
