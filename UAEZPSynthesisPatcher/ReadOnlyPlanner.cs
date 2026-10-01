using Mutagen.Bethesda.Plugins;

namespace UAEZPSynthesisPatcher;

public static class ReadOnlyPlanner
{
    public const int UnclassifiedDungeonSampleLimit = 25;

    public static PatchPlan Build(
        Settings settings,
        IReadOnlyList<ValidatedDummyZone> orderedZones,
        IEnumerable<RecordSnapshot> cells,
        IEnumerable<RecordSnapshot> worldspaces,
        IEnumerable<RecordSnapshot> encounterZones,
        EncounterZoneDifficultyProfile? difficultyProfile = null)
    {
        SettingsValidator.Validate(settings);
        ArgumentNullException.ThrowIfNull(orderedZones);

        List<RecordSnapshot> cellRecords = cells.ToList();
        List<RecordSnapshot> worldspaceRecords = worldspaces.ToList();
        List<RecordSnapshot> encounterZoneRecords = encounterZones.ToList();
        difficultyProfile ??= EncounterZoneDifficultyResolver.Resolve(
            settings.DifficultyProfile,
            orderedZones.Select(zone => zone.MinLevel));

        ExistingStateProvenance provenance = ProvenanceAnalyzer.Analyze(
            orderedZones,
            cellRecords,
            worldspaceRecords,
            encounterZoneRecords);

        var changes = new List<PlannedChange>();
        var distribution = orderedZones.ToDictionary(zone => zone.FormKey, _ => 0);
        var perOriginPlugin = new Dictionary<ModKey, int>();
        int forwardedCells = 0;
        int forwardedWorldspaces = 0;
        var dungeonAssignmentsByCategory = Enum
            .GetValues<DungeonCategory>()
            .Where(category => category != DungeonCategory.None)
            .ToDictionary(category => category, _ => 0);
        var dungeonTierShifts = new Dictionary<int, int>();
        int dungeonTierClampedAtMaximum = 0;
        var dungeonClassificationAudit =
            new DungeonClassificationAuditBuilder();

        RecordPlanSummary cellSummary = PlanAssignments(
            settings.AssignMissingCellEncounterZones,
            settings,
            orderedZones,
            cellRecords,
            PlannedRecordType.Cell,
            changes,
            distribution,
            perOriginPlugin,
            ref forwardedCells,
            dungeonAssignmentsByCategory,
            dungeonTierShifts,
            ref dungeonTierClampedAtMaximum,
            dungeonClassificationAudit);

        RecordPlanSummary worldspaceSummary = PlanAssignments(
            settings.AssignMissingWorldspaceEncounterZones,
            settings,
            orderedZones,
            worldspaceRecords,
            PlannedRecordType.Worldspace,
            changes,
            distribution,
            perOriginPlugin,
            ref forwardedWorldspaces,
            dungeonAssignmentsByCategory,
            dungeonTierShifts,
            ref dungeonTierClampedAtMaximum,
            dungeonClassificationAudit);

        IReadOnlyDictionary<FormKey, byte> desiredMinimumLevels =
            BuildDesiredMinimumLevels(orderedZones, difficultyProfile);
        RecordPlanSummary encounterZoneSummary = PlanEncounterZones(
            settings,
            encounterZoneRecords,
            desiredMinimumLevels,
            changes,
            out int difficultyChanges,
            out int combatBoundaryChanges);

        var plan = new PatchPlan(
            cellSummary,
            worldspaceSummary,
            encounterZoneSummary,
            changes,
            distribution,
            perOriginPlugin)
        {
            ExistingStateProvenance = provenance,
            FwmfForwards = new FwmfForwardCounts(
                forwardedCells,
                forwardedWorldspaces),
            DifficultyProfileDisplayName = difficultyProfile.DisplayName,
            EncounterZoneDifficultyChanges = difficultyChanges,
            CombatBoundaryChanges = combatBoundaryChanges,
            DungeonTierBias = new DungeonTierBiasSummary(
                dungeonAssignmentsByCategory,
                dungeonTierShifts,
                dungeonTierClampedAtMaximum),
            DungeonClassificationAudit = dungeonClassificationAudit.Build(),
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
        IDictionary<ModKey, int> perOriginPlugin,
        ref int forwardedThroughFwmf,
        IDictionary<DungeonCategory, int> dungeonAssignmentsByCategory,
        IDictionary<int, int> dungeonTierShifts,
        ref int dungeonTierClampedAtMaximum,
        DungeonClassificationAuditBuilder dungeonClassificationAudit)
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

            if (settings.ForwardEncounterZonesThroughFwmf &&
                FwmfEncounterZoneForwarder.IsFwmfFamilyPlugin(
                    record.WinningModKey))
            {
                if (record.EarlierResolvableEncounterZoneTarget is
                    FormKey forwardedTarget)
                {
                    changes.Add(new PlannedChange(record, null)
                    {
                        ForwardedEncounterZone = forwardedTarget,
                    });
                    IncrementOriginPluginCount(record, perOriginPlugin);
                    forwardedThroughFwmf++;
                    planned++;
                    continue;
                }
            }

            if (!enabled)
            {
                continue;
            }

            int baseTier = DeterministicDummyZoneSelector.SelectIndex(
                settings.DummyZoneMode,
                settings.Seed,
                record.FormKey,
                orderedZones.Count);
            DungeonTierSelection tierSelection = DungeonTierBias.Apply(
                settings.DungeonDifficulty,
                expectedType == PlannedRecordType.Cell
                    ? record.DungeonCategory
                    : DungeonCategory.None,
                baseTier,
                orderedZones.Count);
            ValidatedDummyZone assigned = orderedZones[tierSelection.FinalTier];

            changes.Add(new PlannedChange(record, assigned)
            {
                BaseDummyZoneTier = tierSelection.BaseTier,
                DungeonTierModifier = tierSelection.Modifier,
                FinalDummyZoneTier = tierSelection.FinalTier,
                DungeonTierClampedAtMaximum =
                    tierSelection.ClampedAtMaximum,
            });
            if (expectedType == PlannedRecordType.Cell)
            {
                dungeonClassificationAudit.Record(record);
            }
            if (settings.EnableDungeonTierBias &&
                expectedType == PlannedRecordType.Cell &&
                record.DungeonCategory != DungeonCategory.None)
            {
                dungeonAssignmentsByCategory[record.DungeonCategory]++;
                if (tierSelection.Modifier != 0)
                {
                    dungeonTierShifts.TryGetValue(
                        tierSelection.Modifier,
                        out int shiftCount);
                    dungeonTierShifts[tierSelection.Modifier] = shiftCount + 1;
                }
                if (tierSelection.ClampedAtMaximum)
                {
                    dungeonTierClampedAtMaximum++;
                }
            }
            distribution[assigned.FormKey]++;
            IncrementOriginPluginCount(record, perOriginPlugin);
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

    private static void IncrementOriginPluginCount(
        RecordSnapshot record,
        IDictionary<ModKey, int> perOriginPlugin)
    {
        perOriginPlugin.TryGetValue(
            record.FormKey.ModKey,
            out int currentCount);
        perOriginPlugin[record.FormKey.ModKey] = currentCount + 1;
    }

    private static RecordPlanSummary PlanEncounterZones(
        Settings settings,
        IEnumerable<RecordSnapshot> records,
        IReadOnlyDictionary<FormKey, byte> desiredMinimumLevels,
        ICollection<PlannedChange> changes,
        out int difficultyChanges,
        out int combatBoundaryChanges)
    {
        int scanned = 0;
        int missing = 0;
        int existing = 0;
        int deleted = 0;
        int planned = 0;
        difficultyChanges = 0;
        combatBoundaryChanges = 0;
        bool applyDifficulty = settings.DifficultyProfile !=
            EncounterZoneDifficultyProfileSelection.MatchValidatedSource;

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
            }
            else
            {
                missing++;
            }

            bool addCombatBoundary =
                settings.DisableCombatBoundaries &&
                !record.RequirementSatisfied;
            byte? desiredMinimumLevel = null;
            if (applyDifficulty &&
                desiredMinimumLevels.TryGetValue(
                    record.FormKey,
                    out byte desired))
            {
                byte existingMinimumLevel =
                    record.EncounterZoneMinimumLevel ??
                    throw new InvalidOperationException(
                        $"Dummy ECZN {record.FormKey} has no captured MinLevel.");
                if (existingMinimumLevel != desired)
                {
                    desiredMinimumLevel = desired;
                }
            }

            if (!addCombatBoundary && desiredMinimumLevel is null)
            {
                continue;
            }

            changes.Add(new PlannedChange(record, null)
            {
                AddDisableCombatBoundary = addCombatBoundary,
                DesiredEncounterZoneMinimumLevel = desiredMinimumLevel,
            });
            if (addCombatBoundary)
            {
                combatBoundaryChanges++;
            }
            if (desiredMinimumLevel is not null)
            {
                difficultyChanges++;
            }
            planned++;
        }

        return new RecordPlanSummary(scanned, missing, existing, deleted, 0, planned);
    }

    private static IReadOnlyDictionary<FormKey, byte> BuildDesiredMinimumLevels(
        IReadOnlyList<ValidatedDummyZone> orderedZones,
        EncounterZoneDifficultyProfile difficultyProfile)
    {
        if (orderedZones.Count != EncounterZoneDifficultyResolver.TierCount ||
            difficultyProfile.Tiers.Count !=
                EncounterZoneDifficultyResolver.TierCount)
        {
            throw new InvalidOperationException(
                "Difficulty planning requires exactly nine ordered dummy zones " +
                "and nine difficulty tiers.");
        }

        return orderedZones
            .Select((zone, index) => new
            {
                zone.FormKey,
                MinimumLevel = difficultyProfile.Tiers[index].MinimumLevel,
            })
            .ToDictionary(item => item.FormKey, item => item.MinimumLevel);
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

        int difficultyChanges = plan.Changes.Count(change =>
            change.DesiredEncounterZoneMinimumLevel is not null);
        int combatBoundaryChanges = plan.Changes.Count(change =>
            change.AddDisableCombatBoundary);
        int encounterZoneChanges = plan.Changes.Count(change =>
            change.Target.RecordType == PlannedRecordType.EncounterZone);
        if (difficultyChanges != plan.EncounterZoneDifficultyChanges ||
            combatBoundaryChanges != plan.CombatBoundaryChanges ||
            encounterZoneChanges != plan.EncounterZones.PlannedOverrides)
        {
            throw new InvalidOperationException(
                "Internal planning error: ECZN intent counts do not match " +
                "the captured changes.");
        }
    }

    private sealed class DungeonClassificationAuditBuilder
    {
        private int _assignedInteriorCells;
        private int _assignedInteriorCellsWithLocation;
        private int _classifiedDungeonCells;
        private int _unclassifiedInteriorCellsWithLocation;
        private readonly List<UnclassifiedDungeonLocationSample> _samples = [];

        public void Record(RecordSnapshot record)
        {
            if (!record.IsInteriorCell)
            {
                return;
            }

            _assignedInteriorCells++;
            if (record.LocationFormKey is not FormKey locationFormKey)
            {
                return;
            }

            _assignedInteriorCellsWithLocation++;
            if (record.DungeonCategory != DungeonCategory.None)
            {
                _classifiedDungeonCells++;
                return;
            }

            _unclassifiedInteriorCellsWithLocation++;
            if (_samples.Count >= UnclassifiedDungeonSampleLimit)
            {
                return;
            }

            _samples.Add(new UnclassifiedDungeonLocationSample(
                record.FormKey,
                record.EditorId,
                locationFormKey,
                record.LocationEditorId,
                record.LocationKeywords,
                record.ParentLocationFormKey,
                record.ParentLocationEditorId,
                record.ParentLocationKeywords));
        }

        public DungeonClassificationAudit Build()
        {
            return new DungeonClassificationAudit(
                _assignedInteriorCells,
                _assignedInteriorCellsWithLocation,
                _classifiedDungeonCells,
                _unclassifiedInteriorCellsWithLocation,
                _samples);
        }
    }
}
