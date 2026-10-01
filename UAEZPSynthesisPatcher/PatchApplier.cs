using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace UAEZPSynthesisPatcher;

public static class PatchApplier
{
    public static ApplyResult Apply(ISkyrimMod patchMod, PlanningRun run)
    {
        ArgumentNullException.ThrowIfNull(patchMod);
        ArgumentNullException.ThrowIfNull(run);

        ApplyContextCatalog contexts = run.ApplyContexts ??
            throw new InvalidOperationException(
                "The plan has no captured winning contexts and cannot be applied.");

        Preflight(run.Plan, contexts);

        int cellsApplied = 0;
        int worldspacesApplied = 0;
        int encounterZonesApplied = 0;
        int encounterZoneDifficultyChangesApplied = 0;
        int combatBoundaryChangesApplied = 0;
        int cellsForwardedThroughFwmf = 0;
        int worldspacesForwardedThroughFwmf = 0;

        foreach (PlannedChange change in run.Plan.Changes)
        {
            try
            {
                switch (change.Target.RecordType)
                {
                    case PlannedRecordType.Cell:
                        ApplyCell(patchMod, contexts, change);
                        cellsApplied++;
                        if (change.IsFwmfForward)
                        {
                            cellsForwardedThroughFwmf++;
                        }
                        break;
                    case PlannedRecordType.Worldspace:
                        ApplyWorldspace(patchMod, contexts, change);
                        worldspacesApplied++;
                        if (change.IsFwmfForward)
                        {
                            worldspacesForwardedThroughFwmf++;
                        }
                        break;
                    case PlannedRecordType.EncounterZone:
                        ApplyEncounterZone(patchMod, contexts, change);
                        encounterZonesApplied++;
                        if (change.DesiredEncounterZoneMinimumLevel is not null)
                        {
                            encounterZoneDifficultyChangesApplied++;
                        }
                        if (change.AddDisableCombatBoundary)
                        {
                            combatBoundaryChangesApplied++;
                        }
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unsupported planned record type: " +
                            $"{change.Target.RecordType}.");
                }
            }
            catch (Exception exception) when (
                exception is not InvalidOperationException ||
                !exception.Message.StartsWith(
                    "Failed to apply planned",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Failed to apply planned {change.Target.RecordType} " +
                    $"change for {change.Target.FormKey}: " +
                    exception.Message,
                    exception);
            }
        }

        var result = new ApplyResult(
            cellsApplied,
            worldspacesApplied,
            encounterZonesApplied)
        {
            CellsForwardedThroughFwmf = cellsForwardedThroughFwmf,
            WorldspacesForwardedThroughFwmf =
                worldspacesForwardedThroughFwmf,
            EncounterZoneDifficultyChangesApplied =
                encounterZoneDifficultyChangesApplied,
            CombatBoundaryChangesApplied = combatBoundaryChangesApplied,
        };
        VerifyCounts(run.Plan, result);
        VerifyPlannedTargetsExist(patchMod, run.Plan);
        return result;
    }

    private static void Preflight(
        PatchPlan plan,
        ApplyContextCatalog contexts)
    {
        var seen = new HashSet<FormKey>();

        foreach (PlannedChange change in plan.Changes)
        {
            if (!seen.Add(change.Target.FormKey))
            {
                throw new InvalidOperationException(
                    $"Plan contains duplicate mutation target " +
                    $"{change.Target.FormKey}.");
            }

            if (change.Target.IsDeleted)
            {
                throw new InvalidOperationException(
                    $"Plan contains an ineligible {change.Target.RecordType} " +
                    $"target {change.Target.FormKey}.");
            }

            bool hasContext = change.Target.RecordType switch
            {
                PlannedRecordType.Cell =>
                    contexts.Cells.ContainsKey(change.Target.FormKey),
                PlannedRecordType.Worldspace =>
                    contexts.Worldspaces.ContainsKey(change.Target.FormKey),
                PlannedRecordType.EncounterZone =>
                    contexts.EncounterZones.ContainsKey(change.Target.FormKey),
                _ => false,
            };

            if (!hasContext)
            {
                throw new InvalidOperationException(
                    $"No captured winning context exists for planned " +
                    $"{change.Target.RecordType} {change.Target.FormKey}.");
            }

            bool needsEncounterZone = change.Target.RecordType is
                PlannedRecordType.Cell or PlannedRecordType.Worldspace;
            bool hasDummy = change.AssignedDummyZone is not null;
            bool hasForward = change.ForwardedEncounterZone is not null;
            if (needsEncounterZone != (hasDummy || hasForward) ||
                (hasDummy && hasForward) ||
                (hasForward && change.ForwardedEncounterZone!.Value.IsNull))
            {
                throw new InvalidOperationException(
                    $"Planned {change.Target.RecordType} {change.Target.FormKey} " +
                    "has inconsistent encounter-zone assignment data.");
            }

            if (change.Target.RecordType == PlannedRecordType.EncounterZone)
            {
                bool hasDifficulty =
                    change.DesiredEncounterZoneMinimumLevel is not null;
                if ((!change.AddDisableCombatBoundary && !hasDifficulty) ||
                    change.AssignedDummyZone is not null ||
                    change.ForwardedEncounterZone is not null ||
                    (change.AddDisableCombatBoundary &&
                     change.Target.RequirementSatisfied) ||
                    (hasDifficulty &&
                     (change.Target.EncounterZoneMinimumLevel is null ||
                      change.Target.EncounterZoneMinimumLevel ==
                          change.DesiredEncounterZoneMinimumLevel)))
                {
                    throw new InvalidOperationException(
                        $"Planned EncounterZone {change.Target.FormKey} " +
                        "has inconsistent ECZN mutation data.");
                }
            }
            else if (change.Target.RequirementSatisfied)
            {
                throw new InvalidOperationException(
                    $"Plan contains an ineligible {change.Target.RecordType} " +
                    $"target {change.Target.FormKey}.");
            }
            else if (change.AddDisableCombatBoundary ||
                     change.DesiredEncounterZoneMinimumLevel is not null)
            {
                throw new InvalidOperationException(
                    $"Planned {change.Target.RecordType} " +
                    $"{change.Target.FormKey} contains ECZN mutation data.");
            }
        }
    }

    private static void ApplyCell(
        ISkyrimMod patchMod,
        ApplyContextCatalog contexts,
        PlannedChange change)
    {
        FormKey assigned = change.EncounterZoneToWrite!.Value;
        ICell target = contexts.Cells[change.Target.FormKey](patchMod);
        VerifyTargetIdentity(target.FormKey, change);

        SetEncounterZone(
            target.EncounterZone,
            assigned,
            change.Target.RecordType,
            change.Target.FormKey);
    }

    private static void ApplyWorldspace(
        ISkyrimMod patchMod,
        ApplyContextCatalog contexts,
        PlannedChange change)
    {
        FormKey assigned = change.EncounterZoneToWrite!.Value;
        IWorldspace target =
            contexts.Worldspaces[change.Target.FormKey](patchMod);
        VerifyTargetIdentity(target.FormKey, change);

        SetEncounterZone(
            target.EncounterZone,
            assigned,
            change.Target.RecordType,
            change.Target.FormKey);
    }

    private static void ApplyEncounterZone(
        ISkyrimMod patchMod,
        ApplyContextCatalog contexts,
        PlannedChange change)
    {
        IEncounterZone target =
            contexts.EncounterZones[change.Target.FormKey](patchMod);
        VerifyTargetIdentity(target.FormKey, change);

        if (change.AddDisableCombatBoundary)
        {
            target.Flags |= EncounterZone.Flag.DisableCombatBoundary;
            if (!target.Flags.HasFlag(
                    EncounterZone.Flag.DisableCombatBoundary))
            {
                throw new InvalidOperationException(
                    $"ECZN {change.Target.FormKey} did not retain " +
                    "Disable Combat Boundary after mutation.");
            }
        }

        if (change.DesiredEncounterZoneMinimumLevel is byte desiredMinLevel)
        {
            target.MinLevel = desiredMinLevel;
            if (target.MinLevel != desiredMinLevel)
            {
                throw new InvalidOperationException(
                    $"ECZN {change.Target.FormKey} has MinLevel " +
                    $"{target.MinLevel}, expected {desiredMinLevel}.");
            }
        }
    }

    private static void SetEncounterZone<TGetter>(
        Mutagen.Bethesda.Plugins.IFormLinkNullable<TGetter> link,
        FormKey plannedFormKey,
        PlannedRecordType recordType,
        FormKey targetFormKey)
        where TGetter : class, Mutagen.Bethesda.Plugins.Records.IMajorRecordGetter
    {
        if (!link.IsNull && link.FormKey != plannedFormKey)
        {
            throw new InvalidOperationException(
                $"Output {recordType} {targetFormKey} already has XEZN " +
                $"{link.FormKey}; planned {plannedFormKey} was not written.");
        }

        if (link.IsNull)
        {
            link.SetTo(plannedFormKey);
        }

        if (link.FormKey != plannedFormKey)
        {
            throw new InvalidOperationException(
                $"Output {recordType} {targetFormKey} has XEZN " +
                $"{link.FormKey}, expected {plannedFormKey}.");
        }
    }

    private static void VerifyTargetIdentity(
        FormKey actualFormKey,
        PlannedChange change)
    {
        if (actualFormKey != change.Target.FormKey)
        {
            throw new InvalidOperationException(
                $"Captured context returned {actualFormKey} for planned " +
                $"target {change.Target.FormKey}.");
        }
    }

    private static void VerifyCounts(PatchPlan plan, ApplyResult result)
    {
        if (result.CellsApplied != plan.Cells.PlannedOverrides ||
            result.WorldspacesApplied != plan.Worldspaces.PlannedOverrides ||
            result.EncounterZonesApplied !=
                plan.EncounterZones.PlannedOverrides ||
            result.CellsForwardedThroughFwmf != plan.FwmfForwards.Cells ||
            result.WorldspacesForwardedThroughFwmf !=
                plan.FwmfForwards.Worldspaces ||
            result.EncounterZoneDifficultyChangesApplied !=
                plan.EncounterZoneDifficultyChanges ||
            result.CombatBoundaryChangesApplied !=
                plan.CombatBoundaryChanges ||
            result.TotalApplied != plan.TotalPlannedOverrides)
        {
            throw new InvalidOperationException(
                "Applied override counts do not match the validated plan: " +
                $"CELL {result.CellsApplied}/{plan.Cells.PlannedOverrides}, " +
                $"WRLD {result.WorldspacesApplied}/" +
                $"{plan.Worldspaces.PlannedOverrides}, " +
                $"ECZN {result.EncounterZonesApplied}/" +
                $"{plan.EncounterZones.PlannedOverrides}, " +
                $"FWMF CELL forwards " +
                $"{result.CellsForwardedThroughFwmf}/" +
                $"{plan.FwmfForwards.Cells}, " +
                $"FWMF WRLD forwards " +
                $"{result.WorldspacesForwardedThroughFwmf}/" +
                $"{plan.FwmfForwards.Worldspaces}, " +
                $"ECZN difficulty " +
                $"{result.EncounterZoneDifficultyChangesApplied}/" +
                $"{plan.EncounterZoneDifficultyChanges}, " +
                $"combat boundary {result.CombatBoundaryChangesApplied}/" +
                $"{plan.CombatBoundaryChanges}.");
        }
    }

    private static void VerifyPlannedTargetsExist(
        ISkyrimMod patchMod,
        PatchPlan plan)
    {
        var outputRecords = patchMod.EnumerateMajorRecords().ToList();
        var outputCells = outputRecords.OfType<ICellGetter>()
            .Select(record => record.FormKey)
            .ToHashSet();
        var outputWorldspaces = outputRecords.OfType<IWorldspaceGetter>()
            .Select(record => record.FormKey)
            .ToHashSet();
        var outputEncounterZones = outputRecords.OfType<IEncounterZoneGetter>()
            .Select(record => record.FormKey)
            .ToHashSet();

        foreach (PlannedChange change in plan.Changes)
        {
            bool exists = change.Target.RecordType switch
            {
                PlannedRecordType.Cell =>
                    outputCells.Contains(change.Target.FormKey),
                PlannedRecordType.Worldspace =>
                    outputWorldspaces.Contains(change.Target.FormKey),
                PlannedRecordType.EncounterZone =>
                    outputEncounterZones.Contains(change.Target.FormKey),
                _ => false,
            };

            if (!exists)
            {
                throw new InvalidOperationException(
                    $"Applied {change.Target.RecordType} " +
                    $"{change.Target.FormKey} is missing from the output mod.");
            }
        }
    }
}
