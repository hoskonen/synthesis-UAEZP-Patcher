using Mutagen.Bethesda.Plugins;

namespace UAEZPSynthesisPatcher.Tests;

[TestClass]
public sealed class ExistingDungeonEncounterZoneDifficultyTests
{
    [TestMethod]
    [DataRow(6, 1, 7)]
    [DataRow(9, 1, 11)]
    [DataRow(11, 1, 13)]
    [DataRow(6, 2, 9)]
    [DataRow(10, -1, 9)]
    [DataRow(25, 1, 25)]
    [DataRow(2, -1, 2)]
    public void ResolverMovesThroughDistinctProfileBreakpoints(
        int existing,
        int offset,
        int expected)
    {
        byte actual = ExistingEncounterZoneDifficultyResolver.Resolve(
            (byte)existing,
            EncounterZoneDifficultyResolver.Easy,
            offset);

        Assert.AreEqual((byte)expected, actual);
        if (offset > 0)
        {
            Assert.IsTrue(actual >= existing);
        }
        else
        {
            Assert.IsTrue(actual <= existing);
        }
    }

    [TestMethod]
    public void DisabledFeatureDoesNotPlanExistingZoneMutation()
    {
        PatchPlan plan = BuildPlan(
            SettingsForBias(enabled: false),
            [DungeonCell(1, ZoneKey(1), DungeonCategory.Cave)],
            [EncounterZone(1, 6)]);

        Assert.AreEqual(0, plan.ExistingDungeonEncounterZoneBias.PlannedChanges);
        Assert.AreEqual(0, plan.EncounterZones.PlannedOverrides);
    }

    [TestMethod]
    [DataRow(DungeonCategory.Cave)]
    [DataRow(DungeonCategory.NordicRuin)]
    [DataRow(DungeonCategory.DwemerRuin)]
    [DataRow(DungeonCategory.Mine)]
    public void ClassifiedExistingZoneUsesNextBreakpoint(DungeonCategory category)
    {
        PatchPlan plan = BuildPlan(
            SettingsForBias(),
            [DungeonCell(1, ZoneKey(1), category)],
            [EncounterZone(1, 6)]);
        PlannedChange change = plan.Changes.Single();

        Assert.AreEqual(PlannedRecordType.EncounterZone, change.Target.RecordType);
        Assert.AreEqual((byte)7, change.DesiredEncounterZoneMinimumLevel);
        Assert.IsTrue(change.IsExistingDungeonMinimumLevelChange);
        Assert.AreEqual(0, plan.Cells.PlannedOverrides);
    }

    [TestMethod]
    public void OrdinaryInteriorWithExistingZoneIsUnchanged()
    {
        RecordSnapshot cell = DungeonCell(
            1,
            ZoneKey(1),
            DungeonCategory.None) with { IsInteriorCell = true };

        PatchPlan plan = BuildPlan(
            SettingsForBias(),
            [cell],
            [EncounterZone(1, 6)]);

        Assert.AreEqual(0, plan.EncounterZones.PlannedOverrides);
    }

    [TestMethod]
    public void DummyZonesAreExcludedFromExistingZonePath()
    {
        ValidatedDummyZone dummy = TestData.ValidatedZones()[0];
        PatchPlan plan = BuildPlan(
            SettingsForBias(),
            [DungeonCell(1, dummy.FormKey, DungeonCategory.Cave)],
            [new RecordSnapshot(
                PlannedRecordType.EncounterZone,
                dummy.FormKey,
                dummy.EditorId,
                dummy.FormKey.ModKey,
                false,
                true)
            {
                EncounterZoneMinimumLevel = dummy.MinLevel,
            }]);

        Assert.AreEqual(0, plan.ExistingDungeonEncounterZoneBias.PlannedChanges);
        Assert.AreEqual(0, plan.EncounterZones.PlannedOverrides);
    }

    [TestMethod]
    public void UnresolvedAndDeletedEncounterZonesAreSkipped()
    {
        RecordSnapshot unresolved = DungeonCell(
            1,
            ZoneKey(1),
            DungeonCategory.Cave) with
        {
            HasUnresolvedRequirementReference = true,
        };
        RecordSnapshot deletedZone = EncounterZone(2, 6) with
        {
            IsDeleted = true,
        };
        PatchPlan plan = BuildPlan(
            SettingsForBias(),
            [
                unresolved,
                DungeonCell(2, ZoneKey(2), DungeonCategory.Cave),
            ],
            [EncounterZone(1, 6), deletedZone]);

        Assert.AreEqual(0, plan.ExistingDungeonEncounterZoneBias.PlannedChanges);
        Assert.AreEqual(0, plan.EncounterZones.PlannedOverrides);
    }

    [TestMethod]
    public void SharedZoneProducesOneOverrideAndStrongestPositiveResult()
    {
        Settings settings = SettingsForBias();
        settings.NordicRuinTierOffset = 2;
        FormKey zone = ZoneKey(1);

        PatchPlan plan = BuildPlan(
            settings,
            [
                DungeonCell(1, zone, DungeonCategory.Cave),
                DungeonCell(2, zone, DungeonCategory.NordicRuin),
            ],
            [EncounterZone(1, 6)]);

        Assert.AreEqual(1, plan.EncounterZones.PlannedOverrides);
        Assert.AreEqual((byte)9, plan.Changes.Single()
            .DesiredEncounterZoneMinimumLevel);
        Assert.AreEqual(
            1,
            plan.ExistingDungeonEncounterZoneBias.SharedZoneConflictsResolved);
    }

    [TestMethod]
    public void MixedDirectionSharedZoneIsNeutralizedDeterministically()
    {
        Settings settings = SettingsForBias();
        settings.CaveTierOffset = 1;
        settings.NordicRuinTierOffset = -1;
        RecordSnapshot cave = DungeonCell(
            1,
            ZoneKey(1),
            DungeonCategory.Cave);
        RecordSnapshot nordic = DungeonCell(
            2,
            ZoneKey(1),
            DungeonCategory.NordicRuin);

        PatchPlan first = BuildPlan(
            settings,
            [cave, nordic],
            [EncounterZone(1, 10)]);
        PatchPlan reversed = BuildPlan(
            settings,
            [nordic, cave],
            [EncounterZone(1, 10)]);

        Assert.AreEqual(0, first.EncounterZones.PlannedOverrides);
        Assert.AreEqual(0, reversed.EncounterZones.PlannedOverrides);
        Assert.AreEqual(
            1,
            first.ExistingDungeonEncounterZoneBias.SharedZoneConflictsResolved);
        Assert.AreEqual(
            0,
            first.ExistingDungeonEncounterZoneBias.UnchangedAtProfileBoundary);
    }

    [TestMethod]
    public void SharedNegativeRequestsUseLowestResult()
    {
        Settings settings = SettingsForBias();
        settings.CaveTierOffset = -1;
        settings.NordicRuinTierOffset = -2;

        PatchPlan plan = BuildPlan(
            settings,
            [
                DungeonCell(1, ZoneKey(1), DungeonCategory.Cave),
                DungeonCell(2, ZoneKey(1), DungeonCategory.NordicRuin),
            ],
            [EncounterZone(1, 10)]);

        Assert.AreEqual((byte)7, plan.Changes.Single()
            .DesiredEncounterZoneMinimumLevel);
        Assert.AreEqual(
            1,
            plan.ExistingDungeonEncounterZoneBias.SharedZoneConflictsResolved);
    }

    [TestMethod]
    public void DungeonMinLevelAndCombatBoundaryShareOnePlan()
    {
        Settings settings = SettingsForBias();
        settings.DisableCombatBoundaries = true;

        PatchPlan plan = BuildPlan(
            settings,
            [DungeonCell(1, ZoneKey(1), DungeonCategory.Cave)],
            [EncounterZone(1, 6, hasCombatBoundaryFlag: false)]);
        PlannedChange change = plan.Changes.Single();

        Assert.AreEqual(1, plan.EncounterZones.PlannedOverrides);
        Assert.IsTrue(change.AddDisableCombatBoundary);
        Assert.IsTrue(change.IsExistingDungeonMinimumLevelChange);
        Assert.AreEqual((byte)7, change.DesiredEncounterZoneMinimumLevel);
    }

    [TestMethod]
    public void ActiveProfileChangesBreakpointsWithoutChangingClassification()
    {
        Settings easy = SettingsForBias();
        easy.DifficultyProfile = EncounterZoneDifficultyProfileSelection.UAEZPEasy;
        Settings hard = SettingsForBias();
        hard.DifficultyProfile = EncounterZoneDifficultyProfileSelection.UAEZPHard;
        RecordSnapshot cell = DungeonCell(
            1,
            ZoneKey(1),
            DungeonCategory.Cave);

        byte? easyLevel = BuildPlan(easy, [cell], [EncounterZone(1, 6)])
            .Changes.Single().DesiredEncounterZoneMinimumLevel;
        byte? hardLevel = BuildPlan(hard, [cell], [EncounterZone(1, 6)])
            .Changes.Single().DesiredEncounterZoneMinimumLevel;

        Assert.AreEqual((byte)7, easyLevel);
        Assert.AreEqual((byte)10, hardLevel);
        Assert.AreEqual(DungeonCategory.Cave, cell.DungeonCategory);
    }

    [TestMethod]
    public void ReportsContainCountsAndCappedSample()
    {
        Settings settings = SettingsForBias();
        settings.NordicRuinTierOffset = 2;
        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate([TestData.EasySource()]);
        PatchPlan plan = BuildPlan(
            settings,
            [
                DungeonCell(1, ZoneKey(1), DungeonCategory.Cave),
                DungeonCell(2, ZoneKey(1), DungeonCategory.NordicRuin),
                DungeonCell(3, ZoneKey(2), DungeonCategory.Cave),
            ],
            [EncounterZone(1, 6), EncounterZone(2, 25)]);

        string dryRun = DryRunReport.Render(
            new PlanningRun(source, plan),
            settings);
        string apply = ApplyReport.Render(
            plan,
            new ApplyResult(0, 0, 1)
            {
                ExistingDungeonEncounterZoneChangesApplied = 1,
            });

        StringAssert.Contains(dryRun, "Existing dungeon encounter-zone bias:");
        StringAssert.Contains(dryRun, "Cave ECZNs: 2");
        StringAssert.Contains(dryRun, "Existing ECZN MinLevel changes:");
        StringAssert.Contains(
            dryRun,
            "Unchanged at profile ceiling/floor: 1");
        StringAssert.Contains(dryRun, "Shared-zone conflicts resolved: 1");
        StringAssert.Contains(dryRun, "CELL");
        StringAssert.Contains(dryRun, "MinLevel: 6 +1 -> 7");
        StringAssert.Contains(apply, "Planned: 1");
        StringAssert.Contains(apply, "Applied: 1");
    }

    private static Settings SettingsForBias(bool enabled = true)
    {
        return new Settings
        {
            EnableDungeonTierBias = enabled,
            DisableCombatBoundaries = false,
            AssignMissingCellEncounterZones = false,
            AssignMissingWorldspaceEncounterZones = false,
            DifficultyProfile =
                EncounterZoneDifficultyProfileSelection.UAEZPEasy,
        };
    }

    private static PatchPlan BuildPlan(
        Settings settings,
        IReadOnlyList<RecordSnapshot> cells,
        IReadOnlyList<RecordSnapshot> encounterZones)
    {
        return ReadOnlyPlanner.Build(
            settings,
            TestData.ValidatedZones(),
            cells,
            [],
            encounterZones);
    }

    private static RecordSnapshot DungeonCell(
        uint id,
        FormKey zone,
        DungeonCategory category)
    {
        return TestData.Record(
            PlannedRecordType.Cell,
            id,
            satisfied: true) with
        {
            EncounterZoneTarget = zone,
            ResolvedEncounterZoneEditorId = $"ExistingZone{id}",
            DungeonCategory = category,
            IsInteriorCell = true,
            LocationFormKey = new FormKey(TestData.SkyrimModKey, 0x3000u + id),
            LocationEditorId = $"DungeonLocation{id}",
        };
    }

    private static RecordSnapshot EncounterZone(
        uint id,
        byte minimumLevel,
        bool hasCombatBoundaryFlag = true)
    {
        return new RecordSnapshot(
            PlannedRecordType.EncounterZone,
            ZoneKey(id),
            $"ExistingZone{id}",
            TestData.SkyrimModKey,
            false,
            hasCombatBoundaryFlag)
        {
            EncounterZoneMinimumLevel = minimumLevel,
        };
    }

    private static FormKey ZoneKey(uint id)
    {
        return new FormKey(TestData.SkyrimModKey, 0x5000u + id);
    }
}
