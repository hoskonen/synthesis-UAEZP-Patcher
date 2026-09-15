using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace UAEZPSynthesisPatcher;

public enum PlannedRecordType
{
    Cell,
    Worldspace,
    EncounterZone,
}

public sealed record DummyZoneDefinition(
    FormKey FormKey,
    string EditorId,
    byte MinLevel,
    byte MaxLevel,
    byte Rank,
    FormKey Owner,
    FormKey Location,
    EncounterZone.Flag Flags,
    bool IsDeleted);

public sealed record ValidatedDummyZone(
    FormKey FormKey,
    string EditorId,
    byte MinLevel,
    byte MaxLevel,
    EncounterZone.Flag Flags);

public sealed record SourcePluginCandidate(
    ModKey ModKey,
    IReadOnlyList<DummyZoneDefinition> DummyZones);

public sealed record ValidatedSourcePlugin(
    ModKey ModKey,
    string Variant,
    IReadOnlyList<ValidatedDummyZone> DummyZones);

public sealed record RecordSnapshot(
    PlannedRecordType RecordType,
    FormKey FormKey,
    string? EditorId,
    ModKey WinningModKey,
    bool IsDeleted,
    bool RequirementSatisfied,
    bool HasUnresolvedRequirementReference = false)
{
    public FormKey? EncounterZoneTarget { get; init; }
    public string? ResolvedEncounterZoneEditorId { get; init; }
}

public sealed record ExistingLinkSample(
    FormKey FormKey,
    string? EditorId,
    ModKey WinningModKey,
    FormKey? EncounterZoneTarget,
    string? ResolvedEncounterZoneEditorId,
    bool IsResolved);

public sealed record ExistingLinkProvenance(
    int ResolvedLinks,
    int UnresolvedLinks,
    IReadOnlyDictionary<ModKey, int> ByWinningPlugin,
    int UaeZpDummyZoneLinks,
    IReadOnlyDictionary<ModKey, int> UaeZpDummyZoneLinksByWinningPlugin,
    IReadOnlyList<ExistingLinkSample> SuspiciousSamples);

public sealed record ExistingFlagProvenance(
    IReadOnlyDictionary<ModKey, int> ByWinningPlugin);

public sealed record ExistingStateProvenance(
    ExistingLinkProvenance Cells,
    ExistingLinkProvenance Worldspaces,
    ExistingFlagProvenance EncounterZones);

public sealed record PlannedChange(
    RecordSnapshot Target,
    ValidatedDummyZone? AssignedDummyZone);

public sealed record ApplyContextCatalog(
    IReadOnlyDictionary<FormKey, Func<ISkyrimMod, ICell>> Cells,
    IReadOnlyDictionary<FormKey, Func<ISkyrimMod, IWorldspace>> Worldspaces,
    IReadOnlyDictionary<FormKey, Func<ISkyrimMod, IEncounterZone>> EncounterZones);

public sealed record ApplyResult(
    int CellsApplied,
    int WorldspacesApplied,
    int EncounterZonesApplied)
{
    public int TotalApplied =>
        CellsApplied + WorldspacesApplied + EncounterZonesApplied;
}

public sealed record RecordPlanSummary(
    int WinningRecordsScanned,
    int MissingRequirement,
    int ExistingRequirement,
    int DeletedSkipped,
    int UnresolvedExistingReferences,
    int PlannedOverrides);

public sealed record PatchPlan(
    RecordPlanSummary Cells,
    RecordPlanSummary Worldspaces,
    RecordPlanSummary EncounterZones,
    IReadOnlyList<PlannedChange> Changes,
    IReadOnlyDictionary<FormKey, int> AssignmentDistribution,
    IReadOnlyDictionary<ModKey, int> CellAndWorldspaceChangesByOriginPlugin)
{
    public required ExistingStateProvenance ExistingStateProvenance { get; init; }

    public int TotalPlannedOverrides =>
        Cells.PlannedOverrides + Worldspaces.PlannedOverrides + EncounterZones.PlannedOverrides;

    public int TotalDummyAssignments =>
        Cells.PlannedOverrides + Worldspaces.PlannedOverrides;
}

public sealed record PlanningRun(
    ValidatedSourcePlugin SourcePlugin,
    PatchPlan Plan)
{
    public ApplyContextCatalog? ApplyContexts { get; init; }
}
