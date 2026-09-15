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

        foreach (PlannedChange change in run.Plan.Changes)
        {
            try
            {
                switch (change.Target.RecordType)
                {
                    case PlannedRecordType.Cell:
                        ApplyCell(patchMod, contexts, change);
                        cellsApplied++;
                        break;
                    case PlannedRecordType.Worldspace:
                        ApplyWorldspace(patchMod, contexts, change);
                        worldspacesApplied++;
                        break;
                    case PlannedRecordType.EncounterZone:
                        ApplyEncounterZone(patchMod, contexts, change);
                        encounterZonesApplied++;
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
            encounterZonesApplied);
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

            if (change.Target.IsDeleted || change.Target.RequirementSatisfied)
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

            bool needsDummy = change.Target.RecordType is
                PlannedRecordType.Cell or PlannedRecordType.Worldspace;
            if (needsDummy != (change.AssignedDummyZone is not null))
            {
                throw new InvalidOperationException(
                    $"Planned {change.Target.RecordType} {change.Target.FormKey} " +
                    "has inconsistent dummy-zone assignment data.");
            }
        }
    }

    private static void ApplyCell(
        ISkyrimMod patchMod,
        ApplyContextCatalog contexts,
        PlannedChange change)
    {
        ValidatedDummyZone assigned = change.AssignedDummyZone!;
        ICell target = contexts.Cells[change.Target.FormKey](patchMod);
        VerifyTargetIdentity(target.FormKey, change);

        SetEncounterZone(
            target.EncounterZone,
            assigned.FormKey,
            change.Target.RecordType,
            change.Target.FormKey);
    }

    private static void ApplyWorldspace(
        ISkyrimMod patchMod,
        ApplyContextCatalog contexts,
        PlannedChange change)
    {
        ValidatedDummyZone assigned = change.AssignedDummyZone!;
        IWorldspace target =
            contexts.Worldspaces[change.Target.FormKey](patchMod);
        VerifyTargetIdentity(target.FormKey, change);

        SetEncounterZone(
            target.EncounterZone,
            assigned.FormKey,
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

        target.Flags |= EncounterZone.Flag.DisableCombatBoundary;
        if (!target.Flags.HasFlag(
                EncounterZone.Flag.DisableCombatBoundary))
        {
            throw new InvalidOperationException(
                $"ECZN {change.Target.FormKey} did not retain " +
                "Disable Combat Boundary after mutation.");
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
            result.TotalApplied != plan.TotalPlannedOverrides)
        {
            throw new InvalidOperationException(
                "Applied override counts do not match the validated plan: " +
                $"CELL {result.CellsApplied}/{plan.Cells.PlannedOverrides}, " +
                $"WRLD {result.WorldspacesApplied}/" +
                $"{plan.Worldspaces.PlannedOverrides}, " +
                $"ECZN {result.EncounterZonesApplied}/" +
                $"{plan.EncounterZones.PlannedOverrides}.");
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
