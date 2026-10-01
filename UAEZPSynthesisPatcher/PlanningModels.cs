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
    public FormKey? EarlierResolvableEncounterZoneTarget { get; init; }
    public byte? EncounterZoneMinimumLevel { get; init; }
    public DungeonCategory DungeonCategory { get; init; }
    public bool IsInteriorCell { get; init; }
    public FormKey? LocationFormKey { get; init; }
    public string? LocationEditorId { get; init; }
    public IReadOnlyList<LocationKeywordDiagnostic> LocationKeywords
        { get; init; } = [];
    public FormKey? ParentLocationFormKey { get; init; }
    public string? ParentLocationEditorId { get; init; }
    public IReadOnlyList<LocationKeywordDiagnostic> ParentLocationKeywords
        { get; init; } = [];
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
    ValidatedDummyZone? AssignedDummyZone)
{
    public FormKey? ForwardedEncounterZone { get; init; }
    public bool AddDisableCombatBoundary { get; init; }
    public byte? DesiredEncounterZoneMinimumLevel { get; init; }
    public int? BaseDummyZoneTier { get; init; }
    public int DungeonTierModifier { get; init; }
    public int? FinalDummyZoneTier { get; init; }
    public bool DungeonTierClampedAtMaximum { get; init; }

    public FormKey? EncounterZoneToWrite =>
        ForwardedEncounterZone ?? AssignedDummyZone?.FormKey;

    public bool IsFwmfForward => ForwardedEncounterZone is not null;
}

public sealed record FwmfForwardCounts(
    int Cells,
    int Worldspaces)
{
    public int Total => Cells + Worldspaces;
}

public sealed record DungeonTierBiasSummary(
    IReadOnlyDictionary<DungeonCategory, int> AssignmentsByCategory,
    IReadOnlyDictionary<int, int> TierShifts,
    int ClampedAtMaximum);

public sealed record UnclassifiedDungeonLocationSample(
    FormKey CellFormKey,
    string? CellEditorId,
    FormKey LocationFormKey,
    string? LocationEditorId,
    IReadOnlyList<LocationKeywordDiagnostic> LocationKeywords,
    FormKey? ParentLocationFormKey,
    string? ParentLocationEditorId,
    IReadOnlyList<LocationKeywordDiagnostic> ParentLocationKeywords);

public sealed record DungeonClassificationAudit(
    int AssignedInteriorCells,
    int AssignedInteriorCellsWithLocation,
    int ClassifiedDungeonCells,
    int UnclassifiedInteriorCellsWithLocation,
    IReadOnlyList<UnclassifiedDungeonLocationSample> UnclassifiedSamples);

public sealed record ApplyContextCatalog(
    IReadOnlyDictionary<FormKey, Func<ISkyrimMod, ICell>> Cells,
    IReadOnlyDictionary<FormKey, Func<ISkyrimMod, IWorldspace>> Worldspaces,
    IReadOnlyDictionary<FormKey, Func<ISkyrimMod, IEncounterZone>> EncounterZones);

public sealed record ApplyResult(
    int CellsApplied,
    int WorldspacesApplied,
    int EncounterZonesApplied)
{
    public int CellsForwardedThroughFwmf { get; init; }
    public int WorldspacesForwardedThroughFwmf { get; init; }
    public int EncounterZoneDifficultyChangesApplied { get; init; }
    public int CombatBoundaryChangesApplied { get; init; }

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
    public FwmfForwardCounts FwmfForwards { get; init; } = new(0, 0);
    public string DifficultyProfileDisplayName { get; init; } = string.Empty;
    public int EncounterZoneDifficultyChanges { get; init; }
    public int CombatBoundaryChanges { get; init; }
    public DungeonTierBiasSummary DungeonTierBias { get; init; } = new(
        new Dictionary<DungeonCategory, int>(),
        new Dictionary<int, int>(),
        0);
    public DungeonClassificationAudit DungeonClassificationAudit { get; init; } =
        new(0, 0, 0, 0, []);

    public int TotalPlannedOverrides =>
        Cells.PlannedOverrides + Worldspaces.PlannedOverrides + EncounterZones.PlannedOverrides;

    public int TotalDummyAssignments =>
        Cells.PlannedOverrides + Worldspaces.PlannedOverrides -
        FwmfForwards.Total;
}

public sealed record PlanningRun(
    ValidatedSourcePlugin SourcePlugin,
    PatchPlan Plan)
{
    public ApplyContextCatalog? ApplyContexts { get; init; }
    public EncounterZoneDifficultyProfile? DifficultyProfile { get; init; }
}
