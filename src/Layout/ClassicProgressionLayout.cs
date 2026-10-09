using Godot;
using KnightOnlineUiClassic.Windows;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Achievements, attendance, rankings and genie have no original UIF. They are composed as Various-family
/// windows (outside close ornament, detail header rails, Friend surface, Clan list insets, nation tabs and
/// buttons) on the Character window's grid: 8-pixel margins, 36-pixel tabs, 22-pixel rows and 45-pixel item
/// cells. Every live native control is placed at one of the named rectangles below.
/// </summary>
public static class ClassicProgressionLayout
{
    public const int Margin = 8, ContentTop = 94, TabHeight = 36, Row = 22, Cell = 45;

    public static Vector2 Size(string id) => id switch
    {
        "achievements" => new(530, 560),
        "attendance" => new(492, 552),
        "rank" => new(360, 550),
        "genie" => new(530, 570),
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    public static Rect2 Tab(int index, int width = 84) => new(Margin + index * (width + 2), ContentTop, width, TabHeight);

    private static readonly Dictionary<string, Dictionary<string, Rect2>> Controls = new()
    {
        ["achievements"] = new()
        {
            ["ach_summary"] = new(14, 136, 380, Row),
            ["ach_hide_claimed"] = new(398, 136, 124, Row),
            ["ach_scroll"] = new(12, 166, 506, 362),
            ["ach_summary_page"] = new(12, 136, 506, 392),
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
            ["rank_title"] = new(Margin, ContentTop, 344, Row),
            ["rank_tab_karus"] = new(Margin, 118, 110, TabHeight),
            ["rank_tab_elmorad"] = new(120, 118, 110, TabHeight),
            ["rank_refresh"] = new(262, 125, 90, Row),
            ["rank_header_0"] = new(12, 163, 30, Row),
            ["rank_header_1"] = new(46, 163, 136, Row),
            ["rank_header_2"] = new(186, 163, 98, Row),
            ["rank_header_3"] = new(288, 163, 48, Row),
            ["rank_scroll"] = new(12, 188, 336, 286),
            ["rank_mine"] = new(12, 484, 336, Row),
            ["rank_status"] = new(12, 506, 336, 20),
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
            ["genie_tab_main"] = Tab(0),
            ["genie_tab_misc"] = Tab(1),
            ["genie_potion_title"] = new(14, 296, 240, 20),
            ["genie_mode_6"] = new(12, 318, 246, 20),
            ["genie_hp_item"] = new(14, 340, 242, 26),
            ["genie_hp_label"] = new(14, 368, 160, 22),
            ["genie_hp"] = new(176, 368, 80, 22),
            ["genie_mode_7"] = new(12, 392, 246, 20),
            ["genie_mp_item"] = new(14, 414, 242, 26),
            ["genie_mp_label"] = new(14, 442, 160, 22),
            ["genie_mp"] = new(176, 442, 80, 22),
            ["genie_monster_title"] = new(274, 296, 240, 20),
            ["genie_monster_list"] = new(274, 318, 242, 118),
            ["genie_monster_add"] = new(274, 442, 119, Row),
            ["genie_monster_remove"] = new(397, 442, 119, Row),
            ["genie_hint"] = new(12, 478, 506, 18),
            ["genie_run"] = new(12, 498, 506, 20),
            ["genie_start"] = new(12, 520, 96, Row),
            ["genie_save"] = new(112, 520, 120, Row),
            ["genie_spirit"] = new(372, 520, 150, Row),

            ["genie_settings_title"] = new(14, 140, 240, 20),
            ["genie_range_label"] = new(14, 162, 160, 22),
            ["genie_range"] = new(176, 162, 80, 22),
            ["genie_auto_party"] = new(12, 188, 246, 20),
            ["genie_ptcode_title"] = new(14, 212, 240, 20),
            ["genie_ptcode"] = new(14, 234, 242, Row),
            ["genie_ptcode_hint"] = new(14, 260, 242, 34),
            ["genie_combo_hint"] = new(14, 294, 242, 34),
            ["genie_leader_hint"] = new(14, 328, 242, 34),
            ["genie_daily_title"] = new(14, 376, 240, 20),
            ["genie_tip"] = new(14, 398, 242, 58),
            ["genie_daily_status"] = new(14, 458, 242, 34),
            ["genie_claim"] = new(14, 496, 160, Row),
            ["genie_scrolls_title"] = new(274, 140, 240, 20),
            ["genie_auto_scrolls"] = new(272, 162, 246, 20),
            ["genie_scroll_hint"] = new(274, 236, 242, 34),
            ["genie_scroll_status"] = new(274, 272, 242, 34),
            ["genie_hammer_title"] = new(274, 318, 240, 20),
            ["genie_auto_hammer"] = new(272, 340, 246, 20),
            ["genie_hammer_threshold_label"] = new(274, 364, 160, 22),
            ["genie_hammer_threshold"] = new(436, 364, 80, 22),
            ["genie_hammer_status"] = new(274, 390, 242, 34),
        };
        for (int group = 0; group < 3; group++)
        {
            int x = Margin + group * 172;
            rects["genie_skill_title_" + group] = new(x + 6, 140, 158, 20);
            for (int cell = 0; cell < 8; cell++)
                rects["genie_slot_" + (group * 8 + cell)] = new(x + 7 + cell % 4 * 40, 162 + cell / 4 * 40, 36, 36);
            int[] modes = { new[] { 0, 1, 2 }[group], new[] { 3, 4, 5 }[group] };
            rects["genie_mode_" + modes[0]] = new(x + 4, 242, 162, 20);
            rects["genie_mode_" + modes[1]] = new(x + 4, 262, 162, 20);
        }
        for (int i = 0; i < 4; i++) rects["genie_scroll_" + i] = new(278 + i * 49, 186, Cell, Cell);
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
        var surface = root.Find("surface")!; surface.W = root.W; surface.Y = top; surface.H = root.H - top;
        var rails = root.Find("header_rails")!; rails.Type = "classic_detail_header"; rails.Y = top; rails.W = root.W;
        void Section(string name, Rect2 rect) => root.Children.Add(Node("classic_section", name, rect));
        void Rule(string name, Rect2 rect) => root.Children.Add(Node("classic_rule", name, rect));
        switch (id)
        {
            case "achievements":
                Section("ach_list_frame", new(Margin, 162, 514, 370));
                break;
            case "attendance":
                Section("attendance_daily_frame", new(Margin, 120, 334, 404));
                Section("attendance_bonus_frame", new(350, 120, 134, 404));
                Rule("attendance_legend_rule", new(354, 442, 126, 2));
                break;
            case "rank":
                Section("rank_table_frame", new(Margin, 160, 344, 318));
                Rule("rank_header_rule", new(12, 186, 336, 2));
                int x = 12;
                for (int i = 0; i < RankColumns.Length - 1; i++) { x += RankColumns[i] + RankGap; Rule("rank_divider_" + i, new(x - 3, 164, 2, 310)); }
                break;
            case "genie":
                for (int group = 0; group < 3; group++) Section("genie_skill_frame_" + group, new(Margin + group * 172, 136, 170, 150));
                Section("genie_potion_frame", new(Margin, 292, 254, 180));
                Section("genie_monster_frame", new(268, 292, 254, 180));
                Section("genie_settings_frame", new(Margin, 136, 254, 232));
                Section("genie_daily_frame", new(Margin, 372, 254, 156));
                Section("genie_scrolls_frame", new(268, 136, 254, 174));
                Section("genie_hammer_frame", new(268, 314, 254, 118));
                break;
        }
        return root;
    }

    /// <summary>Genie insets shown on each page.</summary>
    public static bool GenieMiscFrame(string name) => name is "genie_settings_frame" or "genie_daily_frame" or "genie_scrolls_frame" or "genie_hammer_frame";
}
