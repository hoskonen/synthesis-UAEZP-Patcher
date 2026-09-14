using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;

namespace UAEZPSynthesisPatcher;

public static class StatePlanner
{
    public static PlanningRun Build(
        IPatcherState<ISkyrimMod, ISkyrimModGetter> state,
        Settings settings)
    {
        ArgumentNullException.ThrowIfNull(state);
        SettingsValidator.Validate(settings);

        var sourceCandidates = new List<SourcePluginCandidate>();
        foreach (var listing in state.LoadOrder.ListedOrder.Where(listing =>
                     listing.Enabled && string.Equals(
                         listing.ModKey.FileName.String,
                         SourcePluginValidator.SupportedPluginFileName,
                         StringComparison.OrdinalIgnoreCase)))
        {
            ISkyrimModGetter sourceMod = listing.Mod ??
                throw new InvalidOperationException(
                    $"Active source listing '{listing.ModKey}' has no loaded mod data.");

            sourceCandidates.Add(new SourcePluginCandidate(
                listing.ModKey,
                sourceMod.EncounterZones
                    .Where(zone => zone.EditorID?.StartsWith(
                        SourcePluginValidator.DummyEditorIdPrefix,
                        StringComparison.Ordinal) == true)
                    .Select(ToDummyZoneDefinition)
                    .ToList()));
        }

        ValidatedSourcePlugin source =
            SourcePluginValidator.Validate(sourceCandidates);
        source = ResolveWinningDummyZones(state, source);

        List<RecordSnapshot> cells = state.LoadOrder.PriorityOrder
            .Cell()
            .WinningContextOverrides(
                state.LinkCache,
                includeDeletedRecords: true)
            .Select(context => ToEncounterZoneLinkSnapshot(
                PlannedRecordType.Cell,
                context.Record,
                context.Record.EncounterZone.FormKey,
                context.ModKey,
                state.LinkCache))
            .ToList();

        List<RecordSnapshot> worldspaces = state.LoadOrder.PriorityOrder
            .Worldspace()
            .WinningContextOverrides(includeDeletedRecords: true)
            .Select(context => ToEncounterZoneLinkSnapshot(
                PlannedRecordType.Worldspace,
                context.Record,
                context.Record.EncounterZone.FormKey,
                context.ModKey,
                state.LinkCache))
            .ToList();

        List<RecordSnapshot> encounterZones = state.LoadOrder.PriorityOrder
            .EncounterZone()
            .WinningContextOverrides(includeDeletedRecords: true)
            .Select(context => new RecordSnapshot(
                PlannedRecordType.EncounterZone,
                context.Record.FormKey,
                context.Record.EditorID,
                context.ModKey,
                context.Record.IsDeleted,
                context.Record.Flags.HasFlag(
                    EncounterZone.Flag.DisableCombatBoundary)))
            .ToList();

        PatchPlan plan = ReadOnlyPlanner.Build(
            settings,
            source.DummyZones,
            cells,
            worldspaces,
            encounterZones);

        return new PlanningRun(source, plan);
    }

    private static DummyZoneDefinition ToDummyZoneDefinition(
        IEncounterZoneGetter zone)
    {
        return new DummyZoneDefinition(
            zone.FormKey,
            zone.EditorID!,
            zone.MinLevel,
            zone.MaxLevel,
            zone.Rank,
            zone.Owner.FormKey,
            zone.Location.FormKey,
            zone.Flags,
            zone.IsDeleted);
    }

    private static RecordSnapshot ToEncounterZoneLinkSnapshot(
        PlannedRecordType recordType,
        IMajorRecordGetter record,
        FormKey encounterZoneFormKey,
        ModKey winningModKey,
        ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache)
    {
        bool hasEncounterZone = !encounterZoneFormKey.IsNull;
        IEncounterZoneGetter? resolvedEncounterZone = null;
        bool unresolved = false;
        if (hasEncounterZone)
        {
            if (linkCache.TryResolve<IEncounterZoneGetter>(
                    encounterZoneFormKey,
                    out IEncounterZoneGetter? resolved))
            {
                resolvedEncounterZone = resolved;
            }
            else
            {
                unresolved = true;
            }
        }

        return new RecordSnapshot(
            recordType,
            record.FormKey,
            record.EditorID,
            winningModKey,
            record.IsDeleted,
            hasEncounterZone,
            unresolved)
        {
            EncounterZoneTarget = hasEncounterZone
                ? encounterZoneFormKey
                : null,
            ResolvedEncounterZoneEditorId = resolvedEncounterZone?.EditorID,
        };
    }

    private static ValidatedSourcePlugin ResolveWinningDummyZones(
        IPatcherState<ISkyrimMod, ISkyrimModGetter> state,
        ValidatedSourcePlugin source)
    {
        var winners = new List<ValidatedDummyZone>(source.DummyZones.Count);

        foreach (ValidatedDummyZone sourceZone in source.DummyZones)
        {
            if (!state.LinkCache.TryResolve<IEncounterZoneGetter>(
                    sourceZone.FormKey,
                    out IEncounterZoneGetter? winner))
            {
                throw new InvalidOperationException(
                    $"Dummy encounter zone {sourceZone.EditorId} " +
                    $"({sourceZone.FormKey}) does not resolve in the winning load order.");
            }

            if (winner.IsDeleted)
            {
                throw new InvalidOperationException(
                    $"Dummy encounter zone {sourceZone.EditorId} " +
                    $"({sourceZone.FormKey}) is deleted by its winning override.");
            }

            winners.Add(new ValidatedDummyZone(
                winner.FormKey,
                winner.EditorID ?? sourceZone.EditorId,
                winner.MinLevel,
                winner.MaxLevel,
                winner.Flags));
        }

        return source with { DummyZones = winners };
    }
}
