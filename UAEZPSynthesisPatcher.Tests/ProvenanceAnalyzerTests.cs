using Mutagen.Bethesda.Plugins;

namespace UAEZPSynthesisPatcher.Tests;

[TestClass]
public sealed class ProvenanceAnalyzerTests
{
    [TestMethod]
    public void ExistingCellLinkIsAttributedToWinningPluginNotOrigin()
    {
        ModKey winner = ModKey.FromFileName("SomePatch.esp");
        PatchPlan plan = Build(cells: [ExistingLink(
            PlannedRecordType.Cell,
            1,
            winner,
            TestData.ValidatedZones()[0].FormKey)]);

        Assert.AreEqual(
            1,
            plan.ExistingStateProvenance.Cells.ByWinningPlugin[winner]);
        Assert.IsFalse(
            plan.ExistingStateProvenance.Cells.ByWinningPlugin.ContainsKey(
                TestData.SkyrimModKey));
    }

    [TestMethod]
    public void UnresolvedExistingCellLinkRetainsWinningPluginAttribution()
    {
        ModKey winner = ModKey.FromFileName("BrokenLinks.esp");
        RecordSnapshot cell = ExistingLink(
            PlannedRecordType.Cell,
            1,
            winner,
            new FormKey(ModKey.FromFileName("Missing.esp"), 0x123)) with
        {
            HasUnresolvedRequirementReference = true,
            ResolvedEncounterZoneEditorId = null,
        };

        PatchPlan plan = Build(cells: [cell]);

        Assert.AreEqual(1, plan.ExistingStateProvenance.Cells.UnresolvedLinks);
        Assert.AreEqual(
            1,
            plan.ExistingStateProvenance.Cells.ByWinningPlugin[winner]);
    }

    [TestMethod]
    public void ExistingWorldspaceLinkIsAttributedToWinningPlugin()
    {
        ModKey winner = ModKey.FromFileName("WorldspacePatch.esp");
        PatchPlan plan = Build(worldspaces: [ExistingLink(
            PlannedRecordType.Worldspace,
            2,
            winner,
            TestData.ValidatedZones()[1].FormKey)]);

        Assert.AreEqual(
            1,
            plan.ExistingStateProvenance.Worldspaces.ByWinningPlugin[winner]);
    }

    [TestMethod]
    public void ExistingEncounterZoneFlagIsAttributedToWinningPlugin()
    {
        ModKey winner = ModKey.FromFileName("EncounterZonePatch.esp");
        RecordSnapshot encounterZone = TestData.Record(
            PlannedRecordType.EncounterZone,
            3,
            satisfied: true,
            winningModKey: winner);

        PatchPlan plan = Build(encounterZones: [encounterZone]);

        Assert.AreEqual(
            1,
            plan.ExistingStateProvenance.EncounterZones
                .ByWinningPlugin[winner]);
    }

    [TestMethod]
    public void ValidatedDummyFormKeyIsDetectedAsContamination()
    {
        ValidatedDummyZone dummy = TestData.ValidatedZones()[2];
        ModKey winner = ModKey.FromFileName("ForwardingPatch.esp");
        PatchPlan plan = Build(cells: [ExistingLink(
            PlannedRecordType.Cell,
            4,
            winner,
            dummy.FormKey)]);

        ExistingLinkProvenance provenance =
            plan.ExistingStateProvenance.Cells;
        Assert.AreEqual(1, provenance.UaeZpDummyZoneLinks);
        Assert.AreEqual(1, provenance.UaeZpDummyZoneLinksByWinningPlugin[winner]);
    }

    [TestMethod]
    public void NonDummyEncounterZoneIsNotContamination()
    {
        FormKey ordinaryZone = new(TestData.SkyrimModKey, 0x1234);
        PatchPlan plan = Build(cells: [ExistingLink(
            PlannedRecordType.Cell,
            5,
            ModKey.FromFileName("OrdinaryPatch.esp"),
            ordinaryZone)]);

        Assert.AreEqual(
            0,
            plan.ExistingStateProvenance.Cells.UaeZpDummyZoneLinks);
    }

    [TestMethod]
    public void PluginBreakdownUsesDeterministicCountThenNameOrdering()
    {
        ModKey alpha = ModKey.FromFileName("Alpha.esp");
        ModKey beta = ModKey.FromFileName("Beta.esp");
        ModKey gamma = ModKey.FromFileName("Gamma.esp");
        IReadOnlyList<ValidatedDummyZone> zones = TestData.ValidatedZones();
        PatchPlan plan = Build(cells:
        [
            ExistingLink(PlannedRecordType.Cell, 1, beta, zones[0].FormKey),
            ExistingLink(PlannedRecordType.Cell, 2, gamma, zones[0].FormKey),
            ExistingLink(PlannedRecordType.Cell, 3, alpha, zones[0].FormKey),
            ExistingLink(PlannedRecordType.Cell, 4, beta, zones[0].FormKey),
            ExistingLink(PlannedRecordType.Cell, 5, alpha, zones[0].FormKey),
        ]);

        string report = Render(plan);
        int alphaIndex = report.IndexOf("  Alpha.esp: 2", StringComparison.Ordinal);
        int betaIndex = report.IndexOf("  Beta.esp: 2", StringComparison.Ordinal);
        int gammaIndex = report.IndexOf("  Gamma.esp: 1", StringComparison.Ordinal);

        Assert.IsTrue(alphaIndex >= 0);
        Assert.IsTrue(alphaIndex < betaIndex);
        Assert.IsTrue(betaIndex < gammaIndex);
    }

    [TestMethod]
    public void SuspiciousCellSampleIsBoundedAndStablySorted()
    {
        ModKey winner = ModKey.FromFileName("SuspiciousPatch.esp");
        FormKey dummy = TestData.ValidatedZones()[0].FormKey;
        IReadOnlyList<RecordSnapshot> reversed = Enumerable.Range(1, 30)
            .Reverse()
            .Select(index => ExistingLink(
                PlannedRecordType.Cell,
                (uint)index,
                winner,
                dummy))
            .ToList();

        PatchPlan plan = Build(cells: reversed);
        IReadOnlyList<ExistingLinkSample> samples =
            plan.ExistingStateProvenance.Cells.SuspiciousSamples;

        Assert.AreEqual(ProvenanceAnalyzer.SuspiciousCellSampleLimit, samples.Count);
        Assert.AreEqual(1u, samples[0].FormKey.ID);
        Assert.AreEqual(25u, samples[^1].FormKey.ID);
    }

    [TestMethod]
    public void BaseGameWinningPluginIsExcludedFromSuspiciousSample()
    {
        PatchPlan plan = Build(cells: [ExistingLink(
            PlannedRecordType.Cell,
            6,
            TestData.SkyrimModKey,
            TestData.ValidatedZones()[0].FormKey)]);

        Assert.AreEqual(
            0,
            plan.ExistingStateProvenance.Cells.SuspiciousSamples.Count);
    }

    [TestMethod]
    public void ProvenanceDiagnosticsDoNotChangePlannedCounts()
    {
        PatchPlan plan = Build(
            cells:
            [
                TestData.Record(PlannedRecordType.Cell, 10),
                ExistingLink(
                    PlannedRecordType.Cell,
                    11,
                    ModKey.FromFileName("ExistingCell.esp"),
                    TestData.ValidatedZones()[0].FormKey),
            ],
            worldspaces:
            [
                TestData.Record(PlannedRecordType.Worldspace, 12),
                ExistingLink(
                    PlannedRecordType.Worldspace,
                    13,
                    ModKey.FromFileName("ExistingWorld.esp"),
                    TestData.ValidatedZones()[1].FormKey),
            ],
            encounterZones:
            [
                TestData.Record(PlannedRecordType.EncounterZone, 14),
                TestData.Record(
                    PlannedRecordType.EncounterZone,
                    15,
                    satisfied: true,
                    winningModKey: ModKey.FromFileName("ExistingZone.esp")),
            ]);

        Assert.AreEqual(1, plan.Cells.PlannedOverrides);
        Assert.AreEqual(1, plan.Worldspaces.PlannedOverrides);
        Assert.AreEqual(1, plan.EncounterZones.PlannedOverrides);
        Assert.AreEqual(3, plan.TotalPlannedOverrides);
    }

    private static RecordSnapshot ExistingLink(
        PlannedRecordType type,
        uint id,
        ModKey winningModKey,
        FormKey target)
    {
        return TestData.Record(
            type,
            id,
            satisfied: true,
            winningModKey: winningModKey) with
        {
            EncounterZoneTarget = target,
            ResolvedEncounterZoneEditorId = "ResolvedEncounterZone",
        };
    }

    private static PatchPlan Build(
        IEnumerable<RecordSnapshot>? cells = null,
        IEnumerable<RecordSnapshot>? worldspaces = null,
        IEnumerable<RecordSnapshot>? encounterZones = null)
    {
        return ReadOnlyPlanner.Build(
            new Settings(),
            TestData.ValidatedZones(),
            cells ?? [],
            worldspaces ?? [],
            encounterZones ?? []);
    }

    private static string Render(PatchPlan plan)
    {
        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate([TestData.EasySource()]);
        return DryRunReport.Render(
            new PlanningRun(source, plan),
            new Settings());
    }
}
