using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace UAEZPSynthesisPatcher.Tests;

[TestClass]
public sealed class PatchApplierTests
{
    [TestMethod]
    public void DryRunPlanWritesZeroRecordsWhenNotApplied()
    {
        var patch = NewMod("Output.esp");
        RecordSnapshot cell = TestData.Record(PlannedRecordType.Cell, 1);

        _ = BuildRun(cells: [cell]);

        Assert.AreEqual(0, patch.Cells.Count);
        Assert.AreEqual(0, patch.Worldspaces.Count);
        Assert.AreEqual(0, patch.EncounterZones.Count);
    }

    [TestMethod]
    public void AppliesPlannedCellWithExactDummyAndPreservesWinningValues()
    {
        var source = NewMod("CellWinner.esp");
        Cell winning = AddInteriorCell(source);
        winning.EditorID = "WinningCellEditorId";
        winning.WaterHeight = 42.5f;
        RecordSnapshot snapshot = Snapshot(PlannedRecordType.Cell, winning);
        var patch = NewMod("Output.esp");
        PlanningRun run = BuildRun(
            cells: [snapshot],
            cellFactories: new Dictionary<FormKey, Func<ISkyrimMod, ICell>>
            {
                [winning.FormKey] = output => AddInteriorOverride(output, winning),
            });

        FormKey planned = run.Plan.Changes.Single().AssignedDummyZone!.FormKey;
        ApplyResult result = PatchApplier.Apply(patch, run);
        ICellGetter applied = patch.EnumerateMajorRecords()
            .OfType<ICellGetter>()
            .Single();

        Assert.AreEqual(1, result.CellsApplied);
        Assert.AreEqual(planned, applied.EncounterZone.FormKey);
        Assert.AreEqual("WinningCellEditorId", applied.EditorID);
        Assert.AreEqual(42.5f, applied.WaterHeight);
    }

    [TestMethod]
    public void CapturedInteriorWinningContextCreatesOverride()
    {
        var source = NewMod("InteriorSource.esp");
        Cell winning = AddInteriorCell(source);
        winning.EditorID = "CapturedInterior";
        winning.WaterHeight = 19.25f;
        ISkyrimModGetter[] loadOrder = [source];
        ILinkCache<ISkyrimMod, ISkyrimModGetter> cache =
            loadOrder.ToImmutableLinkCache();
        var context = loadOrder.Cell()
            .WinningContextOverrides(cache)
            .Single(candidate => candidate.Record.FormKey == winning.FormKey);
        var patch = NewMod("Output.esp");
        PlanningRun run = BuildRun(
            cells: [Snapshot(PlannedRecordType.Cell, context.Record)],
            cellFactories: new Dictionary<FormKey, Func<ISkyrimMod, ICell>>
            {
                [winning.FormKey] = output => context.GetOrAddAsOverride(output),
            });

        PatchApplier.Apply(patch, run);
        ICellGetter applied = patch.EnumerateMajorRecords()
            .OfType<ICellGetter>()
            .Single();

        Assert.AreEqual("CapturedInterior", applied.EditorID);
        Assert.AreEqual(19.25f, applied.WaterHeight);
        Assert.IsFalse(applied.EncounterZone.IsNull);
    }

    [TestMethod]
    public void ExteriorWinningContextCreatesOnlyRequiredStructuralPath()
    {
        var baseMod = NewMod("BaseExterior.esm");
        Worldspace baseWorld = baseMod.Worldspaces.AddNew();
        baseWorld.EditorID = "BaseWorld";
        Cell baseTarget = AddExteriorCell(baseMod, baseWorld, 2, 3);
        baseTarget.EditorID = "TargetCell";
        Cell sibling = AddExteriorCell(baseMod, baseWorld, 3, 3);
        sibling.EditorID = "UnrelatedSibling";

        var later = NewMod("ExteriorWinner.esp");
        var laterWorld = new Worldspace(
            baseWorld.FormKey,
            SkyrimRelease.SkyrimSE)
        {
            EditorID = "WinningWorld",
        };
        later.Worldspaces.Add(laterWorld);
        var laterTarget = new Cell(
            baseTarget.FormKey,
            SkyrimRelease.SkyrimSE)
        {
            EditorID = "WinningTargetCell",
            Grid = new CellGrid { Point = new P2Int(2, 3) },
            WaterHeight = 77.75f,
        };
        laterWorld.AddCell(laterTarget);
        laterTarget.Persistent.Add(new PlacedObject(
            later.GetNextFormKey(),
            SkyrimRelease.SkyrimSE));
        laterTarget.Temporary.Add(new PlacedObject(
            later.GetNextFormKey(),
            SkyrimRelease.SkyrimSE));

        ISkyrimModGetter[] listedOrder = [baseMod, later];
        ISkyrimModGetter[] priorityOrder = [later, baseMod];
        ILinkCache<ISkyrimMod, ISkyrimModGetter> cache =
            listedOrder.ToImmutableLinkCache();
        var context = priorityOrder.Cell()
            .WinningContextOverrides(cache)
            .Single(candidate => candidate.Record.FormKey == baseTarget.FormKey);
        Assert.AreEqual(later.ModKey, context.ModKey);

        var patch = NewMod("Output.esp");
        PlanningRun run = BuildRun(
            cells: [Snapshot(PlannedRecordType.Cell, context.Record)],
            cellFactories: new Dictionary<FormKey, Func<ISkyrimMod, ICell>>
            {
                [baseTarget.FormKey] = output =>
                    context.GetOrAddAsOverride(output),
            });

        PatchApplier.Apply(patch, run);
        List<ICellGetter> outputCells = patch.EnumerateMajorRecords()
            .OfType<ICellGetter>()
            .ToList();

        Assert.AreEqual(1, patch.Worldspaces.Count);
        Assert.AreEqual("WinningWorld", patch.Worldspaces.Single().EditorID);
        Assert.AreEqual(1, outputCells.Count);
        Assert.AreEqual(baseTarget.FormKey, outputCells[0].FormKey);
        Assert.AreEqual("WinningTargetCell", outputCells[0].EditorID);
        Assert.AreEqual(77.75f, outputCells[0].WaterHeight);
        Assert.AreNotEqual(sibling.FormKey, outputCells[0].FormKey);
        Assert.IsFalse(outputCells[0].EncounterZone.IsNull);
        Assert.AreEqual(
            0,
            patch.EnumerateMajorRecords().OfType<IPlacedGetter>().Count());
    }

    [TestMethod]
    public void WorldspaceWinningContextPreservesLaterOverrideValues()
    {
        var baseMod = NewMod("BaseWorld.esm");
        Worldspace baseWorld = baseMod.Worldspaces.AddNew();
        baseWorld.EditorID = "BaseValue";
        var later = NewMod("WorldWinner.esp");
        var winner = new Worldspace(baseWorld.FormKey, SkyrimRelease.SkyrimSE)
        {
            EditorID = "LaterWinningValue",
        };
        later.Worldspaces.Add(winner);
        ISkyrimModGetter[] priorityOrder = [later, baseMod];
        var context = priorityOrder.Worldspace()
            .WinningContextOverrides()
            .Single(candidate => candidate.Record.FormKey == baseWorld.FormKey);
        var patch = NewMod("Output.esp");
        PlanningRun run = BuildRun(
            worldspaces:
            [Snapshot(PlannedRecordType.Worldspace, context.Record)],
            worldspaceFactories:
                new Dictionary<FormKey, Func<ISkyrimMod, IWorldspace>>
                {
                    [baseWorld.FormKey] = output =>
                        context.GetOrAddAsOverride(output),
                });

        PatchApplier.Apply(patch, run);

        Assert.AreEqual("LaterWinningValue", patch.Worldspaces.Single().EditorID);
        Assert.IsFalse(patch.Worldspaces.Single().EncounterZone.IsNull);
    }

    [TestMethod]
    public void PlannedWorldspaceReusesExteriorCellStructuralParent()
    {
        var source = NewMod("SharedExterior.esm");
        Worldspace world = source.Worldspaces.AddNew();
        world.EditorID = "SharedWinningWorld";
        Cell cell = AddExteriorCell(source, world, -2, 5);
        ISkyrimModGetter[] loadOrder = [source];
        ILinkCache<ISkyrimMod, ISkyrimModGetter> cache =
            loadOrder.ToImmutableLinkCache();
        var cellContext = loadOrder.Cell()
            .WinningContextOverrides(cache)
            .Single(candidate => candidate.Record.FormKey == cell.FormKey);
        var worldContext = loadOrder.Worldspace()
            .WinningContextOverrides()
            .Single(candidate => candidate.Record.FormKey == world.FormKey);
        var patch = NewMod("Output.esp");
        PlanningRun run = BuildRun(
            cells: [Snapshot(PlannedRecordType.Cell, cellContext.Record)],
            worldspaces:
            [Snapshot(PlannedRecordType.Worldspace, worldContext.Record)],
            cellFactories: new Dictionary<FormKey, Func<ISkyrimMod, ICell>>
            {
                [cell.FormKey] = output =>
                    cellContext.GetOrAddAsOverride(output),
            },
            worldspaceFactories:
                new Dictionary<FormKey, Func<ISkyrimMod, IWorldspace>>
                {
                    [world.FormKey] = output =>
                        worldContext.GetOrAddAsOverride(output),
                });

        ApplyResult result = PatchApplier.Apply(patch, run);
        IWorldspaceGetter outputWorld = patch.Worldspaces.Single();
        ICellGetter outputCell = patch.EnumerateMajorRecords()
            .OfType<ICellGetter>()
            .Single();

        Assert.AreEqual(1, result.CellsApplied);
        Assert.AreEqual(1, result.WorldspacesApplied);
        Assert.AreEqual("SharedWinningWorld", outputWorld.EditorID);
        Assert.IsFalse(outputWorld.EncounterZone.IsNull);
        Assert.IsFalse(outputCell.EncounterZone.IsNull);
    }

    [TestMethod]
    public void EncounterZoneWinningContextPreservesLaterFlagsAndData()
    {
        var baseMod = NewMod("BaseZones.esm");
        EncounterZone baseZone = baseMod.EncounterZones.AddNew();
        var later = NewMod("ZoneWinner.esp");
        var winner = new EncounterZone(
            baseZone.FormKey,
            SkyrimRelease.SkyrimSE)
        {
            EditorID = "LaterWinningZone",
            MinLevel = 21,
            MaxLevel = 48,
            Rank = 4,
            Flags = EncounterZone.Flag.NeverResets |
                EncounterZone.Flag.MatchPcBelowMinimumLevel,
        };
        later.EncounterZones.Add(winner);
        ISkyrimModGetter[] priorityOrder = [later, baseMod];
        var context = priorityOrder.EncounterZone()
            .WinningContextOverrides()
            .Single(candidate => candidate.Record.FormKey == baseZone.FormKey);
        var patch = NewMod("Output.esp");
        PlanningRun run = BuildRun(
            encounterZones:
            [Snapshot(PlannedRecordType.EncounterZone, context.Record)],
            encounterZoneFactories:
                new Dictionary<FormKey, Func<ISkyrimMod, IEncounterZone>>
                {
                    [baseZone.FormKey] = output =>
                        context.GetOrAddAsOverride(output),
                });

        PatchApplier.Apply(patch, run);
        IEncounterZoneGetter applied = patch.EncounterZones.Single();

        Assert.AreEqual("LaterWinningZone", applied.EditorID);
        Assert.AreEqual(21, applied.MinLevel);
        Assert.AreEqual(48, applied.MaxLevel);
        Assert.AreEqual(4, applied.Rank);
        Assert.IsTrue(applied.Flags.HasFlag(EncounterZone.Flag.NeverResets));
        Assert.IsTrue(applied.Flags.HasFlag(
            EncounterZone.Flag.MatchPcBelowMinimumLevel));
        Assert.IsTrue(applied.Flags.HasFlag(
            EncounterZone.Flag.DisableCombatBoundary));
    }

    [TestMethod]
    public void LaterWinningCellEncounterZoneSuppressesPlan()
    {
        var baseMod = NewMod("BaseCell.esm");
        Cell baseCell = AddInteriorCell(baseMod);
        var later = NewMod("ConfiguredWinner.esp");
        var winner = new Cell(baseCell.FormKey, SkyrimRelease.SkyrimSE)
        {
            Flags = Cell.Flag.IsInteriorCell,
        };
        winner.EncounterZone.SetTo(new FormKey(TestData.SkyrimModKey, 0x1234));
        later.Cells.AddInteriorCell(winner);
        ISkyrimModGetter[] listedOrder = [baseMod, later];
        ISkyrimModGetter[] priorityOrder = [later, baseMod];
        ILinkCache<ISkyrimMod, ISkyrimModGetter> cache =
            listedOrder.ToImmutableLinkCache();
        var context = priorityOrder.Cell()
            .WinningContextOverrides(cache)
            .Single(candidate => candidate.Record.FormKey == baseCell.FormKey);
        RecordSnapshot snapshot = Snapshot(PlannedRecordType.Cell, context.Record)
            with
            {
                RequirementSatisfied = !context.Record.EncounterZone.IsNull,
                HasUnresolvedRequirementReference = true,
            };

        PlanningRun run = BuildRun(cells: [snapshot]);
        var patch = NewMod("Output.esp");
        ApplyResult result = PatchApplier.Apply(patch, run);

        Assert.AreEqual(later.ModKey, context.ModKey);
        Assert.AreEqual(0, run.Plan.Cells.PlannedOverrides);
        Assert.AreEqual(0, result.TotalApplied);
        Assert.AreEqual(0, patch.Cells.Count);
    }

    [TestMethod]
    public void AppliesPlannedWorldspaceAndPreservesWinningValues()
    {
        var source = NewMod("WorldWinner.esp");
        IWorldspace winning = source.Worldspaces.AddNew();
        winning.EditorID = "WinningWorldEditorId";
        RecordSnapshot snapshot = Snapshot(PlannedRecordType.Worldspace, winning);
        var patch = NewMod("Output.esp");
        PlanningRun run = BuildRun(
            worldspaces: [snapshot],
            worldspaceFactories:
                new Dictionary<FormKey, Func<ISkyrimMod, IWorldspace>>
                {
                    [winning.FormKey] = output =>
                        output.Worldspaces.GetOrAddAsOverride(winning),
                });

        FormKey planned = run.Plan.Changes.Single().AssignedDummyZone!.FormKey;
        ApplyResult result = PatchApplier.Apply(patch, run);
        IWorldspaceGetter applied = patch.Worldspaces.Single();

        Assert.AreEqual(1, result.WorldspacesApplied);
        Assert.AreEqual(planned, applied.EncounterZone.FormKey);
        Assert.AreEqual("WinningWorldEditorId", applied.EditorID);
    }

    [TestMethod]
    public void AppliesEncounterZoneByOringFlagAndPreservingData()
    {
        var source = NewMod("EncounterZoneWinner.esp");
        IEncounterZone winning = source.EncounterZones.AddNew();
        winning.EditorID = "WinningEncounterZone";
        winning.MinLevel = 17;
        winning.MaxLevel = 44;
        winning.Rank = 3;
        winning.Flags = EncounterZone.Flag.NeverResets |
            EncounterZone.Flag.MatchPcBelowMinimumLevel;
        EncounterZone.Flag originalFlags = winning.Flags;
        RecordSnapshot snapshot = Snapshot(
            PlannedRecordType.EncounterZone,
            winning);
        var patch = NewMod("Output.esp");
        PlanningRun run = BuildRun(
            encounterZones: [snapshot],
            encounterZoneFactories:
                new Dictionary<FormKey, Func<ISkyrimMod, IEncounterZone>>
                {
                    [winning.FormKey] = output =>
                        output.EncounterZones.GetOrAddAsOverride(winning),
                });

        ApplyResult result = PatchApplier.Apply(patch, run);
        IEncounterZoneGetter applied = patch.EncounterZones.Single();

        Assert.AreEqual(1, result.EncounterZonesApplied);
        Assert.AreEqual(
            originalFlags | EncounterZone.Flag.DisableCombatBoundary,
            applied.Flags);
        Assert.AreEqual(17, applied.MinLevel);
        Assert.AreEqual(44, applied.MaxLevel);
        Assert.AreEqual(3, applied.Rank);
        Assert.AreEqual("WinningEncounterZone", applied.EditorID);
    }

    [TestMethod]
    public void ExistingCellEncounterZoneCreatesNoOverride()
    {
        AssertNoOverrideForSatisfied(PlannedRecordType.Cell);
    }

    [TestMethod]
    public void ExistingWorldspaceEncounterZoneCreatesNoOverride()
    {
        AssertNoOverrideForSatisfied(PlannedRecordType.Worldspace);
    }

    [TestMethod]
    public void ExistingEncounterZoneFlagCreatesNoOverride()
    {
        AssertNoOverrideForSatisfied(PlannedRecordType.EncounterZone);
    }

    [TestMethod]
    public void UnresolvedNonNullCellEncounterZoneCreatesNoOverride()
    {
        RecordSnapshot unresolved = TestData.Record(
            PlannedRecordType.Cell,
            1,
            satisfied: true) with
        {
            HasUnresolvedRequirementReference = true,
        };
        var patch = NewMod("Output.esp");
        PlanningRun run = BuildRun(cells: [unresolved]);

        ApplyResult result = PatchApplier.Apply(patch, run);

        Assert.AreEqual(0, result.TotalApplied);
        Assert.AreEqual(0, patch.Cells.Count);
    }

    [TestMethod]
    public void DeletedWinnerCreatesNoOverride()
    {
        RecordSnapshot deleted = TestData.Record(
            PlannedRecordType.Cell,
            1,
            deleted: true);
        var patch = NewMod("Output.esp");
        PlanningRun run = BuildRun(cells: [deleted]);

        ApplyResult result = PatchApplier.Apply(patch, run);

        Assert.AreEqual(0, result.TotalApplied);
        Assert.AreEqual(0, patch.Cells.Count);
    }

    [TestMethod]
    public void AppliedCountsEqualPlannedCounts()
    {
        var source = NewMod("Winning.esp");
        Cell cell = AddInteriorCell(source);
        IWorldspace worldspace = source.Worldspaces.AddNew();
        IEncounterZone encounterZone = source.EncounterZones.AddNew();
        var patch = NewMod("Output.esp");
        PlanningRun run = BuildRun(
            cells: [Snapshot(PlannedRecordType.Cell, cell)],
            worldspaces: [Snapshot(PlannedRecordType.Worldspace, worldspace)],
            encounterZones:
            [Snapshot(PlannedRecordType.EncounterZone, encounterZone)],
            cellFactories: new Dictionary<FormKey, Func<ISkyrimMod, ICell>>
            {
                [cell.FormKey] = output => AddInteriorOverride(output, cell),
            },
            worldspaceFactories:
                new Dictionary<FormKey, Func<ISkyrimMod, IWorldspace>>
                {
                    [worldspace.FormKey] = output =>
                        output.Worldspaces.GetOrAddAsOverride(worldspace),
                },
            encounterZoneFactories:
                new Dictionary<FormKey, Func<ISkyrimMod, IEncounterZone>>
                {
                    [encounterZone.FormKey] = output =>
                        output.EncounterZones.GetOrAddAsOverride(encounterZone),
                });

        ApplyResult result = PatchApplier.Apply(patch, run);

        Assert.AreEqual(run.Plan.Cells.PlannedOverrides, result.CellsApplied);
        Assert.AreEqual(
            run.Plan.Worldspaces.PlannedOverrides,
            result.WorldspacesApplied);
        Assert.AreEqual(
            run.Plan.EncounterZones.PlannedOverrides,
            result.EncounterZonesApplied);
        Assert.AreEqual(run.Plan.TotalPlannedOverrides, result.TotalApplied);
    }

    [TestMethod]
    public void DryRunAndApplySettingsProduceIdenticalPlans()
    {
        RecordSnapshot cell = TestData.Record(PlannedRecordType.Cell, 1);
        RecordSnapshot worldspace =
            TestData.Record(PlannedRecordType.Worldspace, 2);
        RecordSnapshot encounterZone =
            TestData.Record(PlannedRecordType.EncounterZone, 3);

        PatchPlan dryPlan = ReadOnlyPlanner.Build(
            new Settings { DryRun = true },
            TestData.ValidatedZones(),
            [cell],
            [worldspace],
            [encounterZone]);
        PatchPlan applyPlan = ReadOnlyPlanner.Build(
            new Settings { DryRun = false },
            TestData.ValidatedZones(),
            [cell],
            [worldspace],
            [encounterZone]);

        CollectionAssert.AreEqual(
            dryPlan.Changes.ToList(),
            applyPlan.Changes.ToList());
        Assert.AreEqual(
            dryPlan.TotalPlannedOverrides,
            applyPlan.TotalPlannedOverrides);
    }

    [TestMethod]
    public void MissingRequiredContextFailsBeforeMutation()
    {
        PlanningRun run = BuildRun(
            cells: [TestData.Record(PlannedRecordType.Cell, 1)]);
        var patch = NewMod("Output.esp");

        InvalidOperationException error =
            Assert.ThrowsExactly<InvalidOperationException>(
                () => PatchApplier.Apply(patch, run));

        StringAssert.Contains(error.Message, "No captured winning context");
        Assert.AreEqual(0, patch.Cells.Count);
    }

    [TestMethod]
    public void ContextFailureIsSurfacedClearly()
    {
        RecordSnapshot cell = TestData.Record(PlannedRecordType.Cell, 1);
        PlanningRun run = BuildRun(
            cells: [cell],
            cellFactories: new Dictionary<FormKey, Func<ISkyrimMod, ICell>>
            {
                [cell.FormKey] = _ => throw new InvalidOperationException("boom"),
            });
        var patch = NewMod("Output.esp");

        InvalidOperationException error =
            Assert.ThrowsExactly<InvalidOperationException>(
                () => PatchApplier.Apply(patch, run));

        StringAssert.Contains(error.Message, "Failed to apply planned Cell");
        StringAssert.Contains(error.Message, cell.FormKey.ToString());
        StringAssert.Contains(error.Message, "boom");
    }

    [TestMethod]
    public void ExistingDifferentOutputEncounterZoneIsNotOverwritten()
    {
        var source = NewMod("CellWinner.esp");
        Cell winning = AddInteriorCell(source);
        RecordSnapshot snapshot = Snapshot(PlannedRecordType.Cell, winning);
        var patch = NewMod("Output.esp");
        ICell existingOutput = AddInteriorOverride(patch, winning);
        FormKey earlierAssignment = new(TestData.SkyrimModKey, 0x1234);
        existingOutput.EncounterZone.SetTo(earlierAssignment);
        PlanningRun run = BuildRun(
            cells: [snapshot],
            cellFactories: new Dictionary<FormKey, Func<ISkyrimMod, ICell>>
            {
                [winning.FormKey] = _ => existingOutput,
            });

        InvalidOperationException error =
            Assert.ThrowsExactly<InvalidOperationException>(
                () => PatchApplier.Apply(patch, run));

        StringAssert.Contains(error.Message, "already has XEZN");
        Assert.AreEqual(earlierAssignment, existingOutput.EncounterZone.FormKey);
    }

    [TestMethod]
    public void ApplyReportShowsVerifiedCounts()
    {
        PatchPlan plan = ReadOnlyPlanner.Build(
            new Settings(),
            TestData.ValidatedZones(),
            [],
            [],
            []);
        var result = new ApplyResult(0, 0, 0);

        string report = ApplyReport.Render(plan, result);

        StringAssert.Contains(report, "UAEZP Synthesis Patcher - Apply");
        StringAssert.Contains(report, "Applied/verified overrides:");
        StringAssert.Contains(report, "Apply complete.");
    }

    private static void AssertNoOverrideForSatisfied(PlannedRecordType type)
    {
        RecordSnapshot record = TestData.Record(type, 1, satisfied: true);
        var patch = NewMod("Output.esp");
        PlanningRun run = type switch
        {
            PlannedRecordType.Cell => BuildRun(cells: [record]),
            PlannedRecordType.Worldspace => BuildRun(worldspaces: [record]),
            PlannedRecordType.EncounterZone =>
                BuildRun(encounterZones: [record]),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        ApplyResult result = PatchApplier.Apply(patch, run);

        Assert.AreEqual(0, result.TotalApplied);
        Assert.AreEqual(0, patch.Cells.Count);
        Assert.AreEqual(0, patch.Worldspaces.Count);
        Assert.AreEqual(0, patch.EncounterZones.Count);
    }

    private static PlanningRun BuildRun(
        IEnumerable<RecordSnapshot>? cells = null,
        IEnumerable<RecordSnapshot>? worldspaces = null,
        IEnumerable<RecordSnapshot>? encounterZones = null,
        IReadOnlyDictionary<FormKey, Func<ISkyrimMod, ICell>>?
            cellFactories = null,
        IReadOnlyDictionary<FormKey, Func<ISkyrimMod, IWorldspace>>?
            worldspaceFactories = null,
        IReadOnlyDictionary<FormKey, Func<ISkyrimMod, IEncounterZone>>?
            encounterZoneFactories = null)
    {
        PatchPlan plan = ReadOnlyPlanner.Build(
            new Settings { DryRun = false },
            TestData.ValidatedZones(),
            cells ?? [],
            worldspaces ?? [],
            encounterZones ?? []);

        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate([TestData.EasySource()]);
        return new PlanningRun(source, plan)
        {
            ApplyContexts = new ApplyContextCatalog(
                cellFactories ??
                    new Dictionary<FormKey, Func<ISkyrimMod, ICell>>(),
                worldspaceFactories ??
                    new Dictionary<FormKey, Func<ISkyrimMod, IWorldspace>>(),
                encounterZoneFactories ??
                    new Dictionary<FormKey, Func<ISkyrimMod, IEncounterZone>>()),
        };
    }

    private static RecordSnapshot Snapshot(
        PlannedRecordType type,
        IMajorRecordGetter record)
    {
        return new RecordSnapshot(
            type,
            record.FormKey,
            record.EditorID,
            record.FormKey.ModKey,
            false,
            false);
    }

    private static SkyrimMod NewMod(string fileName)
    {
        return new SkyrimMod(
            ModKey.FromFileName(fileName),
            SkyrimRelease.SkyrimSE);
    }

    private static Cell AddInteriorCell(SkyrimMod mod)
    {
        var cell = new Cell(mod.GetNextFormKey(), SkyrimRelease.SkyrimSE)
        {
            Flags = Cell.Flag.IsInteriorCell,
        };
        mod.Cells.AddInteriorCell(cell);
        return cell;
    }

    private static ICell AddInteriorOverride(ISkyrimMod mod, ICellGetter winning)
    {
        Cell copy = winning.DeepCopy();
        mod.Cells.AddInteriorCell(copy);
        return copy;
    }

    private static Cell AddExteriorCell(
        SkyrimMod mod,
        IWorldspace worldspace,
        int x,
        int y)
    {
        var cell = new Cell(mod.GetNextFormKey(), SkyrimRelease.SkyrimSE)
        {
            Grid = new CellGrid { Point = new P2Int(x, y) },
        };
        worldspace.AddCell(cell);
        return cell;
    }
}
