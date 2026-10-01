using Mutagen.Bethesda.Synthesis.Settings;

namespace UAEZPSynthesisPatcher;

public enum DummyZoneMode
{
    DeterministicRandom,
    First,
}

public sealed class Settings
{
    [Newtonsoft.Json.JsonIgnore]
    [SynthesisSettingName("Encounter Zone Assignment")]
    public EncounterZoneAssignmentSettings EncounterZoneAssignment { get; set; } =
        new();

    [Newtonsoft.Json.JsonIgnore]
    [SynthesisSettingName("Encounter Zone Behavior")]
    public EncounterZoneBehaviorSettings EncounterZoneBehavior { get; set; } =
        new();

    [Newtonsoft.Json.JsonIgnore]
    [SynthesisSettingName("Compatibility")]
    public CompatibilitySettings Compatibility { get; set; } = new();

    [Newtonsoft.Json.JsonIgnore]
    [SynthesisSettingName("Testing")]
    public TestingSettings Testing { get; set; } = new();

    [SynthesisIgnoreSetting]
    public bool AssignMissingCellEncounterZones
    {
        get => EncounterZoneAssignment.AssignMissingCellEncounterZones;
        set => EncounterZoneAssignment.AssignMissingCellEncounterZones = value;
    }

    [SynthesisIgnoreSetting]
    public bool AssignMissingWorldspaceEncounterZones
    {
        get => EncounterZoneAssignment.AssignMissingWorldspaceEncounterZones;
        set => EncounterZoneAssignment.AssignMissingWorldspaceEncounterZones = value;
    }

    [SynthesisIgnoreSetting]
    public bool DisableCombatBoundaries
    {
        get => EncounterZoneBehavior.DisableCombatBoundaries;
        set => EncounterZoneBehavior.DisableCombatBoundaries = value;
    }

    [SynthesisIgnoreSetting]
    public bool ForwardEncounterZonesThroughFwmf
    {
        get => Compatibility.ForwardEncounterZonesThroughFwmf;
        set => Compatibility.ForwardEncounterZonesThroughFwmf = value;
    }

    [SynthesisIgnoreSetting]
    public DummyZoneMode DummyZoneMode
    {
        get => EncounterZoneAssignment.DummyZoneMode;
        set => EncounterZoneAssignment.DummyZoneMode = value;
    }

    [SynthesisIgnoreSetting]
    public int Seed
    {
        get => EncounterZoneAssignment.Seed;
        set => EncounterZoneAssignment.Seed = value;
    }

    [SynthesisIgnoreSetting]
    public bool DryRun
    {
        get => Testing.DryRun;
        set => Testing.DryRun = value;
    }
}

public sealed class EncounterZoneAssignmentSettings
{
    [SynthesisSettingName("Assign Missing Cell Encounter Zones")]
    [SynthesisTooltip(
        "Assigns UAEZP dummy encounter zones to CELL records that currently " +
        "have no XEZN.")]
    public bool AssignMissingCellEncounterZones { get; set; } = true;

    [SynthesisSettingName("Assign Missing Worldspace Encounter Zones")]
    [SynthesisTooltip(
        "Assigns UAEZP dummy encounter zones to WRLD records that currently " +
        "have no XEZN.")]
    public bool AssignMissingWorldspaceEncounterZones { get; set; } = true;

    [SynthesisSettingName("Dummy Zone Mode")]
    [SynthesisTooltip("Controls how missing encounter zones are selected.")]
    public DummyZoneMode DummyZoneMode { get; set; } =
        DummyZoneMode.DeterministicRandom;

    [SynthesisSettingName("Seed")]
    [SynthesisTooltip(
        "Controls deterministic random assignment. The same seed and load " +
        "order produce stable results.")]
    public int Seed { get; set; } = 38174;
}

public sealed class EncounterZoneBehaviorSettings
{
    [SynthesisSettingName("Disable Combat Boundaries")]
    [SynthesisTooltip(
        "Adds Disable Combat Boundary to encounter-zone records while " +
        "preserving existing flags.")]
    public bool DisableCombatBoundaries { get; set; } = true;
}

public sealed class CompatibilitySettings
{
    [SynthesisSettingName("Forward encounter zones through FWMF")]
    [SynthesisTooltip(
        "Restores the nearest earlier valid XEZN when an FWMF-family winning " +
        "override removes it. Intended primarily for a separate late " +
        "Synthesis group placed after FWMF and its patches.")]
    public bool ForwardEncounterZonesThroughFwmf { get; set; }
}

public sealed class TestingSettings
{
    [SynthesisSettingName("Dry Run")]
    [SynthesisTooltip(
        "Plans and reports changes without writing functional Skyrim " +
        "overrides.")]
    public bool DryRun { get; set; } = true;
}

public static class SettingsValidator
{
    public static void Validate(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!Enum.IsDefined(settings.DummyZoneMode))
        {
            throw new InvalidOperationException(
                $"Unsupported dummy-zone mode value: {(int)settings.DummyZoneMode}.");
        }
    }
}
