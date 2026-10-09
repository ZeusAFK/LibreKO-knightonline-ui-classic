using Godot;
using KnightOnlineUiClassic.Windows;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Achievements, attendance, rankings and genie have no original UIF. They are composed as Character-window
/// family windows: the outside close ornament, the Character header rails carrying the window's tabs at the
/// Character tab geometry (85-pixel plates at an 87-pixel pitch), the window title as the centred page heading
/// under the rails, the Friend surface, Clan list insets, nation buttons, 8-pixel margins, 22-pixel rows and
/// 45-pixel item cells. Attendance has no tabs; its rails carry the centred title as the detail windows do.
/// Every live native control is placed at one of the named rectangles below.
/// </summary>
public static class ClassicProgressionLayout
{
    public const int Margin = 8, ContentTop = 94, TabHeight = 36, Row = 22, Cell = 45;

    public static Vector2 Size(string id) => id switch
    {
        "achievements" => new(530, 540),
        "attendance" => new(492, 552),
        "rank" => new(360, 504),
        "genie" => new(530, 550),
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    /// <summary>Top of the header rails' tab plates, as in the Character window.</summary>
    public static int TabTop => ClassicDesign.Karus ? 44 : 48;

    /// <summary>A tab plate on the header rails, at the Character window's tab geometry.</summary>
    public static Rect2 Tab(int index) => new(6 + index * 87, TabTop, 85, TabHeight);

    /// <summary>Number of tab plates on the rails of a window.</summary>
    public static int Tabs(string id) => id switch { "achievements" => 6, "attendance" => 0, _ => 2 };

    /// <summary>The window title: the centred page heading under the rails (Character page headings), or the rails themselves without tabs.</summary>
    public static Rect2 Heading(string id) => Tabs(id) > 0 ? new(12, 86, Size(id).X - 24, 24) : new(48, TabTop, Size(id).X - 96, TabHeight);

    private static readonly Dictionary<string, Dictionary<string, Rect2>> Controls = new()
    {
        ["achievements"] = new()
        {
            ["ach_summary"] = new(14, 116, 380, Row),
            ["ach_hide_claimed"] = new(398, 116, 124, Row),
            ["ach_scroll"] = new(12, 146, 506, 362),
            ["ach_summary_page"] = new(12, 116, 506, 392),
        },
        ["attendance"] = new()
        {
            ["attendance_daily_title"] = new(14, ContentTop, 110, Row),
            ["attendance_count"] = new(126, ContentTop, 210, Row),
            ["attendance_daily"] = new(12, 126, 326, 394),
            ["attendance_bonus_title"] = new(354, ContentTop, 130, Row),
            ["attendance_bonus"] = new(354, 126, 126, 236),
            ["attendance_notice"] = new(356, 368, 122, 74),
            ["attendance_legend_0"] = new(360, 450, 120, 20),
            ["attendance_legend_1"] = new(360, 474, 120, 20),
            ["attendance_legend_2"] = new(360, 498, 120, 20),
        },
        ["rank"] = new()
        {
            // The native ladder heading is the page heading; the window title stays on the drag strip unseen.
            ["rank_title"] = new(12, 86, 336, 24),
            ["rank_header_0"] = new(12, 117, 30, Row),
            ["rank_header_1"] = new(46, 117, 136, Row),
            ["rank_header_2"] = new(186, 117, 98, Row),
            ["rank_header_3"] = new(288, 117, 48, Row),
            ["rank_scroll"] = new(12, 142, 336, 286),
            ["rank_mine"] = new(12, 438, 240, Row),
            ["rank_status"] = new(12, 460, 240, 20),
            ["rank_refresh"] = new(262, 449, 90, Row),
        },
        ["genie"] = Genie(),
    };

    /// <summary>Column widths of a ranking row (place, name, clan, loyalty) with 4-pixel gaps; the scroll bar keeps its own lane.</summary>
    public static readonly int[] RankColumns = { 30, 136, 98, 48 };
    public const int RankGap = 4;

    private static Dictionary<string, Rect2> Genie()
    {
        var rects = new Dictionary<string, Rect2>
        {
            ["genie_potion_title"] = new(14, 276, 240, 20),
            ["genie_mode_6"] = new(12, 298, 246, 20),
            ["genie_hp_item"] = new(14, 320, 242, 26),
            ["genie_hp_label"] = new(14, 348, 160, 22),
            ["genie_hp"] = new(176, 348, 80, 22),
            ["genie_mode_7"] = new(12, 372, 246, 20),
            ["genie_mp_item"] = new(14, 394, 242, 26),
            ["genie_mp_label"] = new(14, 422, 160, 22),
            ["genie_mp"] = new(176, 422, 80, 22),
            ["genie_monster_title"] = new(274, 276, 240, 20),
            ["genie_monster_list"] = new(274, 298, 242, 118),
            ["genie_monster_add"] = new(274, 422, 119, Row),
            ["genie_monster_remove"] = new(397, 422, 119, Row),
            ["genie_hint"] = new(12, 458, 506, 18),
            ["genie_run"] = new(12, 478, 506, 20),
            ["genie_start"] = new(12, 500, 120, Row),
            ["genie_save"] = new(136, 500, 120, Row),
            ["genie_spirit"] = new(372, 500, 150, Row),

            ["genie_settings_title"] = new(14, 120, 240, 20),
            ["genie_range_label"] = new(14, 142, 160, 22),
            ["genie_range"] = new(176, 142, 80, 22),
            ["genie_auto_party"] = new(12, 168, 246, 20),
            ["genie_ptcode_title"] = new(14, 192, 240, 20),
            ["genie_ptcode"] = new(14, 214, 242, Row),
            ["genie_ptcode_hint"] = new(14, 240, 242, 34),
            ["genie_combo_hint"] = new(14, 274, 242, 34),
            ["genie_leader_hint"] = new(14, 308, 242, 34),
            ["genie_daily_title"] = new(14, 356, 240, 20),
            ["genie_tip"] = new(14, 378, 242, 58),
            ["genie_daily_status"] = new(14, 438, 242, 34),
            ["genie_claim"] = new(14, 476, 160, Row),
            ["genie_scrolls_title"] = new(274, 120, 240, 20),
            ["genie_auto_scrolls"] = new(272, 142, 246, 20),
            ["genie_scroll_hint"] = new(274, 216, 242, 34),
            ["genie_scroll_status"] = new(274, 252, 242, 34),
            ["genie_hammer_title"] = new(274, 298, 240, 20),
            ["genie_auto_hammer"] = new(272, 320, 246, 20),
            ["genie_hammer_threshold_label"] = new(274, 344, 160, 22),
            ["genie_hammer_threshold"] = new(436, 344, 80, 22),
            ["genie_hammer_status"] = new(274, 370, 242, 34),
        };
        for (int group = 0; group < 3; group++)
        {
            int x = Margin + group * 172;
            rects["genie_skill_title_" + group] = new(x + 6, 120, 158, 20);
            for (int cell = 0; cell < 8; cell++)
                rects["genie_slot_" + (group * 8 + cell)] = new(x + 7 + cell % 4 * 40, 142 + cell / 4 * 40, 36, 36);
            int[] modes = { new[] { 0, 1, 2 }[group], new[] { 3, 4, 5 }[group] };
            rects["genie_mode_" + modes[0]] = new(x + 4, 222, 162, 20);
            rects["genie_mode_" + modes[1]] = new(x + 4, 242, 162, 20);
        }
        for (int i = 0; i < 4; i++) rects["genie_scroll_" + i] = new(278 + i * 49, 166, Cell, Cell);
        return rects;
    }

    /// <summary>Named control rectangle, or null when the control is not placed by the layout.</summary>
    public static Rect2? Rect(string id, string name) =>
        Controls.TryGetValue(id, out var rects) && rects.TryGetValue(name, out var rect) ? rect : null;

    /// <summary>The genie page a control belongs to: the Misc page holds settings, scrolls, hammer and the daily reward.</summary>
    public static bool GenieMisc(string name) => name is "genie_settings_title" or "genie_range_label" or "genie_range" or "genie_auto_party"
        or "genie_ptcode_title" or "genie_ptcode" or "genie_ptcode_hint" or "genie_combo_hint" or "genie_leader_hint" or "genie_daily_title"
        or "genie_tip" or "genie_daily_status" or "genie_claim" or "genie_scrolls_title" or "genie_auto_scrolls" or "genie_scroll_hint"
        or "genie_scroll_status" or "genie_hammer_title" or "genie_auto_hammer" or "genie_hammer_threshold_label" or "genie_hammer_threshold"
        or "genie_hammer_status" || name.StartsWith("genie_scroll_") && name.Length == "genie_scroll_0".Length;

    private static LayoutNode Node(string type, string id, Rect2 rect) =>
        new() { Type = type, Id = id, X = (int)rect.Position.X, Y = (int)rect.Position.Y, W = (int)rect.Size.X, H = (int)rect.Size.Y };

    /// <summary>The window frame plus every list inset and rule of the window, sharing the Character window's components.</summary>
    public static LayoutNode Frame(string id)
    {
        var size = Size(id);
        var root = CharacterLayout.Frame();
        root.Id = "progression_" + id; root.W = (int)size.X; root.H = (int)size.Y;
        root.Children.RemoveAll(n => n.Type == "classic_tab");
        int top = ClassicDesign.Karus ? 40 : 44;
        var surface = root.Find("surface")!; surface.W = root.W;
        if (Tabs(id) == 0) surface.Y = top;
        surface.H = root.H - surface.Y;
        var rails = root.Find("header_rails")!; rails.W = root.W;
        if (Tabs(id) == 0) { rails.Type = "classic_detail_header"; rails.Y = top; }
        void Section(string name, Rect2 rect) => root.Children.Add(Node("classic_section", name, rect));
        void Rule(string name, Rect2 rect) => root.Children.Add(Node("classic_rule", name, rect));
        switch (id)
        {
            case "achievements":
                Section("ach_list_frame", new(Margin, 142, 514, 370));
                break;
            case "attendance":
                Section("attendance_daily_frame", new(Margin, 120, 334, 404));
                Section("attendance_bonus_frame", new(350, 120, 134, 404));
                Rule("attendance_legend_rule", new(354, 442, 126, 2));
                break;
            case "rank":
                Section("rank_table_frame", new(Margin, 114, 344, 318));
                Rule("rank_header_rule", new(12, 140, 336, 2));
                int x = 12;
                for (int i = 0; i < RankColumns.Length - 1; i++) { x += RankColumns[i] + RankGap; Rule("rank_divider_" + i, new(x - 3, 118, 2, 310)); }
                break;
            case "genie":
                for (int group = 0; group < 3; group++) Section("genie_skill_frame_" + group, new(Margin + group * 172, 116, 170, 150));
                Section("genie_potion_frame", new(Margin, 272, 254, 180));
                Section("genie_monster_frame", new(268, 272, 254, 180));
                Section("genie_settings_frame", new(Margin, 116, 254, 232));
                Section("genie_daily_frame", new(Margin, 352, 254, 156));
                Section("genie_scrolls_frame", new(268, 116, 254, 174));
                Section("genie_hammer_frame", new(268, 294, 254, 118));
                break;
        }
        return root;
    }

    /// <summary>Genie insets shown on each page.</summary>
    public static bool GenieMiscFrame(string name) => name is "genie_settings_frame" or "genie_daily_frame" or "genie_scrolls_frame" or "genie_hammer_frame";
}
