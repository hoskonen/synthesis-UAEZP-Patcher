using System.Runtime.Serialization;
using Mutagen.Bethesda.Synthesis.Settings;
using Newtonsoft.Json;

namespace UAEZPSynthesisPatcher;

public enum DummyZoneMode
{
    DeterministicRandom,
    First,
}

public sealed class Settings
{
    private EncounterZoneAssignmentSettings _encounterZoneAssignment = new();
    private EncounterZoneDifficultySettings _encounterZoneDifficulty = new();
    private EncounterZoneBehaviorSettings _encounterZoneBehavior = new();
    private CompatibilitySettings _compatibility = new();
    private TestingSettings _testing = new();

    private bool _hasCanonicalEncounterZoneAssignment;
    private bool _hasCanonicalEncounterZoneDifficulty;
    private bool _hasCanonicalEncounterZoneBehavior;
    private bool _hasCanonicalCompatibility;
    private bool _hasCanonicalTesting;

    private bool? _legacyAssignMissingCellEncounterZones;
    private bool? _legacyAssignMissingWorldspaceEncounterZones;
    private DummyZoneMode? _legacyDummyZoneMode;
    private int? _legacySeed;
    private EncounterZoneDifficultyProfileSelection? _legacyDifficultyProfile;
    private bool? _legacyDisableCombatBoundaries;
    private bool? _legacyForwardEncounterZonesThroughFwmf;
    private bool? _legacyDryRun;

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    [SynthesisSettingName("Encounter Zone Assignment")]
    public EncounterZoneAssignmentSettings EncounterZoneAssignment
    {
        get => _encounterZoneAssignment;
        set
        {
            _encounterZoneAssignment = value ?? new();
            _hasCanonicalEncounterZoneAssignment = true;
        }
    }

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    [SynthesisSettingName("Encounter Zone Difficulty")]
    public EncounterZoneDifficultySettings EncounterZoneDifficulty
    {
        get => _encounterZoneDifficulty;
        set
        {
            _encounterZoneDifficulty = value ?? new();
            _hasCanonicalEncounterZoneDifficulty = true;
        }
    }

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    [SynthesisSettingName("Encounter Zone Behavior")]
    public EncounterZoneBehaviorSettings EncounterZoneBehavior
    {
        get => _encounterZoneBehavior;
        set
        {
            _encounterZoneBehavior = value ?? new();
            _hasCanonicalEncounterZoneBehavior = true;
        }
    }

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    [SynthesisSettingName("Compatibility")]
    public CompatibilitySettings Compatibility
    {
        get => _compatibility;
        set
        {
            _compatibility = value ?? new();
            _hasCanonicalCompatibility = true;
        }
    }

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    [SynthesisSettingName("Testing")]
    public TestingSettings Testing
    {
        get => _testing;
        set
        {
            _testing = value ?? new();
            _hasCanonicalTesting = true;
        }
    }

    [JsonIgnore]
    [SynthesisIgnoreSetting]
    public bool AssignMissingCellEncounterZones
    {
        get => EncounterZoneAssignment.AssignMissingCellEncounterZones;
        set => EncounterZoneAssignment.AssignMissingCellEncounterZones = value;
    }

    [JsonIgnore]
    [SynthesisIgnoreSetting]
    public bool AssignMissingWorldspaceEncounterZones
    {
        get => EncounterZoneAssignment.AssignMissingWorldspaceEncounterZones;
        set => EncounterZoneAssignment.AssignMissingWorldspaceEncounterZones = value;
    }

    [JsonIgnore]
    [SynthesisIgnoreSetting]
    public bool DisableCombatBoundaries
    {
        get => EncounterZoneBehavior.DisableCombatBoundaries;
        set => EncounterZoneBehavior.DisableCombatBoundaries = value;
    }

    [JsonIgnore]
    [SynthesisIgnoreSetting]
    public bool ForwardEncounterZonesThroughFwmf
    {
        get => Compatibility.ForwardEncounterZonesThroughFwmf;
        set => Compatibility.ForwardEncounterZonesThroughFwmf = value;
    }

    [JsonIgnore]
    [SynthesisIgnoreSetting]
    public EncounterZoneDifficultyProfileSelection DifficultyProfile
    {
        get => EncounterZoneDifficulty.DifficultyProfile;
        set => EncounterZoneDifficulty.DifficultyProfile = value;
    }

    [JsonIgnore]
    [SynthesisIgnoreSetting]
    public DummyZoneMode DummyZoneMode
    {
        get => EncounterZoneAssignment.DummyZoneMode;
        set => EncounterZoneAssignment.DummyZoneMode = value;
    }

    [JsonIgnore]
    [SynthesisIgnoreSetting]
    public int Seed
    {
        get => EncounterZoneAssignment.Seed;
        set => EncounterZoneAssignment.Seed = value;
    }

    [JsonIgnore]
    [SynthesisIgnoreSetting]
    public bool DryRun
    {
        get => Testing.DryRun;
        set => Testing.DryRun = value;
    }

    [JsonProperty(nameof(AssignMissingCellEncounterZones))]
    private bool LegacyAssignMissingCellEncounterZones
    {
        set => _legacyAssignMissingCellEncounterZones = value;
    }

    [JsonProperty(nameof(AssignMissingWorldspaceEncounterZones))]
    private bool LegacyAssignMissingWorldspaceEncounterZones
    {
        set => _legacyAssignMissingWorldspaceEncounterZones = value;
    }

    [JsonProperty(nameof(DummyZoneMode))]
    private DummyZoneMode LegacyDummyZoneMode
    {
        set => _legacyDummyZoneMode = value;
    }

    [JsonProperty(nameof(Seed))]
    private int LegacySeed
    {
        set => _legacySeed = value;
    }

    [JsonProperty(nameof(DifficultyProfile))]
    private EncounterZoneDifficultyProfileSelection LegacyDifficultyProfile
    {
        set => _legacyDifficultyProfile = value;
    }

    [JsonProperty(nameof(DisableCombatBoundaries))]
    private bool LegacyDisableCombatBoundaries
    {
        set => _legacyDisableCombatBoundaries = value;
    }

    [JsonProperty(nameof(ForwardEncounterZonesThroughFwmf))]
    private bool LegacyForwardEncounterZonesThroughFwmf
    {
        set => _legacyForwardEncounterZonesThroughFwmf = value;
    }

    [JsonProperty(nameof(DryRun))]
    private bool LegacyDryRun
    {
        set => _legacyDryRun = value;
    }

    [OnDeserialized]
    private void ApplyLegacySettings(StreamingContext _)
    {
        if (!_hasCanonicalEncounterZoneAssignment)
        {
            EncounterZoneAssignment.AssignMissingCellEncounterZones =
                _legacyAssignMissingCellEncounterZones ??
                EncounterZoneAssignment.AssignMissingCellEncounterZones;
            EncounterZoneAssignment.AssignMissingWorldspaceEncounterZones =
                _legacyAssignMissingWorldspaceEncounterZones ??
                EncounterZoneAssignment.AssignMissingWorldspaceEncounterZones;
            EncounterZoneAssignment.DummyZoneMode =
                _legacyDummyZoneMode ?? EncounterZoneAssignment.DummyZoneMode;
            EncounterZoneAssignment.Seed =
                _legacySeed ?? EncounterZoneAssignment.Seed;
        }

        if (!_hasCanonicalEncounterZoneDifficulty)
        {
            EncounterZoneDifficulty.DifficultyProfile =
                _legacyDifficultyProfile ??
                EncounterZoneDifficulty.DifficultyProfile;
        }

        if (!_hasCanonicalEncounterZoneBehavior)
        {
            EncounterZoneBehavior.DisableCombatBoundaries =
                _legacyDisableCombatBoundaries ??
                EncounterZoneBehavior.DisableCombatBoundaries;
        }

        if (!_hasCanonicalCompatibility)
        {
            Compatibility.ForwardEncounterZonesThroughFwmf =
                _legacyForwardEncounterZonesThroughFwmf ??
                Compatibility.ForwardEncounterZonesThroughFwmf;
        }

        if (!_hasCanonicalTesting)
        {
            Testing.DryRun = _legacyDryRun ?? Testing.DryRun;
        }
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

public sealed class EncounterZoneDifficultySettings
{
    [SynthesisSettingName("Difficulty Profile")]
    [SynthesisTooltip(
        "Defines the numeric minimum levels of the nine ordered UAEZP zone " +
        "tiers. Match Validated Source preserves the active UAEZP.esp values.")]
    public EncounterZoneDifficultyProfileSelection DifficultyProfile { get; set; } =
        EncounterZoneDifficultyProfileSelection.MatchValidatedSource;
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

        if (!Enum.IsDefined(settings.DifficultyProfile))
        {
            throw new InvalidOperationException(
                $"Unsupported difficulty profile value: " +
                $"{(int)settings.DifficultyProfile}.");
        }

        if (settings.DifficultyProfile ==
            EncounterZoneDifficultyProfileSelection.Custom)
        {
            throw new InvalidOperationException(
                "Custom difficulty profile data is not configured. Use Match " +
                "Validated Source, UAEZP Easy, or UAEZP Hard.");
        }
    }
}
