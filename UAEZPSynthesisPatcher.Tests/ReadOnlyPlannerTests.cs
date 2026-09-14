using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace UAEZPSynthesisPatcher.Tests;

[TestClass]
public sealed class ReadOnlyPlannerTests
{
    [TestMethod]
    public void CellWithExistingEncounterZoneHasNoPlannedChange()
    {
        PatchPlan plan = Build(
            cells: [TestData.Record(PlannedRecordType.Cell, 1, satisfied: true)]);

        Assert.AreEqual(1, plan.Cells.ExistingRequirement);
        Assert.AreEqual(0, plan.Cells.PlannedOverrides);
    }

    [TestMethod]
    public void CellWithoutEncounterZoneGetsPlannedAssignment()
    {
        PatchPlan plan = Build(
            cells: [TestData.Record(PlannedRecordType.Cell, 1)]);

        Assert.AreEqual(1, plan.Cells.MissingRequirement);
        Assert.AreEqual(1, plan.Cells.PlannedOverrides);
        Assert.IsNotNull(plan.Changes.Single().AssignedDummyZone);
    }

    [TestMethod]
    public void UnresolvedNonNullCellLinkIsPreservedAndReported()
    {
        RecordSnapshot unresolved = TestData.Record(
            PlannedRecordType.Cell,
            1,
            satisfied: true) with
        {
            HasUnresolvedRequirementReference = true,
        };

        PatchPlan plan = Build(cells: [unresolved]);

        Assert.AreEqual(1, plan.Cells.ExistingRequirement);
        Assert.AreEqual(1, plan.Cells.UnresolvedExistingReferences);
        Assert.AreEqual(0, plan.Cells.PlannedOverrides);
    }

    [TestMethod]
    public void WorldspaceWithExistingEncounterZoneHasNoPlannedChange()
    {
        PatchPlan plan = Build(
            worldspaces:
            [TestData.Record(PlannedRecordType.Worldspace, 1, satisfied: true)]);

        Assert.AreEqual(1, plan.Worldspaces.ExistingRequirement);
        Assert.AreEqual(0, plan.Worldspaces.PlannedOverrides);
    }

    [TestMethod]
    public void WorldspaceWithoutEncounterZoneGetsPlannedAssignment()
    {
        PatchPlan plan = Build(
            worldspaces: [TestData.Record(PlannedRecordType.Worldspace, 1)]);

        Assert.AreEqual(1, plan.Worldspaces.MissingRequirement);
        Assert.AreEqual(1, plan.Worldspaces.PlannedOverrides);
        Assert.IsNotNull(plan.Changes.Single().AssignedDummyZone);
    }

    [TestMethod]
    public void EncounterZoneWithCombatBoundaryFlagHasNoPlannedChange()
    {
        PatchPlan plan = Build(
            encounterZones:
            [TestData.Record(PlannedRecordType.EncounterZone, 1, satisfied: true)]);

        Assert.AreEqual(1, plan.EncounterZones.ExistingRequirement);
        Assert.AreEqual(0, plan.EncounterZones.PlannedOverrides);
    }

    [TestMethod]
    public void EncounterZoneWithoutCombatBoundaryFlagGetsPlannedChange()
    {
        PatchPlan plan = Build(
            encounterZones: [TestData.Record(PlannedRecordType.EncounterZone, 1)]);

        Assert.AreEqual(1, plan.EncounterZones.MissingRequirement);
        Assert.AreEqual(1, plan.EncounterZones.PlannedOverrides);
        Assert.IsNull(plan.Changes.Single().AssignedDummyZone);
    }

    [TestMethod]
    public void DeletedWinningRecordsAreCountedAndSkipped()
    {
        PatchPlan plan = Build(
            cells: [TestData.Record(PlannedRecordType.Cell, 1, deleted: true)],
            worldspaces:
            [TestData.Record(PlannedRecordType.Worldspace, 2, deleted: true)],
            encounterZones:
            [TestData.Record(PlannedRecordType.EncounterZone, 3, deleted: true)]);

        Assert.AreEqual(1, plan.Cells.DeletedSkipped);
        Assert.AreEqual(1, plan.Worldspaces.DeletedSkipped);
        Assert.AreEqual(1, plan.EncounterZones.DeletedSkipped);
        Assert.AreEqual(0, plan.TotalPlannedOverrides);
    }

    [TestMethod]
    public void DistributionAndPlannedCountsAreConsistent()
    {
        PatchPlan plan = Build(
            cells:
            [
                TestData.Record(PlannedRecordType.Cell, 1),
                TestData.Record(PlannedRecordType.Cell, 2),
                TestData.Record(PlannedRecordType.Cell, 3, satisfied: true),
            ],
            worldspaces:
            [
                TestData.Record(PlannedRecordType.Worldspace, 4),
                TestData.Record(PlannedRecordType.Worldspace, 5),
            ],
            encounterZones:
            [
                TestData.Record(PlannedRecordType.EncounterZone, 6),
                TestData.Record(
                    PlannedRecordType.EncounterZone,
                    7,
                    satisfied: true),
            ]);

        Assert.AreEqual(5, plan.TotalPlannedOverrides);
        Assert.AreEqual(4, plan.TotalDummyAssignments);
        Assert.AreEqual(4, plan.AssignmentDistribution.Values.Sum());
        Assert.AreEqual(plan.TotalPlannedOverrides, plan.Changes.Count);
    }

    [TestMethod]
    public void DisabledFeaturesStillScanButDoNotPlan()
    {
        var settings = new Settings
        {
            AssignMissingCellEncounterZones = false,
            AssignMissingWorldspaceEncounterZones = false,
            DisableCombatBoundaries = false,
        };

        PatchPlan plan = Build(
            settings,
            cells: [TestData.Record(PlannedRecordType.Cell, 1)],
            worldspaces: [TestData.Record(PlannedRecordType.Worldspace, 2)],
            encounterZones: [TestData.Record(PlannedRecordType.EncounterZone, 3)]);

        Assert.AreEqual(1, plan.Cells.MissingRequirement);
        Assert.AreEqual(1, plan.Worldspaces.MissingRequirement);
        Assert.AreEqual(1, plan.EncounterZones.MissingRequirement);
        Assert.AreEqual(0, plan.TotalPlannedOverrides);
    }

    [TestMethod]
    public void DryRunPlanningDoesNotModifyAnOutputMod()
    {
        var output = new SkyrimMod(
            ModKey.FromFileName("UAEZPSynthesisPatcher.esp"),
            SkyrimRelease.SkyrimSE);

        _ = Build(
            cells: [TestData.Record(PlannedRecordType.Cell, 1)],
            worldspaces: [TestData.Record(PlannedRecordType.Worldspace, 2)],
            encounterZones: [TestData.Record(PlannedRecordType.EncounterZone, 3)]);

        Assert.AreEqual(0, output.Cells.Count);
        Assert.AreEqual(0, output.Worldspaces.Count);
        Assert.AreEqual(0, output.EncounterZones.Count);
    }

    private static PatchPlan Build(
        IEnumerable<RecordSnapshot>? cells = null,
        IEnumerable<RecordSnapshot>? worldspaces = null,
        IEnumerable<RecordSnapshot>? encounterZones = null)
    {
        return Build(
            new Settings(),
            cells,
            worldspaces,
            encounterZones);
    }

    private static PatchPlan Build(
        Settings settings,
        IEnumerable<RecordSnapshot>? cells = null,
        IEnumerable<RecordSnapshot>? worldspaces = null,
        IEnumerable<RecordSnapshot>? encounterZones = null)
    {
        return ReadOnlyPlanner.Build(
            settings,
            TestData.ValidatedZones(),
            cells ?? [],
            worldspaces ?? [],
            encounterZones ?? []);
    }
}
