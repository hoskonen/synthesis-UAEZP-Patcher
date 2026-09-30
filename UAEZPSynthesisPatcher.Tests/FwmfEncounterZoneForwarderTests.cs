using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace UAEZPSynthesisPatcher.Tests;

[TestClass]
public sealed class FwmfEncounterZoneForwarderTests
{
    [TestMethod]
    [DataRow("FWMF.esp")]
    [DataRow("FWMF for Fantasy Paper Maps.esp")]
    [DataRow("FWMF - Map Addon.esl")]
    [DataRow("Flat World Map Framework.esp")]
    public void RecognizesFwmfFamilyPluginNames(string fileName)
    {
        Assert.IsTrue(
            FwmfEncounterZoneForwarder.IsFwmfFamilyPlugin(
                ModKey.FromFileName(fileName)));
    }

    [TestMethod]
    [DataRow("NotFWMF.esp")]
    [DataRow("MyFWMFPatch.esp")]
    [DataRow("FWMFake.esp")]
    [DataRow("OrdinaryMapMod.esp")]
    public void DoesNotTreatIncidentalFwmfTextAsFamilyProvenance(
        string fileName)
    {
        Assert.IsFalse(
            FwmfEncounterZoneForwarder.IsFwmfFamilyPlugin(
                ModKey.FromFileName(fileName)));
    }

    [TestMethod]
    public void CellUsesNearestEarlierValidAssignment()
    {
        SkyrimMod baseMod = NewMod("Base.esm");
        EncounterZone baseZone = baseMod.EncounterZones.AddNew();
        Cell baseCell = AddInteriorCell(baseMod);
        baseCell.EncounterZone.SetTo(baseZone.FormKey);

        SkyrimMod middle = NewMod("Middle.esp");
        EncounterZone middleZone = middle.EncounterZones.AddNew();
        Cell middleCell = AddInteriorOverride(middle, baseCell.FormKey);
        middleCell.EncounterZone.SetTo(middleZone.FormKey);

        SkyrimMod fwmf = NewMod("FWMF for Fantasy Paper Maps.esp");
        _ = AddInteriorOverride(fwmf, baseCell.FormKey);
        ILinkCache<ISkyrimMod, ISkyrimModGetter> cache =
            ToCache(baseMod, middle, fwmf);

        FormKey? result = FwmfEncounterZoneForwarder
            .FindNearestEarlierCellEncounterZone(
                cache,
                baseCell.FormKey,
                fwmf.ModKey);

        Assert.AreEqual(middleZone.FormKey, result);
    }

    [TestMethod]
    public void CellSkipsUnresolvedEarlierAssignmentAndUsesNextValidOne()
    {
        SkyrimMod baseMod = NewMod("Base.esm");
        EncounterZone baseZone = baseMod.EncounterZones.AddNew();
        Cell baseCell = AddInteriorCell(baseMod);
        baseCell.EncounterZone.SetTo(baseZone.FormKey);

        SkyrimMod middle = NewMod("Middle.esp");
        Cell middleCell = AddInteriorOverride(middle, baseCell.FormKey);
        middleCell.EncounterZone.SetTo(
            new FormKey(ModKey.FromFileName("Missing.esp"), 0x1234));

        SkyrimMod fwmf = NewMod("FWMF.esp");
        _ = AddInteriorOverride(fwmf, baseCell.FormKey);
        ILinkCache<ISkyrimMod, ISkyrimModGetter> cache =
            ToCache(baseMod, middle, fwmf);

        FormKey? result = FwmfEncounterZoneForwarder
            .FindNearestEarlierCellEncounterZone(
                cache,
                baseCell.FormKey,
                fwmf.ModKey);

        Assert.AreEqual(baseZone.FormKey, result);
    }

    [TestMethod]
    public void CellSkipsDeletedEarlierOverrideAndUsesNextValidOne()
    {
        SkyrimMod baseMod = NewMod("Base.esm");
        EncounterZone baseZone = baseMod.EncounterZones.AddNew();
        Cell baseCell = AddInteriorCell(baseMod);
        baseCell.EncounterZone.SetTo(baseZone.FormKey);

        SkyrimMod middle = NewMod("Middle.esp");
        Cell middleCell = AddInteriorOverride(middle, baseCell.FormKey);
        middleCell.IsDeleted = true;
        middleCell.EncounterZone.SetTo(baseZone.FormKey);

        SkyrimMod fwmf = NewMod("FWMF.esp");
        _ = AddInteriorOverride(fwmf, baseCell.FormKey);
        ILinkCache<ISkyrimMod, ISkyrimModGetter> cache =
            ToCache(baseMod, middle, fwmf);

        FormKey? result = FwmfEncounterZoneForwarder
            .FindNearestEarlierCellEncounterZone(
                cache,
                baseCell.FormKey,
                fwmf.ModKey);

        Assert.AreEqual(baseZone.FormKey, result);
    }

    [TestMethod]
    public void DeletedEncounterZoneTargetIsNotForwarded()
    {
        SkyrimMod baseMod = NewMod("Base.esm");
        EncounterZone baseZone = baseMod.EncounterZones.AddNew();
        Cell baseCell = AddInteriorCell(baseMod);
        baseCell.EncounterZone.SetTo(baseZone.FormKey);

        SkyrimMod fwmf = NewMod("FWMF.esp");
        _ = AddInteriorOverride(fwmf, baseCell.FormKey);
        var deletedZone = new EncounterZone(
            baseZone.FormKey,
            SkyrimRelease.SkyrimSE)
        {
            IsDeleted = true,
        };
        fwmf.EncounterZones.Add(deletedZone);
        ILinkCache<ISkyrimMod, ISkyrimModGetter> cache =
            ToCache(baseMod, fwmf);

        FormKey? result = FwmfEncounterZoneForwarder
            .FindNearestEarlierCellEncounterZone(
                cache,
                baseCell.FormKey,
                fwmf.ModKey);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void WorldspaceUsesNearestEarlierValidAssignment()
    {
        SkyrimMod baseMod = NewMod("Base.esm");
        EncounterZone baseZone = baseMod.EncounterZones.AddNew();
        Worldspace baseWorld = baseMod.Worldspaces.AddNew();
        baseWorld.EncounterZone.SetTo(baseZone.FormKey);

        SkyrimMod middle = NewMod("Middle.esp");
        EncounterZone middleZone = middle.EncounterZones.AddNew();
        var middleWorld = new Worldspace(
            baseWorld.FormKey,
            SkyrimRelease.SkyrimSE);
        middleWorld.EncounterZone.SetTo(middleZone.FormKey);
        middle.Worldspaces.Add(middleWorld);

        SkyrimMod fwmf = NewMod("FWMF.esp");
        fwmf.Worldspaces.Add(new Worldspace(
            baseWorld.FormKey,
            SkyrimRelease.SkyrimSE));
        ILinkCache<ISkyrimMod, ISkyrimModGetter> cache =
            ToCache(baseMod, middle, fwmf);

        FormKey? result = FwmfEncounterZoneForwarder
            .FindNearestEarlierWorldspaceEncounterZone(
                cache,
                baseWorld.FormKey,
                fwmf.ModKey);

        Assert.AreEqual(middleZone.FormKey, result);
    }

    private static ILinkCache<ISkyrimMod, ISkyrimModGetter> ToCache(
        params ISkyrimModGetter[] listedOrder)
    {
        return listedOrder.ToImmutableLinkCache();
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

    private static Cell AddInteriorOverride(SkyrimMod mod, FormKey formKey)
    {
        var cell = new Cell(formKey, SkyrimRelease.SkyrimSE)
        {
            Flags = Cell.Flag.IsInteriorCell,
        };
        mod.Cells.AddInteriorCell(cell);
        return cell;
    }
}
