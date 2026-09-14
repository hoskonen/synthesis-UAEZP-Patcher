namespace UAEZPSynthesisPatcher;

public enum DummyZoneMode
{
    DeterministicRandom,
    First,
}

public sealed class Settings
{
    public bool AssignMissingCellEncounterZones { get; set; } = true;
    public bool AssignMissingWorldspaceEncounterZones { get; set; } = true;
    public bool DisableCombatBoundaries { get; set; } = true;
    public DummyZoneMode DummyZoneMode { get; set; } = DummyZoneMode.DeterministicRandom;
    public int Seed { get; set; } = 38174;
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

public static class MilestoneGuard
{
    public static void EnsureReadOnly(Settings settings)
    {
        SettingsValidator.Validate(settings);

        if (!settings.DryRun)
        {
            throw new InvalidOperationException(
                "DryRun=false is not supported in milestone 1. " +
                "No records were modified; record mutation has not been implemented.");
        }
    }
}
