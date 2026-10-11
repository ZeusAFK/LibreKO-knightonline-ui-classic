using System.Collections;
using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;

namespace KnightOnlineUiClassic.Native;

/// <summary>
/// Party HUD and seek-party board support: the native party actions the Classic party HUD offers (invite by
/// name, row meter, war board, member menu), and on the board the listing data on the native rows plus a
/// private-message action per listing.
/// </summary>
public static class NativeParty
{
    private const int MembersPerRecruitment = 8, ListingsPerPage = 10;

    /// <summary>The client's invite-by-name prompt (the Classic text prompt when the theme replaces it).</summary>
    public static void InviteByName(World? world) { if (world != null) Native.TryCall(world, "OpenInvitePrompt", out _); }

    /// <summary>Cycles the party rows' meter (off, damage, healing) through the client's own cooldown.</summary>
    public static void ToggleMeter(World? world) { if (world != null) Native.TryCall(world, "ToggleRowMeter", out _); }

    public static void ToggleWarBoard(World? world) { if (world != null) Native.TryCall(world, "ToggleWarBoard", out _); }

    public static void ToggleBoard(World? world) { if (world != null) Native.TryCall(world, "ToggleSeekParty", out _); }

    /// <summary>The row meter mode: 0 off, 1 damage dealt, 2 healing.</summary>
    public static byte MeterMode => Net.I?.PartyRowMeterMode ?? 0;

    /// <summary>A member's share of the party meter, 0..1.</summary>
    public static float MeterShare(World? world, int memberId)
    {
        if (world == null || Native.Get<IDictionary<int, int>>(world, "_warBoardRowShares") is not { } shares) return 0;
        return PartyWarBoard.Fill(shares.TryGetValue(memberId, out int share) ? share : 0);
    }

    public static bool IsCommander(int memberId) => Net.I?.PartyCommanderId == memberId;

    /// <summary>Gives the native member menu the Classic menu look.</summary>
    public static void StyleMemberMenu(World? world, Action<PopupMenu> style)
    {
        if (world != null && Native.Get<PopupMenu>(world, "_partyHudMenu") is { } menu && !menu.HasMeta("classic_menu"))
        {
            menu.SetMeta("classic_menu", true);
            style(menu);
        }
    }

    public static void PrepareBoard(HudWindow window, World world)
    {
        if (Native.Get<VBoxContainer>(world, "_seekListBox") is not { } list) return;
        if (list.Name != "seek_members") list.Name = "seek_members";
        NativeWindows.Name(Native.Get<OptionButton>(world, "_wantedClassOpt"), "seek_wanted_class");
        NativeWindows.Name(Native.Get<LineEdit>(world, "_wantedMsgInput"), "seek_wanted_message");
        NativeWindows.Name(Native.Get<HBoxContainer>(world, "_seekFilters"), "seek_filters");
        NativeWindows.Name(Native.Get<HBoxContainer>(world, "_fullMoonRow"), "seek_full_moon");
        NativeWindows.Name(Native.Get<Label>(world, "_seekHeading"), "seek_heading");
        NativeWindows.Name(Native.Get<Label>(world, "_seekStatusLbl"), "seek_status");
        if (Native.Descendants(window.Body).OfType<Button>().FirstOrDefault(b => b.Text == "Invite by name") is { } byName) byName.Name = "seek_invite_name";
        // Rows from the previous page are queued for deletion; drop them before the new page is shown. Filter
        // changes rebuild the rows without a server reply, so rows are described as they arrive.
        bool queued = false;
        list.ChildEnteredTree += _ =>
        {
            foreach (var old in list.GetChildren().Where(c => c.IsQueuedForDeletion()).ToArray()) list.RemoveChild(old);
            if (queued) return;
            queued = true;
            Callable.From(() => { queued = false; if (GodotObject.IsInstanceValid(world)) Listed(world); }).CallDeferred();
        };
        NativeWindows.Sync(window, () => Mirror(world));
        if (Net.I == null) return;
        var net = Net.I;
        var weakWorld = new WeakReference<World>(world);
        Action<int, int, List<PartyBbsEntry>>? handler = null;
        handler = (page, total, entries) =>
        {
            if (!weakWorld.TryGetTarget(out var w) || !GodotObject.IsInstanceValid(w)) { net.PartyBbsListEvent -= handler; return; }
            Listed(w);
        };
        net.PartyBbsListEvent += handler;
    }

    private static void Mirror(World world)
    {
        if (Native.Get<Button>(world, "_seekRegisterBtn") is { } register) register.SetMeta("seeking", Native.Get<bool>(world, "_seeking"));
        if (Native.Get<Label>(world, "_seekPageLbl") is { } page && page.Text.StartsWith("Page ") && page.Text.Contains(" / "))
        {
            int pages = Math.Max(1, (Native.Get<int>(world, "_seekTotal") + ListingsPerPage - 1) / ListingsPerPage);
            page.SetMeta("page_caption", $"{Native.Get<int>(world, "_seekPage") + 1} / {pages}");
        }
    }

    /// <summary>
    /// Describes the board's rows after the client listed a page of seek-party entries: the rows show the
    /// entries that pass the client's board filter, without the player's own listing.
    /// </summary>
    public static void Listed(World world, IReadOnlyList<PartyBbsEntry> _) => Listed(world);

    public static void Listed(World world)
    {
        if (Native.Get<VBoxContainer>(world, "_seekListBox") is not { } list) return;
        var all = Native.Get<List<PartyBbsEntry>>(world, "_seekEntries") ?? new List<PartyBbsEntry>();
        var filter = Native.Get<PartyBoardFilter>(world, "_seekFilter");
        int zone = Native.Get<int>(world, "_zone"), level = Net.I?.Sheet.Level ?? 0;
        string self = Net.I?.LastEnter.Name ?? "";
        var entries = all.Where(e => filter.Passes(e, zone, level) && !string.Equals(e.Name, self, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var old in list.GetChildren().Where(c => c.IsQueuedForDeletion()).ToArray()) list.RemoveChild(old);
        Mirror(world);
        var rows = list.GetChildren().OfType<PanelContainer>().ToArray();
        var classes = Native.Get<Array>(typeof(World), "WantedClasses");
        for (int i = 0; i < rows.Length && i < entries.Count; i++)
        {
            var e = entries[i];
            var panel = rows[i];
            string wanted = classes != null && classes.Length > 0
                ? Native.Get<string>(classes.GetValue(Math.Clamp(e.ClassOrWanted, 0, classes.Length - 1))!, "Item1") ?? "" : "";
            panel.SetMeta("seek_name", e.Name);
            panel.SetMeta("seek_level", e.IsLeaderRecruiting ? $"{e.MemberCount}/{MembersPerRecruitment}" : e.Level.ToString());
            panel.SetMeta("seek_class", e.IsLeaderRecruiting ? wanted : CharacterClassCatalog.DisplayName(e.ClassOrWanted));
            panel.SetMeta("seek_detail", $"{Native.Call(typeof(World), "SeekZoneName", e.ZoneId)}\n{e.Message}");
            if (panel.GetChildCount() > 0 && panel.GetChild(0) is HBoxContainer row && !row.GetChildren().OfType<Button>().Any(b => b.Text == "Private"))
            {
                string name = e.Name;
                var whisper = new Button { Text = "Private", Visible = false, FocusMode = Control.FocusModeEnum.None };
                whisper.AddThemeFontSizeOverride("font_size", 12);
                whisper.Pressed += () => { if (GodotObject.IsInstanceValid(world)) Native.Call(world, "OpenWhisperWith", name); };
                row.AddChild(whisper);
            }
        }
    }
}
