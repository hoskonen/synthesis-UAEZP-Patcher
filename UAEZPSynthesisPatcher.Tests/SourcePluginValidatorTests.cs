using Mutagen.Bethesda.Plugins;

namespace UAEZPSynthesisPatcher.Tests;

[TestClass]
public sealed class SourcePluginValidatorTests
{
    [TestMethod]
    public void EasyAndHardSchemasAreRecognized()
    {
        Assert.AreEqual(
            "Easy",
            SourcePluginValidator.Validate([TestData.EasySource()]).Variant);
        Assert.AreEqual(
            "Hard",
            SourcePluginValidator.Validate([TestData.HardSource()]).Variant);
    }

    [TestMethod]
    public void MissingSourcePluginFailsClearly()
    {
        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => SourcePluginValidator.Validate([]));

        StringAssert.Contains(error.Message, "not active");
        StringAssert.Contains(error.Message, "UAEZP.esp");
    }

    [TestMethod]
    public void MultipleSourceVariantsFailClearly()
    {
        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => SourcePluginValidator.Validate(
                [TestData.EasySource(), TestData.HardSource()]));

        StringAssert.Contains(error.Message, "mutually exclusive");
        StringAssert.Contains(error.Message, "exactly one");
    }

    [TestMethod]
    public void ZeroDummyZonesFailsClearly()
    {
        SourcePluginCandidate source = TestData.EasySource() with { DummyZones = [] };

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => SourcePluginValidator.Validate([source]));

        StringAssert.Contains(error.Message, "requires exactly 9");
    }

    [TestMethod]
    public void DuplicateEditorIdsFailClearly()
    {
        List<DummyZoneDefinition> zones = TestData.EasySource().DummyZones.ToList();
        zones[1] = zones[1] with { EditorId = zones[0].EditorId };
        SourcePluginCandidate source = TestData.EasySource() with { DummyZones = zones };

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => SourcePluginValidator.Validate([source]));

        StringAssert.Contains(error.Message, "duplicate dummy-zone EditorIDs");
    }

    [TestMethod]
    public void DuplicateFormKeysFailClearly()
    {
        List<DummyZoneDefinition> zones = TestData.EasySource().DummyZones.ToList();
        zones[1] = zones[1] with { FormKey = zones[0].FormKey };
        SourcePluginCandidate source = TestData.EasySource() with { DummyZones = zones };

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => SourcePluginValidator.Validate([source]));

        StringAssert.Contains(error.Message, "duplicate dummy-zone FormKeys");
    }

    [TestMethod]
    public void MalformedSchemaFailsClearly()
    {
        List<DummyZoneDefinition> zones = TestData.EasySource().DummyZones.ToList();
        zones[4] = zones[4] with { MaxLevel = 99 };
        SourcePluginCandidate source = TestData.EasySource() with { DummyZones = zones };

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => SourcePluginValidator.Validate([source]));

        StringAssert.Contains(error.Message, "schema mismatch");
        StringAssert.Contains(error.Message, "DummyEncounterZone4");
    }

    [TestMethod]
    public void UnknownMinimumLevelVectorFailsClearly()
    {
        List<DummyZoneDefinition> zones = TestData.EasySource().DummyZones.ToList();
        zones[8] = zones[8] with { MinLevel = 18 };
        SourcePluginCandidate source = TestData.EasySource() with { DummyZones = zones };

        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => SourcePluginValidator.Validate([source]));

        StringAssert.Contains(error.Message, "do not match the audited Easy or Hard schema");
    }

    [TestMethod]
    public void StableOrderingUsesLocalFormId()
    {
        SourcePluginCandidate source = TestData.EasySource() with
        {
            DummyZones = TestData.EasySource().DummyZones
                .OrderByDescending(zone => zone.FormKey.ID)
                .ToList(),
        };

        ValidatedSourcePlugin validated = SourcePluginValidator.Validate([source]);

        CollectionAssert.AreEqual(
            Enumerable.Range(0, 9).Select(index => 0x800u + (uint)index).ToList(),
            validated.DummyZones.Select(zone => zone.FormKey.ID).ToList());
    }
}
