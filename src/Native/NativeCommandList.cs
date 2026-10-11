using System.Collections;
using Godot;
using LibreKO;

namespace KnightOnlineUiClassic.Native;

/// <summary>
/// The client's command list (<c>World.CommandList</c>): two native <see cref="ItemList"/>s, the groups of the
/// client's <c>CommandCatalog</c> and the commands of the selected group, run by the client's own
/// <c>RunListedCommand</c> when a row is activated. The Classic window regroups the commands for display; it
/// reads every row the client builds (text, hint, availability) and writes the regrouped rows back into the
/// client's own list and its <c>_cmdShown</c> entries, so activating a row still runs the client's command.
/// </summary>
public static class NativeCommandList
{
    public const string Gate = "classic_command_controls";

    /// <summary>One command as the client lists it.</summary>
    public sealed record Row(string Id, string ClientGroup, string Text, string Tooltip, bool Available, object Entry);

    internal static void Register() => NativeWindows.Prepare("command_list", Prepare);

    /// <summary>Names the native lists, reads the client's rows and hands both to the Classic window.</summary>
    public static void Prepare(HudWindow window, World world)
    {
        if (window.HasMeta(Gate)) return;
        var groups = Native.Get<ItemList>(world, "_cmdGroups");
        var entries = Native.Get<ItemList>(world, "_cmdEntries");
        if (groups == null || entries == null || Native.Get<IList>(world, "_cmdShown") == null) return;
        if (!Native.HasMethod(world, "FillCommandEntries") || Native.ClientType("LibreKO.Domain.CommandCatalog") == null) return;
        NativeWindows.Name(groups, "command_client_groups");
        NativeWindows.Name(entries, "command_entries");
        window.SetMeta(WorldMeta, world);
        window.SetMeta(Gate, 1);
    }

    /// <summary>The World whose command list the window shows (the skin binds once the gate is set).</summary>
    public const string WorldMeta = "classic_command_world";

    public static ItemList? Groups(World world) => Native.Get<ItemList>(world, "_cmdGroups");
    public static ItemList? Entries(World world) => Native.Get<ItemList>(world, "_cmdEntries");

    /// <summary>
    /// Every command of the client's catalog in catalog order, read from the rows the client builds for each of
    /// its groups (so names, hints and the "not available yet" state stay the client's). The client's list
    /// is left showing its selected group.
    /// </summary>
    public static List<Row> Read(World world)
    {
        var rows = new List<Row>();
        var catalog = Native.ClientType("LibreKO.Domain.CommandCatalog");
        var entries = Entries(world);
        if (catalog == null || entries == null || Native.Get<Array>(catalog, "Groups") is not { } groups) return rows;
        Color unavailable = Native.Get<Color>(typeof(World), "CommandUnavailableColour");
        foreach (var group in groups)
        {
            Native.Call(world, "FillCommandEntries", group);
            if (Native.Get<IList>(world, "_cmdShown") is not { } shown) break;
            for (int i = 0; i < shown.Count && i < entries.ItemCount; i++)
            {
                object entry = shown[i]!;
                string id = Native.Get<object>(entry, "Id")?.ToString() ?? "";
                rows.Add(new Row(id, group.ToString()!, entries.GetItemText(i), entries.GetItemTooltip(i),
                    entries.GetItemCustomFgColor(i) != unavailable, entry));
            }
        }
        Restore(world);
        return rows;
    }

    /// <summary>The client's title of one of its groups (its catalog text), or null when the client has no such group.</summary>
    public static string? GroupTitle(World world, string clientGroup)
    {
        if (Groups(world) is not { } list || Native.ClientType("LibreKO.Domain.CommandCatalog") is not { } catalog
            || Native.Get<Array>(catalog, "Groups") is not { } groups) return null;
        for (int i = 0; i < groups.Length && i < list.ItemCount; i++)
            if (groups.GetValue(i)?.ToString() == clientGroup) return list.GetItemText(i);
        return null;
    }

    /// <summary>Shows the client's selected group again, as the client itself would.</summary>
    public static void Restore(World world)
    {
        if (Groups(world) is not { } list || list.ItemCount == 0) return;
        int[] picked = list.GetSelectedItems();
        if (Native.ClientType("LibreKO.Domain.CommandCatalog") is { } catalog && Native.Get<Array>(catalog, "Groups") is { } groups)
            Native.Call(world, "FillCommandEntries", groups.GetValue(picked.Length > 0 ? picked[0] : 0));
    }

    /// <summary>
    /// Shows <paramref name="rows"/> in the client's command list. The client's <c>_cmdShown</c> receives the
    /// same entries in the same order, so the client's activation handler runs the row that was clicked.
    /// </summary>
    public static void Show(World world, IReadOnlyList<Row> rows, Action<ItemList, int, Row> style)
    {
        if (Entries(world) is not { } list || Native.Get<IList>(world, "_cmdShown") is not { } shown) return;
        list.Clear();
        shown.Clear();
        foreach (var row in rows)
        {
            int index = list.AddItem(row.Text);
            list.SetItemTooltip(index, row.Tooltip);
            shown.Add(row.Entry);
            style(list, index, row);
        }
    }

    /// <summary>Runs a listed row the way a double click does (the client's <c>ItemActivated</c> handler).</summary>
    public static void Activate(World world, int index)
    {
        if (Entries(world) is { } list) list.EmitSignal(ItemList.SignalName.ItemActivated, (long)index);
    }
}
