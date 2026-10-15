using System.Collections;
using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;

namespace KnightOnlineUiClassic.Native;

/// <summary>
/// Party HUD and seek-party board support: the native party actions the Classic party HUD offers beyond the
/// plugin party API (invite by name, party board, member menu), and on the board the listing data on the
/// native rows plus a private-message action per listing. The row meter, war board, row order, automatic
/// re-invite, commander bar and user vote go through <see cref="LibreKO.Plugins.IGameParty"/>.
/// </summary>
public static class NativeParty
{
    private const int MembersPerRecruitment = 8, ListingsPerPage = 10;

    /// <summary>The client's invite-by-name prompt (the Classic text prompt when the theme replaces it).</summary>
    public static void InviteByName(World? world) { if (world != null) Native.TryCall(world, "OpenInvitePrompt", out _); }

    /// <summary>The party board through the client's toggle, which also loads its list.</summary>
    public static void ToggleBoard(World? world) { if (world != null) Native.TryCall(world, "ToggleSeekParty", out _); }

    public static bool IsCommander(int memberId) => Net.I?.PartyCommanderId == memberId;

    /// <summary>Shows the native member menu as the Classic context menu, titled with the member's name.</summary>
    public static void PrepareMemberMenu(World? world, string name) =>
        NativeContextMenus.MirrorPartyMenu(world)?.SetMeta(KnightOnlineUiClassic.Windows.ClassicContextMenu.TitleMeta, name);

    public static void PrepareBoard(HudWindow window, World world)
    {
        if (Native.Get<VBoxContainer>(world, "_seekListBox") is not { } list) return;
        if (list.Name != "seek_members") list.Name = "seek_members";
        NativeWindows.Name(Native.Get<OptionButton>(world, "_wantedClassOpt"), "seek_wanted_class");
        NativeWindows.Name(Native.Get<LineEdit>(world, "_wantedMsgInput"), "seek_wanted_message");
        NativeWindows.Name(Native.Get<HBoxContainer>(world, "_seekFilters"), "seek_filters");
        if (Native.Get<HBoxContainer>(world, "_fullMoonRow") is { } fullMoon && NativeWindows.Name(fullMoon, "seek_full_moon"))
        {
            // Enter, Party Board and (3277df5) Auto Entry, which joins the first fitting Full Moon Rift party.
            var actions = fullMoon.GetChildren().OfType<Button>().ToArray();
            string[] names = { "seek_full_moon_enter", "seek_full_moon_shout", "seek_full_moon_auto" };
            for (int i = 0; i < actions.Length && i < names.Length; i++) actions[i].Name = names[i];
            if (actions.Length > 2) actions[2].TooltipText = "Join the first open Full Moon Rift party that fits your class and level";
        }
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
        Action<int, int, List<PartyBbsEntry>> handler = (page, total, entries) =>
        {
            if (weakWorld.TryGetTarget(out var w) && GodotObject.IsInstanceValid(w)) Listed(w);
        };
        NativeHooks.Listen("Net.PartyBbsListEvent", () => net.PartyBbsListEvent += handler, () => net.PartyBbsListEvent -= handler, world);
    }

    private static void Mirror(World world)
    {
        if (Native.Get<Button>(world, "_seekRegisterBtn") is { } register) register.SetMeta(HotNames.Seeking, Native.Get<bool>(world, "_seeking"));
        if (Native.Get<Label>(world, "_seekPageLbl") is { } page && page.Text.StartsWith("Page ") && page.Text.Contains(" / "))
        {
            int pages = Math.Max(1, (Native.Get<int>(world, "_seekTotal") + ListingsPerPage - 1) / ListingsPerPage);
            page.SetMeta(HotNames.PageCaption, $"{Native.Get<int>(world, "_seekPage") + 1} / {pages}");
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

/// <summary>Godot names used every frame in this file, created once (a literal creates a finalizable wrapper per call).</summary>
file static class HotNames
{
    internal static readonly StringName PageCaption = "page_caption";
    internal static readonly StringName Seeking = "seeking";
}
