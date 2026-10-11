using Godot;
using LibreKO;
using LibreKO.Domain;
using KnightOnlineUiClassic.Windows;

namespace KnightOnlineUiClassic.Native;

/// <summary>
/// The client's right-click popup menus shown as the Classic context menu (<see cref="ClassicContextMenu.Mirror"/>):
/// the loot item rule menu (titled with the item's name in its grade colour), the party member menu and the clan
/// member menu (titled with the member's name). The client's rows, ids and handlers stay its own.
/// </summary>
public static class NativeContextMenus
{
    private const string Watched = "classic_context_menus";

    /// <summary>Mirrors the menus the world has and watches for the loot rule menu, which the client builds on the first right click.</summary>
    public static void Prepare(World world)
    {
        if (!GodotObject.IsInstanceValid(world)) return;
        if (!world.HasMeta(Watched))
        {
            world.SetMeta(Watched, true);
            ClassicPointer.Track(world);
            // The client adds the loot rule menu to the world and opens it right away, so mirror it as it enters.
            world.ChildEnteredTree += node =>
            {
                if (node is PopupMenu menu && Plugin.Active && Native.Get<PopupMenu>(world, "_lootRuleMenu") == menu) MirrorLoot(world, menu);
            };
        }
        PrepareLater(world);
    }

    /// <summary>Mirrors the menus that exist now; safe to call repeatedly.</summary>
    public static void PrepareLater(World world)
    {
        if (!GodotObject.IsInstanceValid(world)) return;
        if (Native.Has(world, "_lootRuleMenu")) MirrorLoot(world, Native.Get<PopupMenu>(world, "_lootRuleMenu"));
        if (Native.Has(world, "_partyHudMenu")) MirrorPartyMenu(world);
        if (Native.Has(world, "_clanMemberMenu"))
            ClassicContextMenu.Mirror(Native.Get<PopupMenu>(world, "_clanMemberMenu"), "clan_menu",
                () => Native.Get<string>(world, "_ctxMember") is { Length: > 0 } name ? (name, null) : null,
                // The client heads its rows with a disabled "— name —" row; the Classic title row replaces it.
                (menu, index) => index == 0 && menu.IsItemDisabled(0) && menu.GetItemId(0) == 0);
    }

    /// <summary>The party member menu; the Classic party list sets the member's name as its title before it opens.</summary>
    public static PopupMenu? MirrorPartyMenu(World? world)
    {
        if (world == null || Native.Get<PopupMenu>(world, "_partyHudMenu") is not { } menu) return null;
        ClassicContextMenu.Mirror(menu, "party_menu");
        return menu;
    }

    private static void MirrorLoot(World world, PopupMenu? menu) =>
        ClassicContextMenu.Mirror(menu, "loot_menu", () =>
        {
            int item = Native.Get<int>(world, "_lootRuleItem");
            return item > 0 ? (ItemData.DisplayName(item).Trim(), ItemGrade.Tint(item)) : null;
        });
}
