using Mutagen.Bethesda.Plugins;

namespace UAEZPSynthesisPatcher.Tests;

[TestClass]
public sealed class EncounterZoneDifficultyResolverTests
{
    [TestMethod]
    public void DefaultProfileMatchesValidatedSourceAndPreservesAssignment()
    {
        var settings = new Settings();
        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate([TestData.EasySource()]);
        var target = new FormKey(TestData.SkyrimModKey, 0x123456);

        EncounterZoneDifficultyProfile profile =
            EncounterZoneDifficultyResolver.Resolve(
                settings.DifficultyProfile,
                source);
        ValidatedDummyZone selected = DeterministicDummyZoneSelector.Select(
            settings.DummyZoneMode,
            settings.Seed,
            target,
            source.DummyZones);

        Assert.AreEqual(
            EncounterZoneDifficultyProfileSelection.MatchValidatedSource,
            settings.DifficultyProfile);
        Assert.AreEqual("UAEZP Easy", profile.DisplayName);
        Assert.AreEqual(0x807u, selected.FormKey.ID);
    }

    [TestMethod]
    public void EasyProfileMapsToAuditedMinimumLevels()
    {
        CollectionAssert.AreEqual(
            new byte[] { 3, 5, 7, 9, 11, 11, 13, 15, 17 },
            EncounterZoneDifficultyResolver.Easy.Tiers
                .Select(tier => tier.MinimumLevel)
                .ToArray());
    }

    [TestMethod]
    public void HardProfileMapsToAuditedMinimumLevels()
    {
        CollectionAssert.AreEqual(
            new byte[] { 10, 15, 20, 25, 30, 35, 40, 45, 50 },
            EncounterZoneDifficultyResolver.Hard.Tiers
                .Select(tier => tier.MinimumLevel)
                .ToArray());
    }

    [TestMethod]
    public void InvalidCustomTierDataIsRejectedClearly()
    {
        InvalidOperationException wrongCount =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                EncounterZoneDifficultyResolver.CreateCustom(
                    [1, 2, 3, 4, 5, 6, 7, 8]));
        InvalidOperationException unordered =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                EncounterZoneDifficultyResolver.CreateCustom(
                    [1, 2, 3, 4, 3, 6, 7, 8, 9]));

        StringAssert.Contains(wrongCount.Message, "exactly 9 ordered tiers");
        StringAssert.Contains(unordered.Message, "nondecreasing");
    }

    [TestMethod]
    public void UnconfiguredCustomSelectionIsRejectedClearly()
    {
        var settings = new Settings
        {
            DifficultyProfile =
                EncounterZoneDifficultyProfileSelection.Custom,
        };

        InvalidOperationException error =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                SettingsValidator.Validate(settings));

        StringAssert.Contains(
            error.Message,
            "Custom difficulty profile data is not configured");
    }

    [TestMethod]
    public void ExplicitProfileMustMatchExistingSourceRecords()
    {
        ValidatedSourcePlugin hardSource =
            SourcePluginValidator.Validate([TestData.HardSource()]);

        InvalidOperationException error =
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                EncounterZoneDifficultyResolver.Resolve(
                    EncounterZoneDifficultyProfileSelection.UAEZPEasy,
                    hardSource));

        StringAssert.Contains(error.Message, "does not match");
        StringAssert.Contains(error.Message, "does not generate ECZN level overrides");
    }

    [TestMethod]
    public void DryRunReportsActiveDifficultyProfile()
    {
        var settings = new Settings
        {
            DifficultyProfile =
                EncounterZoneDifficultyProfileSelection.UAEZPHard,
        };
        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate([TestData.HardSource()]);
        PatchPlan plan = ReadOnlyPlanner.Build(
            settings,
            source.DummyZones,
            [],
            [],
            []);
        EncounterZoneDifficultyProfile difficulty =
            EncounterZoneDifficultyResolver.Resolve(
                settings.DifficultyProfile,
                source);
        var run = new PlanningRun(source, plan)
        {
            DifficultyProfile = difficulty,
        };

        string report = DryRunReport.Render(run, settings);

        StringAssert.Contains(report, "Difficulty profile: UAEZP Hard");
    }
}
