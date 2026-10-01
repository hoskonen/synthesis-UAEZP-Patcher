using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace UAEZPSynthesisPatcher.Tests;

[TestClass]
public sealed class DungeonTierBiasTests
{
    [TestMethod]
    public void DungeonKeywordClassificationUsesSkyrimLocationKeywords()
    {
        Assert.AreEqual(
            DungeonCategory.Cave,
            Classify(DungeonCategoryResolver.LocSetCave));
        Assert.AreEqual(
            DungeonCategory.NordicRuin,
            Classify(DungeonCategoryResolver.LocSetNordicRuin));
        Assert.AreEqual(
            DungeonCategory.DwemerRuin,
            Classify(DungeonCategoryResolver.LocSetDwarvenRuin));
        Assert.AreEqual(
            DungeonCategory.Mine,
            Classify(
                DungeonCategoryResolver.LocSetCave,
                DungeonCategoryResolver.LocTypeMine));
        Assert.AreEqual(
            DungeonCategory.Fort,
            Classify(DungeonCategoryResolver.LocSetMilitaryFort));
        Assert.AreEqual(
            DungeonCategory.OtherDungeon,
            Classify(DungeonCategoryResolver.LocTypeDungeon));
    }

    [TestMethod]
    public void OrdinaryInteriorAndExteriorReceiveNoCategory()
    {
        Assert.AreEqual(
            DungeonCategory.None,
            Classify(
                DungeonCategoryResolver.LocTypeInn,
                DungeonCategoryResolver.LocTypeHouse,
                DungeonCategoryResolver.LocTypeStore));
        Assert.AreEqual(
            DungeonCategory.None,
            DungeonCategoryResolver.ClassifyKeywords(
                false,
                [DungeonCategoryResolver.LocSetCave]));
    }

    [TestMethod]
    public void ResolverUsesCellsAssociatedWinningLocation()
    {
        var mod = new SkyrimMod(
            ModKey.FromFileName("Locations.esp"),
            SkyrimRelease.SkyrimSE);
        var location = new Location(
            mod.GetNextFormKey(),
            SkyrimRelease.SkyrimSE)
        {
            EditorID = "TestNordicDungeon",
        };
        location.Keywords ??= [];
        location.Keywords.Add(
            new FormLink<IKeywordGetter>(
                DungeonCategoryResolver.LocSetNordicRuin));
        mod.Locations.Add(location);
        var cell = new Cell(mod.GetNextFormKey(), SkyrimRelease.SkyrimSE)
        {
            Flags = Cell.Flag.IsInteriorCell,
        };
        cell.Location.SetTo(location.FormKey);
        mod.Cells.AddInteriorCell(cell);
        ILinkCache<ISkyrimMod, ISkyrimModGetter> cache =
            new ISkyrimModGetter[] { mod }.ToImmutableLinkCache();

        DungeonCategoryResolution result =
            DungeonCategoryResolver.Resolve(cell, cache);

        Assert.AreEqual(DungeonCategory.NordicRuin, result.Category);
        Assert.AreEqual(location.FormKey, result.LocationFormKey);
        Assert.AreEqual("TestNordicDungeon", result.LocationEditorId);
    }

    [TestMethod]
    public void DisabledFeaturePreservesExistingAssignment()
    {
        RecordSnapshot cell = CellWithBaseTier(3, DungeonCategory.Cave);
        Settings disabled = BiasSettings(enabled: false);

        PatchPlan categorized = Build(disabled, cell);
        PatchPlan unclassified = Build(
            disabled,
            cell with { DungeonCategory = DungeonCategory.None });

        Assert.AreEqual(
            unclassified.Changes.Single().AssignedDummyZone!.FormKey,
            categorized.Changes.Single().AssignedDummyZone!.FormKey);
        Assert.AreEqual(3, categorized.Changes.Single().FinalDummyZoneTier);
    }

    [TestMethod]
    public void CaveOffsetRaisesSelectedTier()
    {
        PlannedChange change = Build(
            BiasSettings(),
            CellWithBaseTier(3, DungeonCategory.Cave))
            .Changes.Single();

        Assert.AreEqual(3, change.BaseDummyZoneTier);
        Assert.AreEqual(1, change.DungeonTierModifier);
        Assert.AreEqual(4, change.FinalDummyZoneTier);
        Assert.AreEqual(0x804u, change.AssignedDummyZone!.FormKey.ID);
    }

    [TestMethod]
    public void TierEightPositiveOffsetClampsAtTierEight()
    {
        PatchPlan plan = Build(
            BiasSettings(),
            CellWithBaseTier(8, DungeonCategory.Cave));
        PlannedChange change = plan.Changes.Single();

        Assert.AreEqual(8, change.FinalDummyZoneTier);
        Assert.IsTrue(change.DungeonTierClampedAtMaximum);
        Assert.AreEqual(1, plan.DungeonTierBias.ClampedAtMaximum);
    }

    [TestMethod]
    public void FirstModePlusOneSelectsTierOne()
    {
        Settings settings = BiasSettings();
        settings.DummyZoneMode = DummyZoneMode.First;
        RecordSnapshot cell = TestData.Record(PlannedRecordType.Cell, 1) with
        {
            DungeonCategory = DungeonCategory.Cave,
        };

        PlannedChange change = Build(settings, cell).Changes.Single();

        Assert.AreEqual(0, change.BaseDummyZoneTier);
        Assert.AreEqual(1, change.FinalDummyZoneTier);
        Assert.AreEqual(0x801u, change.AssignedDummyZone!.FormKey.ID);
    }

    [TestMethod]
    public void DeterministicRandomBiasIsStable()
    {
        Settings settings = BiasSettings();
        RecordSnapshot cell = CellWithBaseTier(5, DungeonCategory.NordicRuin);

        PlannedChange first = Build(settings, cell).Changes.Single();
        PlannedChange second = Build(settings, cell).Changes.Single();

        Assert.AreEqual(first.BaseDummyZoneTier, second.BaseDummyZoneTier);
        Assert.AreEqual(first.FinalDummyZoneTier, second.FinalDummyZoneTier);
        Assert.AreEqual(
            first.AssignedDummyZone!.FormKey,
            second.AssignedDummyZone!.FormKey);
    }

    [TestMethod]
    public void ExistingEncounterZoneIsNeverShifted()
    {
        Settings settings = BiasSettings();
        RecordSnapshot cell = CellWithBaseTier(3, DungeonCategory.Cave) with
        {
            RequirementSatisfied = true,
        };

        PatchPlan plan = Build(settings, cell);

        Assert.AreEqual(0, plan.Cells.PlannedOverrides);
        Assert.AreEqual(0, plan.DungeonTierBias.TierShifts.Count);
    }

    [TestMethod]
    public void FwmfForwardIsNeverShifted()
    {
        Settings settings = BiasSettings();
        settings.AssignMissingCellEncounterZones = false;
        settings.ForwardEncounterZonesThroughFwmf = true;
        FormKey earlierZone = new(TestData.SkyrimModKey, 0x1234);
        RecordSnapshot cell = TestData.Record(
            PlannedRecordType.Cell,
            1,
            winningModKey: ModKey.FromFileName("FWMF.esp")) with
        {
            DungeonCategory = DungeonCategory.Cave,
            EarlierResolvableEncounterZoneTarget = earlierZone,
        };

        PlannedChange change = Build(settings, cell).Changes.Single();

        Assert.AreEqual(earlierZone, change.ForwardedEncounterZone);
        Assert.IsNull(change.AssignedDummyZone);
        Assert.IsNull(change.BaseDummyZoneTier);
        Assert.AreEqual(0, change.DungeonTierModifier);
    }

    [TestMethod]
    public void DifficultyProfileChangesNumericMeaningNotTierSelection()
    {
        RecordSnapshot cell = CellWithBaseTier(2, DungeonCategory.DwemerRuin);
        Settings easy = BiasSettings();
        easy.DifficultyProfile =
            EncounterZoneDifficultyProfileSelection.UAEZPEasy;
        Settings hard = BiasSettings();
        hard.DifficultyProfile =
            EncounterZoneDifficultyProfileSelection.UAEZPHard;

        PlannedChange easyChange = Build(easy, cell).Changes.Single();
        PlannedChange hardChange = Build(hard, cell).Changes.Single();

        Assert.AreEqual(easyChange.BaseDummyZoneTier, hardChange.BaseDummyZoneTier);
        Assert.AreEqual(easyChange.FinalDummyZoneTier, hardChange.FinalDummyZoneTier);
        Assert.AreEqual(
            easyChange.AssignedDummyZone!.FormKey,
            hardChange.AssignedDummyZone!.FormKey);
        Assert.AreNotEqual(
            EncounterZoneDifficultyResolver.Easy.Tiers[
                easyChange.FinalDummyZoneTier!.Value].MinimumLevel,
            EncounterZoneDifficultyResolver.Hard.Tiers[
                hardChange.FinalDummyZoneTier!.Value].MinimumLevel);
    }

    [TestMethod]
    public void DryRunReportsCategoryShiftAndClampDiagnostics()
    {
        Settings settings = BiasSettings();
        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate([TestData.EasySource()]);
        PatchPlan plan = ReadOnlyPlanner.Build(
            settings,
            source.DummyZones,
            [CellWithBaseTier(8, DungeonCategory.Cave)],
            [],
            []);

        string report = DryRunReport.Render(
            new PlanningRun(source, plan),
            settings);

        StringAssert.Contains(report, "Dungeon tier bias:");
        StringAssert.Contains(report, "  Cave: 1");
        StringAssert.Contains(report, "  +1: 1");
        StringAssert.Contains(report, "  Clamped at tier 8: 1");
    }

    [TestMethod]
    public void ApplyReportContainsCategoryShiftAndClampDiagnostics()
    {
        Settings settings = BiasSettings();
        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate([TestData.EasySource()]);
        PatchPlan plan = ReadOnlyPlanner.Build(
            settings,
            source.DummyZones,
            [
                CellWithBaseTier(8, DungeonCategory.Cave),
                CellWithBaseTier(4, DungeonCategory.NordicRuin),
            ],
            [],
            []);

        string report = ApplyReport.Render(plan, new ApplyResult(2, 0, 0));

        StringAssert.Contains(report, "Dungeon tier bias:");
        StringAssert.Contains(report, "  Cave: 1");
        StringAssert.Contains(report, "  Nordic Ruin: 1");
        StringAssert.Contains(report, "  Dwemer Ruin: 0");
        StringAssert.Contains(report, "  Mine: 0");
        StringAssert.Contains(report, "  Fort: 0");
        StringAssert.Contains(report, "  Other Dungeon: 0");
        StringAssert.Contains(report, "Tier shifts:");
        StringAssert.Contains(report, "  +1: 2");
        StringAssert.Contains(report, "  Clamped at tier 8: 1");
    }

    private static DungeonCategory Classify(params FormKey[] keywords)
    {
        return DungeonCategoryResolver.ClassifyKeywords(true, keywords);
    }

    private static Settings BiasSettings(bool enabled = true)
    {
        return new Settings
        {
            EnableDungeonTierBias = enabled,
            DisableCombatBoundaries = false,
            DryRun = true,
        };
    }

    private static PatchPlan Build(Settings settings, RecordSnapshot cell)
    {
        return ReadOnlyPlanner.Build(
            settings,
            TestData.ValidatedZones(),
            [cell],
            [],
            []);
    }

    private static RecordSnapshot CellWithBaseTier(
        int baseTier,
        DungeonCategory category)
    {
        const int seed = 38174;
        uint id = Enumerable.Range(1, 100_000)
            .Select(value => (uint)value)
            .First(candidate =>
                DeterministicDummyZoneSelector.GetV1Index(
                    seed,
                    new FormKey(TestData.SkyrimModKey, candidate),
                    EncounterZoneDifficultyResolver.TierCount) == baseTier);
        return TestData.Record(PlannedRecordType.Cell, id) with
        {
            DungeonCategory = category,
            LocationFormKey = new FormKey(TestData.SkyrimModKey, 0x2000),
            LocationEditorId = "TestDungeonLocation",
        };
    }
}
