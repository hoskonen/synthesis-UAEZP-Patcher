using Mutagen.Bethesda.Plugins;

namespace UAEZPSynthesisPatcher.Tests;

[TestClass]
public sealed class DeterministicDummyZoneSelectorTests
{
    [TestMethod]
    public void SameSeedAndFormKeyReturnSameZone()
    {
        var zones = TestData.ValidatedZones();
        var target = new FormKey(TestData.SkyrimModKey, 0x123456);

        ValidatedDummyZone first = DeterministicDummyZoneSelector.Select(
            DummyZoneMode.DeterministicRandom, 38174, target, zones);
        ValidatedDummyZone second = DeterministicDummyZoneSelector.Select(
            DummyZoneMode.DeterministicRandom, 38174, target, zones);

        Assert.AreEqual(first.FormKey, second.FormKey);
    }

    [TestMethod]
    public void GoldenVectorsFreezeV1Serialization()
    {
        Assert.AreEqual(
            "A0A162C4669D6BBB6B6B952E961BBC34632A5E50BFAD929370C2EACA2464620A",
            Convert.ToHexString(DeterministicDummyZoneSelector.ComputeV1Digest(
                38174,
                new FormKey(ModKey.FromFileName("Skyrim.esm"), 0x123456))));
        Assert.AreEqual(
            "2CAB8AF128D5B506FA5EAC7B14E4E00B8E6FDD437D02C6AC930CD453CADC9309",
            Convert.ToHexString(DeterministicDummyZoneSelector.ComputeV1Digest(
                -1,
                new FormKey(ModKey.FromFileName("Dawnguard.esm"), 0x00ABCD))));
        Assert.AreEqual(
            "5959EC09714B80C03EB2E9A2348CA7C9D4765BE1B7FE6A3A4E391585A48EF66C",
            Convert.ToHexString(DeterministicDummyZoneSelector.ComputeV1Digest(
                0,
                new FormKey(ModKey.FromFileName("Some Mod.esp"), 0x000001))));

        Assert.AreEqual(
            7,
            DeterministicDummyZoneSelector.GetV1Index(
                38174,
                new FormKey(ModKey.FromFileName("Skyrim.esm"), 0x123456),
                9));
        Assert.AreEqual(
            2,
            DeterministicDummyZoneSelector.GetV1Index(
                -1,
                new FormKey(ModKey.FromFileName("Dawnguard.esm"), 0x00ABCD),
                9));
        Assert.AreEqual(
            1,
            DeterministicDummyZoneSelector.GetV1Index(
                0,
                new FormKey(ModKey.FromFileName("Some Mod.esp"), 0x000001),
                9));
    }

    [TestMethod]
    public void EnumerationOrderDoesNotAffectAssignments()
    {
        var targets = Enumerable.Range(1, 25)
            .Select(id => new FormKey(TestData.SkyrimModKey, (uint)id))
            .ToList();
        var zones = TestData.ValidatedZones();

        Dictionary<FormKey, FormKey> forward = Assign(targets, zones, 38174);
        targets.Reverse();
        Dictionary<FormKey, FormKey> reverse = Assign(targets, zones, 38174);

        CollectionAssert.AreEquivalent(forward.ToList(), reverse.ToList());
    }

    [TestMethod]
    public void InsertingUnrelatedRecordDoesNotChangeExistingAssignments()
    {
        var targets = Enumerable.Range(1, 25)
            .Select(id => new FormKey(TestData.SkyrimModKey, (uint)id))
            .ToList();
        var zones = TestData.ValidatedZones();
        Dictionary<FormKey, FormKey> before = Assign(targets, zones, 38174);

        targets.Insert(8, new FormKey(TestData.SkyrimModKey, 0xABCDEF));
        Dictionary<FormKey, FormKey> after = Assign(targets, zones, 38174);

        foreach ((FormKey target, FormKey assignment) in before)
        {
            Assert.AreEqual(assignment, after[target]);
        }
    }

    [TestMethod]
    public void DifferentSeedsChangeSomeAssignments()
    {
        var targets = Enumerable.Range(1, 100)
            .Select(id => new FormKey(TestData.SkyrimModKey, (uint)id))
            .ToList();
        var zones = TestData.ValidatedZones();
        Dictionary<FormKey, FormKey> first = Assign(targets, zones, 38174);
        Dictionary<FormKey, FormKey> second = Assign(targets, zones, 91823);

        Assert.IsTrue(targets.Any(target => first[target] != second[target]));
    }

    [TestMethod]
    public void FirstModeUsesFormKeySortedFirstZone()
    {
        SourcePluginCandidate shuffled = TestData.EasySource() with
        {
            DummyZones = TestData.EasySource().DummyZones.Reverse().ToList(),
        };
        ValidatedSourcePlugin source = SourcePluginValidator.Validate([shuffled]);

        ValidatedDummyZone selected = DeterministicDummyZoneSelector.Select(
            DummyZoneMode.First,
            38174,
            new FormKey(TestData.SkyrimModKey, 1),
            source.DummyZones);

        Assert.AreEqual(0x800u, selected.FormKey.ID);
    }

    private static Dictionary<FormKey, FormKey> Assign(
        IEnumerable<FormKey> targets,
        IReadOnlyList<ValidatedDummyZone> zones,
        int seed)
    {
        return targets.ToDictionary(
            target => target,
            target => DeterministicDummyZoneSelector.Select(
                DummyZoneMode.DeterministicRandom,
                seed,
                target,
                zones).FormKey);
    }
}
