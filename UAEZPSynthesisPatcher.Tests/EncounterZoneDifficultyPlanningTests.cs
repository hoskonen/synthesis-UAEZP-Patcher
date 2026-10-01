using Mutagen.Bethesda.Plugins;

namespace UAEZPSynthesisPatcher.Tests;

[TestClass]
public sealed class EncounterZoneDifficultyPlanningTests
{
    [TestMethod]
    public void EasySourceMatchValidatedSourcePlansNoDifficultyChanges()
    {
        AssertMatchSourcePlansNoDifficultyChanges(TestData.EasySource());
    }

    [TestMethod]
    public void HardSourceMatchValidatedSourcePlansNoDifficultyChanges()
    {
        AssertMatchSourcePlansNoDifficultyChanges(TestData.HardSource());
    }

    [TestMethod]
    public void EasySourceHardProfilePlansAllNineDesiredMinimumLevels()
    {
        AssertCrossProfile(
            TestData.EasySource(),
            EncounterZoneDifficultyProfileSelection.UAEZPHard,
            [10, 15, 20, 25, 30, 35, 40, 45, 50]);
    }

    [TestMethod]
    public void HardSourceEasyProfilePlansAllNineDesiredMinimumLevels()
    {
        AssertCrossProfile(
            TestData.HardSource(),
            EncounterZoneDifficultyProfileSelection.UAEZPEasy,
            [3, 5, 7, 9, 11, 11, 13, 15, 17]);
    }

    [TestMethod]
    public void AlreadyMatchingTierDoesNotPlanUnnecessaryMutation()
    {
        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate([TestData.HardSource()]);
        Settings settings = DifficultyOnly(
            EncounterZoneDifficultyProfileSelection.UAEZPHard);

        PatchPlan plan = ReadOnlyPlanner.Build(
            settings,
            source.DummyZones,
            [],
            [],
            Snapshots(source.DummyZones),
            EncounterZoneDifficultyResolver.Hard);

        Assert.AreEqual(0, plan.EncounterZoneDifficultyChanges);
        Assert.AreEqual(0, plan.EncounterZones.PlannedOverrides);
        Assert.AreEqual(0, plan.Changes.Count);
    }

    [TestMethod]
    public void DifficultyProfileDoesNotChangeDeterministicAssignments()
    {
        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate([TestData.EasySource()]);
        RecordSnapshot cell = TestData.Record(PlannedRecordType.Cell, 0x123456);
        Settings matchSettings = DifficultyOnly(
            EncounterZoneDifficultyProfileSelection.MatchValidatedSource);
        matchSettings.AssignMissingCellEncounterZones = true;
        Settings hardSettings = DifficultyOnly(
            EncounterZoneDifficultyProfileSelection.UAEZPHard);
        hardSettings.AssignMissingCellEncounterZones = true;
        PatchPlan matchPlan = ReadOnlyPlanner.Build(
            matchSettings,
            source.DummyZones,
            [cell],
            [],
            []);
        PatchPlan hardPlan = ReadOnlyPlanner.Build(
            hardSettings,
            source.DummyZones,
            [cell],
            [],
            [],
            EncounterZoneDifficultyResolver.Hard);

        Assert.AreEqual(
            matchPlan.Changes.Single().AssignedDummyZone!.FormKey,
            hardPlan.Changes.Single().AssignedDummyZone!.FormKey);
    }

    [TestMethod]
    public void DryRunReportsDifficultyProfileAndPlannedCount()
    {
        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate([TestData.EasySource()]);
        Settings settings = DifficultyOnly(
            EncounterZoneDifficultyProfileSelection.UAEZPHard);
        settings.DryRun = true;
        PatchPlan plan = ReadOnlyPlanner.Build(
            settings,
            source.DummyZones,
            [],
            [],
            Snapshots(source.DummyZones),
            EncounterZoneDifficultyResolver.Hard);
        var run = new PlanningRun(source, plan)
        {
            DifficultyProfile = EncounterZoneDifficultyResolver.Hard,
        };

        string report = DryRunReport.Render(run, settings);

        StringAssert.Contains(report, "Difficulty profile: UAEZP Hard");
        StringAssert.Contains(report, "ECZN difficulty changes:");
        StringAssert.Contains(report, "  Planned: 9");
    }

    private static void AssertMatchSourcePlansNoDifficultyChanges(
        SourcePluginCandidate candidate)
    {
        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate([candidate]);
        Settings settings = DifficultyOnly(
            EncounterZoneDifficultyProfileSelection.MatchValidatedSource);
        List<RecordSnapshot> winningSnapshots =
            Snapshots(source.DummyZones).ToList();
        winningSnapshots[0] = winningSnapshots[0] with
        {
            EncounterZoneMinimumLevel = 99,
        };

        PatchPlan plan = ReadOnlyPlanner.Build(
            settings,
            source.DummyZones,
            [],
            [],
            winningSnapshots);

        Assert.AreEqual(0, plan.EncounterZoneDifficultyChanges);
        Assert.AreEqual(0, plan.EncounterZones.PlannedOverrides);
    }

    private static void AssertCrossProfile(
        SourcePluginCandidate candidate,
        EncounterZoneDifficultyProfileSelection selection,
        byte[] expectedLevels)
    {
        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate([candidate]);
        Settings settings = DifficultyOnly(selection);
        EncounterZoneDifficultyProfile profile =
            EncounterZoneDifficultyResolver.Resolve(selection, source);

        PatchPlan plan = ReadOnlyPlanner.Build(
            settings,
            source.DummyZones,
            [],
            [],
            Snapshots(source.DummyZones),
            profile);

        Assert.AreEqual(9, plan.EncounterZoneDifficultyChanges);
        Assert.AreEqual(9, plan.EncounterZones.PlannedOverrides);
        CollectionAssert.AreEqual(
            expectedLevels,
            plan.Changes
                .OrderBy(change => change.Target.FormKey.ID)
                .Select(change =>
                    change.DesiredEncounterZoneMinimumLevel!.Value)
                .ToArray());
    }

    private static IReadOnlyList<RecordSnapshot> Snapshots(
        IReadOnlyList<ValidatedDummyZone> zones)
    {
        return zones.Select(zone => new RecordSnapshot(
            PlannedRecordType.EncounterZone,
            zone.FormKey,
            zone.EditorId,
            zone.FormKey.ModKey,
            false,
            zone.Flags.HasFlag(
                Mutagen.Bethesda.Skyrim.EncounterZone.Flag
                    .DisableCombatBoundary))
        {
            EncounterZoneMinimumLevel = zone.MinLevel,
        }).ToList();
    }

    private static Settings DifficultyOnly(
        EncounterZoneDifficultyProfileSelection selection)
    {
        return new Settings
        {
            DifficultyProfile = selection,
            AssignMissingCellEncounterZones = false,
            AssignMissingWorldspaceEncounterZones = false,
            DisableCombatBoundaries = false,
            DryRun = false,
        };
    }
}
