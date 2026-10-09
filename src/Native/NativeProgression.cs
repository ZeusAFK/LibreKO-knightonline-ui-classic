using System.Collections;
using Godot;
using LibreKO;
using LibreKO.Network;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Native;

public static partial class NativeSetup
{
    static partial void RegisterProgression(PluginContext context)
    {
        foreach (var id in NativeProgression.WindowIds) NativeWindows.Prepare(id, NativeProgression.Prepare);
    }
}

/// <summary>
/// Prepares the native achievements, attendance, rankings and genie windows for the Classic skin: names
/// the native controls the skin places and mirrors the native selection, claim and request state into
/// metas. Every control keeps its own callback; the windows are only recomposed.
/// </summary>
public static class NativeProgression
{
    public static readonly string[] WindowIds = { "achievements", "attendance", "rank", "genie" };
    public const string Gate = "classic_progression_controls";

    public static void Prepare(HudWindow window, World world)
    {
        if (window.HasMeta(Gate)) return;
        bool ready = window.Id switch
        {
            "achievements" => PrepareAchievements(window, world),
            "attendance" => PrepareAttendance(window, world),
            "rank" => PrepareRank(window, world),
            "genie" => PrepareGenie(window, world),
            _ => false,
        };
        if (ready) window.SetMeta(Gate, 1);
    }

    private static bool Named(Node? node, string name) => NativeWindows.Name(node, name);

    private static bool PrepareAchievements(HudWindow window, World world)
    {
        var tabs = Native.Get<IDictionary>(world, "_achTabButtons");
        var list = Native.Get<VBoxContainer>(world, "_achList");
        if (tabs == null || list == null) return false;
        if (!Named(Native.Get<HBoxContainer>(world, "_achTabs"), "ach_tabs")
            || !Named(Native.Get<Button>(world, "_achSummaryTabButton"), "ach_tab_summary")
            || !Named(Native.Get<Label>(world, "_achSummary"), "ach_summary")
            || !Named(Native.Get<CheckButton>(world, "_achHideClaimed"), "ach_hide_claimed")
            || !Named(Native.Get<ScrollContainer>(world, "_achScroll"), "ach_scroll")
            || !Named(Native.Get<VBoxContainer>(world, "_achSummaryPage"), "ach_summary_page")) return false;
        list.Name = "ach_list";
        foreach (DictionaryEntry tab in tabs) Named(tab.Value as Node, "ach_tab_" + Convert.ToInt32(tab.Key));
        NativeWindows.Sync(window, () => SyncAchievements(window, world, tabs, list));
        return true;
    }

    private static void SyncAchievements(HudWindow window, World world, IDictionary tabs, VBoxContainer list)
    {
        bool summary = Native.Get<bool>(world, "_achOnSummary");
        int selected = summary ? -1 : Convert.ToInt32(Native.Get<object>(world, "_achTab") ?? 0);
        window.SetMeta("ach_selected", selected);
        var waiting = Native.Get<IDictionary>(world, "_achClaimablePerTab");
        foreach (DictionaryEntry tab in tabs)
            if (tab.Value is Button button)
            {
                int key = Convert.ToInt32(tab.Key);
                button.SetMeta("progression_selected", key == selected);
                button.SetMeta("progression_waiting", waiting != null && waiting.Contains(tab.Key) ? Convert.ToInt32(waiting[tab.Key]) : 0);
            }
        Native.Get<Button>(world, "_achSummaryTabButton")?.SetMeta("progression_selected", summary);
        // Rows follow the native visible order; mirror each entry's claim state onto its row.
        if (Native.Get<IList>(world, "_achVisible") is not { } visible) return;
        int index = 0;
        foreach (var row in list.GetChildren().OfType<PanelContainer>())
        {
            if (row.IsQueuedForDeletion()) continue;
            if (index >= visible.Count) break;
            var entry = (AchievementEntry)visible[index++]!;
            row.SetMeta("ach_row", entry.Id);
            row.SetMeta("ach_claimed", entry.Claimed);
            row.SetMeta("ach_claimable", entry.Claimable);
        }
    }

    private static bool PrepareAttendance(HudWindow window, World world)
    {
        var daily = Native.Get<GridContainer>(world, "_attendanceGrid");
        var bonus = Native.Get<GridContainer>(world, "_attendanceBonusRow");
        var count = Native.Get<Label>(world, "_attendanceCountLabel");
        if (daily == null || bonus == null || count == null || count.GetParent() is not HBoxContainer header) return false;
        if (!Named(Native.Get<Label>(world, "_attendanceNotice"), "attendance_notice")) return false;
        daily.Name = "attendance_daily";
        bonus.Name = "attendance_bonus";
        count.Name = "attendance_count";
        header.Name = "attendance_header";
        Named(header.GetChildren().OfType<Label>().FirstOrDefault(l => l != count), "attendance_daily_title");
        var body = window.Body;
        var children = body.GetChildren().OfType<Control>().ToList();
        int bonusSection = children.FindIndex(c => c.IsAncestorOf(bonus));
        if (bonusSection < 1) return false;
        Named(children[bonusSection - 1] as Label, "attendance_bonus_title");
        if (bonusSection + 1 < children.Count && children[bonusSection + 1] is HBoxContainer legend)
        {
            legend.Name = "attendance_legend";
            int i = 0;
            foreach (var item in legend.GetChildren().OfType<HBoxContainer>()) item.Name = "attendance_legend_" + i++;
        }
        NativeWindows.Sync(window, () => SyncAttendance(world, daily, bonus));
        return true;
    }

    private static void SyncAttendance(World world, GridContainer daily, GridContainer bonus)
    {
        var slots = Native.Get<int[]>(world, "_attendanceSlots");
        var states = Native.Get<byte[]>(world, "_attendanceStates");
        if (slots == null || states == null) return;
        void Mirror(GridContainer grid, int offset)
        {
            int index = offset;
            foreach (var cell in grid.GetChildren().OfType<VBoxContainer>())
            {
                if (cell.IsQueuedForDeletion()) continue;
                if (index >= states.Length) break;
                byte state = states[index];
                cell.SetMeta("attendance_slot", slots[index++]);
                cell.SetMeta("attendance_state", (int)state);
                cell.SetMeta("attendance_claimable", state is >= Net.AttendanceStateClaimable and <= Net.AttendanceStateClaimableExtra);
                cell.SetMeta("attendance_claimed", state is Net.AttendanceStateClaimed or Net.AttendanceStateExpired);
            }
        }
        Mirror(daily, 0);
        Mirror(bonus, Net.AttendanceDailySlots);
    }

    private static bool PrepareRank(HudWindow window, World world)
    {
        var karus = Native.Get<Button>(world, "_rankKarusTab");
        var elmorad = Native.Get<Button>(world, "_rankElmoradTab");
        var rows = Native.Get<VBoxContainer>(world, "_rankRows");
        if (karus == null || elmorad == null || rows == null || rows.GetParent() is not ScrollContainer scroll
            || karus.GetParent() is not HBoxContainer tabs) return false;
        if (!Named(Native.Get<Label>(world, "_rankMine"), "rank_mine") || !Named(Native.Get<Label>(world, "_rankStatus"), "rank_status")) return false;
        karus.Name = "rank_tab_karus";
        elmorad.Name = "rank_tab_elmorad";
        rows.Name = "rank_rows";
        scroll.Name = "rank_scroll";
        tabs.Name = "rank_tabs";
        Named(tabs.GetChildren().OfType<Button>().LastOrDefault(b => b != karus && b != elmorad), "rank_refresh");
        var body = window.Body.GetChildren().OfType<Control>().ToList();
        Named(body.FirstOrDefault() as Label, "rank_title");
        int at = body.IndexOf(tabs);
        if (at + 1 < body.Count && body[at + 1] is HBoxContainer header)
        {
            header.Name = "rank_header";
            int i = 0;
            foreach (var label in header.GetChildren().OfType<Label>()) label.Name = "rank_header_" + i++;
        }
        Named(body.OfType<HSeparator>().FirstOrDefault(), "rank_rule");
        return true;
    }

    private static bool PrepareGenie(HudWindow window, World world)
    {
        var slots = Native.Get<Button[]>(world, "_genieSlots");
        var modes = Native.Get<CheckButton[]>(world, "_genieModes");
        var start = Native.Get<Button>(world, "_genieStart");
        var monsters = Native.Get<ItemList>(world, "_genieMonsterList");
        var scrolls = Native.Get<Button[]>(world, "_genieScrollButtons");
        if (slots == null || modes == null || start == null || monsters == null || scrolls == null || slots.Any(s => s == null)) return false;
        if (slots[0].GetParent()?.GetParent() is not VBoxContainer firstColumn || firstColumn.GetParent() is not HBoxContainer columns
            || columns.GetParent() is not VBoxContainer main || main.GetParent() is not TabContainer tabs) return false;
        tabs.Name = "genie_tabs"; main.Name = "genie_main";
        if (tabs.GetChildren().OfType<VBoxContainer>().FirstOrDefault(c => c != main) is not { } misc) return false;
        misc.Name = "genie_misc";
        for (int i = 0; i < slots.Length; i++) slots[i].Name = "genie_slot_" + i;
        for (int i = 0; i < modes.Length; i++) Named(modes[i], "genie_mode_" + i);
        int column = 0;
        foreach (var box in columns.GetChildren().OfType<VBoxContainer>())
        {
            Named(box.GetChildren().OfType<Label>().FirstOrDefault(), "genie_skill_title_" + column);
            Named(box.GetChildren().OfType<GridContainer>().FirstOrDefault(), "genie_skill_grid_" + column++);
        }
        // Potion options: title, HP check, HP potion, HP threshold, MP check, MP potion, MP threshold.
        Named(Native.Get<Button>(world, "_genieHpSelect"), "genie_hp_item");
        Named(Native.Get<Button>(world, "_genieMpSelect"), "genie_mp_item");
        NameSpin(Native.Get<SpinBox>(world, "_genieHp"), "genie_hp");
        NameSpin(Native.Get<SpinBox>(world, "_genieMp"), "genie_mp");
        if (Native.Get<SpinBox>(world, "_genieHp")?.GetParent()?.GetParent() is VBoxContainer potions)
            Named(potions.GetChildren().OfType<Label>().FirstOrDefault(), "genie_potion_title");
        monsters.Name = "genie_monster_list";
        if (monsters.GetParent() is VBoxContainer monsterBox)
        {
            Named(monsterBox.GetChildren().OfType<Label>().FirstOrDefault(), "genie_monster_title");
            var actions = monsterBox.GetChildren().OfType<HBoxContainer>().FirstOrDefault()?.GetChildren().OfType<Button>().ToArray() ?? Array.Empty<Button>();
            if (actions.Length == 2) { actions[0].Name = "genie_monster_add"; actions[1].Name = "genie_monster_remove"; }
        }
        var run = Native.Get<Label>(world, "_genieRunLabel");
        Named(run, "genie_run");
        if (run != null && run.GetIndex() > 0) Named(main.GetChild(run.GetIndex() - 1) as Label, "genie_hint");
        start.Name = "genie_start";
        var footer = start.GetParent().GetChildren().OfType<Button>().ToArray();
        if (footer.Length == 3) { footer[1].Name = "genie_save"; footer[2].Name = "genie_spirit"; }
        NameSpin(Native.Get<SpinBox>(world, "_genieRange"), "genie_range");
        Named(Native.Get<CheckButton>(world, "_genieAutoParty"), "genie_auto_party");
        var code = Native.Get<LineEdit>(world, "_geniePtCode");
        Named(code, "genie_ptcode");
        Named(Native.Get<CheckButton>(world, "_genieAutoScrolls"), "genie_auto_scrolls");
        for (int i = 0; i < scrolls.Length; i++) Named(scrolls[i], "genie_scroll_" + i);
        Named(Native.Get<Label>(world, "_genieScrollStatus"), "genie_scroll_status");
        Named(Native.Get<CheckButton>(world, "_genieAutoHammer"), "genie_auto_hammer");
        NameSpin(Native.Get<SpinBox>(world, "_genieHammerThreshold"), "genie_hammer_threshold");
        Named(Native.Get<Label>(world, "_genieHammerStatus"), "genie_hammer_status");
        Named(Native.Get<Label>(world, "_genieTip"), "genie_tip");
        Named(Native.Get<Label>(world, "_genieStatus"), "genie_daily_status");
        Named(Native.Get<Button>(world, "_genieClaimBtn"), "genie_claim");
        // Misc is built in a fixed order; its section titles and hints precede or follow the named fields.
        var items = misc.GetChildren().OfType<Control>().ToList();
        void Before(Node? field, string name) { int at = field is Control c ? items.IndexOf(c) : -1; if (at > 0) Named(items[at - 1] as Label, name); }
        Before(Native.Get<SpinBox>(world, "_genieRange")?.GetParent(), "genie_settings_title");
        Before(code, "genie_ptcode_title");
        Before(Native.Get<CheckButton>(world, "_genieAutoScrolls"), "genie_scrolls_title");
        Before(Native.Get<CheckButton>(world, "_genieAutoHammer"), "genie_hammer_title");
        Before(Native.Get<Label>(world, "_genieTip"), "genie_daily_title");
        int hints = code is Control edit ? items.IndexOf(edit) : -1;
        string[] hintNames = { "genie_ptcode_hint", "genie_combo_hint", "genie_leader_hint" };
        for (int i = 0; hints >= 0 && i < hintNames.Length && hints + 1 + i < items.Count; i++) Named(items[hints + 1 + i] as Label, hintNames[i]);
        if (scrolls[0].GetParent() is HBoxContainer scrollRow)
        {
            scrollRow.Name = "genie_scroll_row";
            int at = items.IndexOf(scrollRow);
            if (at >= 0 && at + 1 < items.Count) Named(items[at + 1] as Label, "genie_scroll_hint");
        }
        Named(misc.GetChildren().OfType<HSeparator>().FirstOrDefault(), "genie_rule");
        Named(Native.Get<PopupMenu>(world, "_genieSkillPicker"), "genie_skill_picker");
        Named(Native.Get<PopupMenu>(world, "_geniePotionPicker"), "genie_potion_picker");
        Named(Native.Get<PopupMenu>(world, "_genieScrollPicker"), "genie_scroll_picker");
        // The pickers live on the genie layer; hand them to the skin through the window.
        int picker = 0;
        foreach (var menu in GeniePickers(world)) window.SetMeta("genie_picker_" + picker++, menu);
        NativeWindows.Sync(window, () =>
        {
            window.SetMeta("genie_pending", Native.Get<bool>(world, "_genieRequestPending"));
            window.SetMeta("genie_running", Net.I?.GenieRunning ?? false);
        });
        return true;
    }

    private static void NameSpin(SpinBox? spin, string name)
    {
        if (spin == null) return;
        spin.Name = name;
        if (spin.GetParent() is HBoxContainer row)
        {
            row.Name = name + "_row";
            Named(row.GetChildren().OfType<Label>().FirstOrDefault(), name + "_label");
        }
    }

    /// <summary>The native genie popups, which live on the genie layer rather than in the window.</summary>
    public static IEnumerable<PopupMenu> GeniePickers(World world)
    {
        foreach (var field in new[] { "_genieSkillPicker", "_geniePotionPicker", "_genieScrollPicker" })
            if (Native.Get<PopupMenu>(world, field) is { } menu) yield return menu;
    }
}
