using Mutagen.Bethesda.Plugins;

namespace UAEZPSynthesisPatcher;

public static class ReadOnlyPlanner
{
    public static PatchPlan Build(
        Settings settings,
        IReadOnlyList<ValidatedDummyZone> orderedZones,
        IEnumerable<RecordSnapshot> cells,
        IEnumerable<RecordSnapshot> worldspaces,
        IEnumerable<RecordSnapshot> encounterZones)
    {
        SettingsValidator.Validate(settings);
        ArgumentNullException.ThrowIfNull(orderedZones);

        List<RecordSnapshot> cellRecords = cells.ToList();
        List<RecordSnapshot> worldspaceRecords = worldspaces.ToList();
        List<RecordSnapshot> encounterZoneRecords = encounterZones.ToList();

        ExistingStateProvenance provenance = ProvenanceAnalyzer.Analyze(
            orderedZones,
            cellRecords,
            worldspaceRecords,
            encounterZoneRecords);

        var changes = new List<PlannedChange>();
        var distribution = orderedZones.ToDictionary(zone => zone.FormKey, _ => 0);
        var perOriginPlugin = new Dictionary<ModKey, int>();

        RecordPlanSummary cellSummary = PlanAssignments(
            settings.AssignMissingCellEncounterZones,
            settings,
            orderedZones,
            cellRecords,
            PlannedRecordType.Cell,
            changes,
            distribution,
            perOriginPlugin);

        RecordPlanSummary worldspaceSummary = PlanAssignments(
            settings.AssignMissingWorldspaceEncounterZones,
            settings,
            orderedZones,
            worldspaceRecords,
            PlannedRecordType.Worldspace,
            changes,
            distribution,
            perOriginPlugin);

        RecordPlanSummary encounterZoneSummary = PlanFlags(
            settings.DisableCombatBoundaries,
            encounterZoneRecords,
            changes);

        var plan = new PatchPlan(
            cellSummary,
            worldspaceSummary,
            encounterZoneSummary,
            changes,
            distribution,
            perOriginPlugin)
        {
            ExistingStateProvenance = provenance,
        };

        ValidateInternalConsistency(plan);
        return plan;
    }

    private static RecordPlanSummary PlanAssignments(
        bool enabled,
        Settings settings,
        IReadOnlyList<ValidatedDummyZone> orderedZones,
        IEnumerable<RecordSnapshot> records,
        PlannedRecordType expectedType,
        ICollection<PlannedChange> changes,
        IDictionary<FormKey, int> distribution,
        IDictionary<ModKey, int> perOriginPlugin)
    {
        int scanned = 0;
        int missing = 0;
        int existing = 0;
        int deleted = 0;
        int unresolved = 0;
        int planned = 0;

        foreach (RecordSnapshot record in records)
        {
            EnsureRecordType(record, expectedType);
            scanned++;

            if (record.IsDeleted)
            {
                deleted++;
                continue;
            }

            if (record.RequirementSatisfied)
            {
                existing++;
                if (record.HasUnresolvedRequirementReference)
                {
                    unresolved++;
                }
                continue;
            }

            missing++;
            if (!enabled)
            {
                continue;
            }

            ValidatedDummyZone assigned = DeterministicDummyZoneSelector.Select(
                settings.DummyZoneMode,
                settings.Seed,
                record.FormKey,
                orderedZones);

            changes.Add(new PlannedChange(record, assigned));
            distribution[assigned.FormKey]++;
            perOriginPlugin.TryGetValue(record.FormKey.ModKey, out int currentCount);
            perOriginPlugin[record.FormKey.ModKey] = currentCount + 1;
            planned++;
        }

        return new RecordPlanSummary(
            scanned,
            missing,
            existing,
            deleted,
            unresolved,
            planned);
    }

    private static RecordPlanSummary PlanFlags(
        bool enabled,
        IEnumerable<RecordSnapshot> records,
        ICollection<PlannedChange> changes)
    {
        int scanned = 0;
        int missing = 0;
        int existing = 0;
        int deleted = 0;
        int planned = 0;

        foreach (RecordSnapshot record in records)
        {
            EnsureRecordType(record, PlannedRecordType.EncounterZone);
            scanned++;

            if (record.IsDeleted)
            {
                deleted++;
                continue;
            }

            if (record.RequirementSatisfied)
            {
                existing++;
                continue;
            }

            missing++;
            if (!enabled)
            {
                continue;
            }

            changes.Add(new PlannedChange(record, null));
            planned++;
        }

        return new RecordPlanSummary(scanned, missing, existing, deleted, 0, planned);
    }

    private static void EnsureRecordType(
        RecordSnapshot record,
        PlannedRecordType expectedType)
    {
        if (record.RecordType != expectedType)
        {
            throw new ArgumentException(
                $"Expected {expectedType} input but received " +
                $"{record.RecordType} ({record.FormKey}).");
        }
    }

    private static void ValidateInternalConsistency(PatchPlan plan)
    {
        int assignmentTotal = plan.AssignmentDistribution.Values.Sum();
        if (assignmentTotal != plan.TotalDummyAssignments)
        {
            throw new InvalidOperationException(
                $"Internal planning error: assignment distribution totals " +
                $"{assignmentTotal}, expected {plan.TotalDummyAssignments}.");
        }

        if (plan.Changes.Count != plan.TotalPlannedOverrides)
        {
            throw new InvalidOperationException(
                $"Internal planning error: {plan.Changes.Count} changes were " +
                $"captured, expected {plan.TotalPlannedOverrides}.");
        }
    }
}
