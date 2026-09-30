using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace UAEZPSynthesisPatcher;

public static class FwmfEncounterZoneForwarder
{
    public static bool IsFwmfFamilyPlugin(ModKey modKey)
    {
        string name = Path.GetFileNameWithoutExtension(
            modKey.FileName.String);
        return HasFamilyPrefix(name, "FWMF") ||
            HasFamilyPrefix(name, "Flat World Map Framework");
    }

    public static FormKey? FindNearestEarlierCellEncounterZone(
        ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache,
        FormKey cellFormKey,
        ModKey winningModKey)
    {
        var contexts = linkCache
            .ResolveAllContexts<ICell, ICellGetter>(cellFormKey)
            .ToList();
        int winnerIndex = contexts.FindIndex(
            context => context.ModKey == winningModKey);

        for (int index = winnerIndex + 1;
             winnerIndex >= 0 && index < contexts.Count;
             index++)
        {
            ICellGetter record = contexts[index].Record;
            FormKey target = record.EncounterZone.FormKey;
            if (!record.IsDeleted && IsValidEncounterZone(
                    linkCache,
                    target))
            {
                return target;
            }
        }

        return null;
    }

    public static FormKey? FindNearestEarlierWorldspaceEncounterZone(
        ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache,
        FormKey worldspaceFormKey,
        ModKey winningModKey)
    {
        var contexts = linkCache
            .ResolveAllContexts<IWorldspace, IWorldspaceGetter>(
                worldspaceFormKey)
            .ToList();
        int winnerIndex = contexts.FindIndex(
            context => context.ModKey == winningModKey);

        for (int index = winnerIndex + 1;
             winnerIndex >= 0 && index < contexts.Count;
             index++)
        {
            IWorldspaceGetter record = contexts[index].Record;
            FormKey target = record.EncounterZone.FormKey;
            if (!record.IsDeleted && IsValidEncounterZone(
                    linkCache,
                    target))
            {
                return target;
            }
        }

        return null;
    }

    private static bool HasFamilyPrefix(string name, string prefix)
    {
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return name.Length == prefix.Length ||
            !char.IsLetterOrDigit(name[prefix.Length]);
    }

    private static bool IsValidEncounterZone(
        ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache,
        FormKey target)
    {
        return !target.IsNull &&
            linkCache.TryResolve<IEncounterZoneGetter>(
                target,
                out IEncounterZoneGetter? encounterZone) &&
            !encounterZone.IsDeleted;
    }
}
