using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace UAEZPSynthesisPatcher;

public enum DungeonCategory
{
    None,
    Cave,
    NordicRuin,
    DwemerRuin,
    Mine,
    Fort,
    OtherDungeon,
}

public sealed record DungeonCategoryResolution(
    DungeonCategory Category,
    FormKey? LocationFormKey,
    string? LocationEditorId);

public static class DungeonCategoryResolver
{
    private static readonly ModKey SkyrimModKey =
        ModKey.FromFileName("Skyrim.esm");

    public static readonly FormKey LocTypeDungeon =
        new(SkyrimModKey, 0x0130DB);
    public static readonly FormKey LocSetCave =
        new(SkyrimModKey, 0x0130EF);
    public static readonly FormKey LocSetCaveIce =
        new(SkyrimModKey, 0x100819);
    public static readonly FormKey LocSetNordicRuin =
        new(SkyrimModKey, 0x0130F2);
    public static readonly FormKey LocSetDwarvenRuin =
        new(SkyrimModKey, 0x0130F0);
    public static readonly FormKey LocTypeMine =
        new(SkyrimModKey, 0x018EF1);
    public static readonly FormKey LocSetMilitaryFort =
        new(SkyrimModKey, 0x0130F1);
    public static readonly FormKey LocTypeMilitaryFort =
        new(SkyrimModKey, 0x0130E7);
    public static readonly FormKey LocTypeInn =
        new(SkyrimModKey, 0x01CB87);
    public static readonly FormKey LocTypeHouse =
        new(SkyrimModKey, 0x01CB85);
    public static readonly FormKey LocTypeStore =
        new(SkyrimModKey, 0x01CB86);

    public static DungeonCategoryResolution Resolve(
        ICellGetter cell,
        ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(linkCache);

        if (!cell.Flags.HasFlag(Cell.Flag.IsInteriorCell) ||
            cell.Location.IsNull ||
            !linkCache.TryResolve<ILocationGetter>(
                cell.Location.FormKey,
                out ILocationGetter? location))
        {
            return new DungeonCategoryResolution(
                DungeonCategory.None,
                cell.Location.IsNull ? null : cell.Location.FormKey,
                null);
        }

        DungeonCategory category = ClassifyKeywords(
            true,
            location.Keywords?.Select(keyword => keyword.FormKey) ?? []);
        return new DungeonCategoryResolution(
            category,
            location.FormKey,
            location.EditorID);
    }

    public static DungeonCategory ClassifyKeywords(
        bool isInterior,
        IEnumerable<FormKey> locationKeywords)
    {
        ArgumentNullException.ThrowIfNull(locationKeywords);
        if (!isInterior)
        {
            return DungeonCategory.None;
        }

        HashSet<FormKey> keywords = locationKeywords.ToHashSet();
        if (keywords.Contains(LocTypeMine))
        {
            return DungeonCategory.Mine;
        }
        if (keywords.Contains(LocSetNordicRuin))
        {
            return DungeonCategory.NordicRuin;
        }
        if (keywords.Contains(LocSetDwarvenRuin))
        {
            return DungeonCategory.DwemerRuin;
        }
        if (keywords.Contains(LocSetMilitaryFort) ||
            keywords.Contains(LocTypeMilitaryFort))
        {
            return DungeonCategory.Fort;
        }
        if (keywords.Contains(LocSetCave) ||
            keywords.Contains(LocSetCaveIce))
        {
            return DungeonCategory.Cave;
        }
        if (keywords.Contains(LocTypeDungeon))
        {
            return DungeonCategory.OtherDungeon;
        }

        return DungeonCategory.None;
    }
}
