using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Native;

namespace KnightOnlineUiClassic.Windows;

/// <summary>Classic achievements, attendance, rankings and genie, composed from the live native controls.</summary>
public static class ClassicProgressionSkin
{
    public static readonly string[] WindowIds = NativeProgression.WindowIds;
    public static void Extend(Control body) => body.Ready += () => Callable.From(() => Apply(body)).CallDeferred();
    public static ClassicProgressionPanel? Apply(Control body)
    {
        Node? root = body; while (root != null && root is not HudWindow) root = root.GetParent();
        if (root is not HudWindow window || window.HasMeta("classic_progression") || window.GetMeta(NativeProgression.Gate, 0).AsInt32() != 1) return null;
        window.SetMeta("classic_progression", true);
        var panel = new ClassicProgressionPanel(window); window.AddChild(panel); window.ResetSize(); return panel;
    }
}

public partial class ClassicProgressionPanel : Control
{
    public HudWindow Window { get; }
    public LayoutView Frame { get; }
    private readonly string _id;
    private readonly Control[] _native;
    private readonly List<(Control Control, Rect2 Rect)> _fixed = new();
    private TabContainer? _genieTabs;
    private float _achTop;

    private static Color Gold => new("ffe094");
    private static Color Claimed => new("8ff099");
    private static Color RowRule => new(ClassicReportDesign.Caption, .32f);

    public ClassicProgressionPanel(HudWindow window)
    {
        Window = window; _id = window.Id; Name = "classic_progression";
        Size = CustomMinimumSize = ClassicProgressionLayout.Size(_id);
        TextureFilter = TextureFilterEnum.Nearest;
        _native = CharacterDetailsSkin.Tree(window).OfType<Control>().ToArray();
        var bar = window.Header!.GetChildren().OfType<HBoxContainer>().First();
        var close = bar.GetChildren().OfType<Button>().LastOrDefault(b => b.TooltipText == "Close");
        foreach (var child in window.GetChildren().OfType<Control>()) child.Visible = false;
        window.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        Frame = new LayoutView(Plugin.Kit, ClassicProgressionLayout.Frame(_id)); AddChild(Frame);
        if (Frame.Get("btn_close") is BaseButton ornament) ornament.Pressed += () => close?.EmitSignal(BaseButton.SignalName.Pressed);
        // The rails carry the window's tabs; separators close each plate unless the last one meets the rail's end.
        int tabs = ClassicProgressionLayout.Tabs(_id);
        if (tabs > 0 && Frame.Get("header_rails") is ClassicReportHeader rails)
            rails.Separators = 4 + tabs * 87 >= Size.X - 4 ? tabs - 1 : tabs;
        // The native header bar keeps native dragging and shows the window title as the centred heading.
        var heading = ClassicProgressionLayout.Heading(_id);
        bar.Reparent(this); bar.Position = heading.Position; bar.Size = heading.Size;
        bar.SetMeta("progression_expected_rect", heading);
        foreach (var control in bar.GetChildren().OfType<Control>())
        {
            control.Visible = control is Label { Text.Length: > 1 };
            if (control is Label title && title.Visible)
            {
                Caption(title); title.ClipText = true; title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                title.SizeFlagsHorizontal = SizeFlags.ExpandFill; title.HorizontalAlignment = HorizontalAlignment.Center;
            }
        }
        switch (_id)
        {
            case "achievements": Achievements(); break;
            case "attendance": Attendance(); break;
            case "rank": Rank(); break;
            case "genie": Genie(); break;
        }
    }

    public override void _Ready() => Frame.ApplyDeclaredBounds();

    private T? Find<T>(string name) where T : Control => _native.OfType<T>().FirstOrDefault(c => GodotObject.IsInstanceValid(c) && c.Name == name);

    private void Place(Control? control, Control? parent = null)
    {
        if (control == null || ClassicProgressionLayout.Rect(_id, control.Name) is not { } rect) return;
        bool visible = control.Visible;
        ClassicVendorSkin.Move(control, parent ?? this, rect);
        control.Visible = visible;
        control.SetMeta("progression_expected_rect", rect);
        // Wrapped text can transiently enlarge a container; it returns to its declared rectangle every frame.
        if (control is Container) _fixed.Add((control, rect));
    }

    private static void Font(Control control, bool bold = false, int size = 13)
    {
        control.AddThemeFontOverride("font", bold ? Plugin.Kit.Bold : Plugin.Kit.Regular);
        control.AddThemeFontSizeOverride("font_size", size);
        control.AddThemeConstantOverride("outline_size", 0);
        control.AddThemeConstantOverride("font_embolden", 0);
        control.AddThemeColorOverride("font_shadow_color", Colors.Black);
        control.AddThemeConstantOverride("shadow_offset_x", 1); control.AddThemeConstantOverride("shadow_offset_y", 1);
    }

    private static void Caption(Label? label)
    {
        if (label == null) return;
        Font(label, true); label.AddThemeColorOverride("font_color", ClassicReportDesign.Caption);
        label.VerticalAlignment = VerticalAlignment.Center;
    }

    private static void Text(Label? label, int size = 13, Color? color = null, bool wrap = false)
    {
        if (label == null) return;
        Font(label, false, size); label.AddThemeColorOverride("font_color", color ?? ClassicReportDesign.Value);
        label.AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
        label.VerticalAlignment = wrap ? VerticalAlignment.Top : VerticalAlignment.Center;
        if (!wrap) label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.ClipText = true;
    }

    private static void StyleButton(Button? button, int width = 0)
    {
        if (button == null) return;
        ClassicReportDesign.StyleButton(button, false);
        button.FocusMode = FocusModeEnum.None; button.ClipText = true; button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        button.AddThemeConstantOverride("icon_max_width", 20);
        if (width > 0) button.CustomMinimumSize = new Vector2(width, 22);
    }

    private static LayoutNode TabArt => Plugin.Kit.Layout("{nation}_various_frame_us").All().First(n => n.IsButton && n.Id == "btn_quest");

    /// <summary>Original Character-window tab art; the selected tab keeps the pressed plate.</summary>
    public static void StyleTab(Button button, bool selected, Color? ink = null)
    {
        ClassicReportDesign.StyleButton(button, true, TabArt);
        // Long native captions (category plus waiting count) step down to fit the original plate width.
        float width = (button.HasMeta("progression_expected_rect") ? button.GetMeta("progression_expected_rect").AsRect2().Size.X : button.Size.X) - 8;
        int size = 13;
        while (size > 10 && Plugin.Kit.Bold.GetStringSize(button.Text, HorizontalAlignment.Left, -1, size).X > width) size--;
        button.AddThemeFontSizeOverride("font_size", size);
        button.FocusMode = FocusModeEnum.None;
        if (selected)
        {
            var pressed = button.GetThemeStylebox("pressed");
            button.AddThemeStyleboxOverride("normal", pressed); button.AddThemeStyleboxOverride("hover", pressed);
        }
        if (ink is { } color)
            foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color" })
                button.AddThemeColorOverride(state, color);
        button.SetMeta("progression_tab_selected", selected);
    }

    public static void StyleCheck(BaseButton check)
    {
        Font(check);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            check.AddThemeColorOverride(state, ClassicReportDesign.Value);
        check.AddThemeColorOverride("font_disabled_color", ClassicDesign.Muted);
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" }) check.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        check.FocusMode = FocusModeEnum.None;
        var socket = Plugin.Kit.Layout("{nation}_chat_us").Find("btn_check_normal")!;
        foreach (var state in new[] { "unchecked", "checked", "unchecked_disabled", "checked_disabled", "unchecked_mirrored", "checked_mirrored" })
        {
            var art = socket.Images.First(i => i.Tag == (state.StartsWith("unchecked") ? 0 : 1));
            check.AddThemeIconOverride(state, new AtlasTexture { Atlas = Plugin.Kit.Texture(art.Texture!), Region = new Rect2(art.SrcX, art.SrcY, art.SrcW, art.SrcH), FilterClip = true });
        }
        if (check is Button text) text.Alignment = HorizontalAlignment.Left;
    }

    private static void StyleScroll(ScrollContainer scroll)
    {
        var bar = scroll.GetVScrollBar(); var track = new StyleBoxFlat { BgColor = new Color("17130e"), BorderColor = new Color(ClassicReportDesign.Caption, .6f) };
        track.SetBorderWidthAll(1); bar.AddThemeStyleboxOverride("scroll", track);
        foreach (string state in new[] { "grabber", "grabber_highlight", "grabber_pressed" })
        { var box = ClassicDesign.ButtonBox(state == "grabber_highlight" ? "hover" : state == "grabber_pressed" ? "pressed" : "normal"); box.SetContentMarginAll(0); bar.AddThemeStyleboxOverride(state, box); }
        bar.CustomMinimumSize = new Vector2(12, 0); bar.FocusMode = FocusModeEnum.None;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        scroll.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private void FixedScroll(ScrollContainer? scroll)
    {
        if (scroll == null) return;
        Place(scroll); StyleScroll(scroll);
    }

    /// <summary>The nation inventory cell: original gold or silver border around a black well.</summary>
    public static StyleBoxTexture CellBox(float light = 1)
    {
        bool karus = ClassicDesign.Karus;
        var box = new StyleBoxTexture { Texture = Plugin.Kit.Texture(karus ? "ui_ka_inven_us.png" : "ui_el_inven_us.png"),
            RegionRect = new Rect2(karus ? 159 : 157, karus ? 326 : 333, 51, 52), ModulateColor = new Color(light, light, light) };
        foreach (var side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom }) { box.SetTextureMargin(side, 4); box.SetContentMargin(side, 3); }
        return box;
    }

    private static void StyleInput(LineEdit input)
    {
        Font(input); input.AddThemeColorOverride("font_color", ClassicReportDesign.Value);
        input.AddThemeColorOverride("font_placeholder_color", ClassicDesign.Muted);
        var box = ClassicDesign.InputBox(); box.SetContentMargin(Side.Top, 1); box.SetContentMargin(Side.Bottom, 1);
        foreach (var state in new[] { "normal", "focus", "read_only" }) input.AddThemeStyleboxOverride(state, box);
        input.TextDirection = TextDirection.Ltr;
    }

    private static void StyleSpin(SpinBox? spin)
    {
        if (spin == null) return;
        StyleInput(spin.GetLineEdit()); spin.GetLineEdit().Alignment = HorizontalAlignment.Right;
        spin.AddThemeColorOverride("up_icon_modulate", ClassicReportDesign.Caption);
        spin.AddThemeColorOverride("down_icon_modulate", ClassicReportDesign.Caption);
    }

    public static void StylePopup(PopupMenu menu)
    {
        var panel = ClassicDesign.InputBox(); panel.BgColor = Colors.Black; panel.SetContentMarginAll(4);
        menu.AddThemeStyleboxOverride("panel", panel);
        var hover = new StyleBoxFlat { BgColor = ClassicReportDesign.SelectedRow, BorderColor = new Color("00ff00") }; hover.SetBorderWidthAll(1);
        menu.AddThemeStyleboxOverride("hover", hover);
        menu.AddThemeFontOverride("font", Plugin.Kit.Regular); menu.AddThemeFontSizeOverride("font_size", ClassicDesign.FontSize);
        menu.AddThemeColorOverride("font_color", ClassicReportDesign.Value); menu.AddThemeColorOverride("font_hover_color", Gold);
        menu.AddThemeConstantOverride("icon_max_width", 24); menu.AddThemeConstantOverride("v_separation", 6);
    }

    private static StyleBoxTexture? GaugeBox(int tag)
    {
        var art = Plugin.Kit.Layout("{nation}_statebar_us").Find("Progress_ExpP")?.Images.FirstOrDefault(i => i.Tag == tag);
        if (art?.Texture == null) return null;
        var box = new StyleBoxTexture { Texture = Plugin.Kit.Texture(art.Texture), RegionRect = new Rect2(art.SrcX, art.SrcY, art.SrcW, art.SrcH) };
        box.SetTextureMargin(Side.Left, 3); box.SetTextureMargin(Side.Right, 3); box.SetContentMarginAll(0);
        return box;
    }

    // ---- Achievements -----------------------------------------------------------------------------

    private void Achievements()
    {
        Place(Find<Label>("ach_summary")); Text(Find<Label>("ach_summary"), 13, ClassicReportDesign.Value);
        var hide = Find<CheckButton>("ach_hide_claimed"); Place(hide); if (hide != null) StyleCheck(hide);
        FixedScroll(Find<ScrollContainer>("ach_scroll"));
        // The list takes the list row beside the category column, not the client's fixed list size.
        if (Find<ScrollContainer>("ach_scroll") is { } listScroll) { listScroll.CustomMinimumSize = Vector2.Zero; listScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill; }
        Find<VBoxContainer>("ach_list")?.AddThemeConstantOverride("separation", 0);
        // Worn title, then search and status filters; the category column sits beside the list.
        if (Find<PanelContainer>("ach_title_banner") is { } banner)
        {
            Place(banner); banner.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            Find<HBoxContainer>("ach_title_row")?.AddThemeConstantOverride("separation", 8);
            var worn = Find<Label>("ach_title"); Text(worn, 13, Gold); if (worn != null) Font(worn, true);
            if (Find<Button>("ach_change_title") is { } change) { StyleButton(change, 120); change.SizeFlagsVertical = SizeFlags.ShrinkCenter; }
        }
        if (Find<HBoxContainer>("ach_filters") is { } filters)
        {
            Place(filters); filters.AddThemeConstantOverride("separation", 8);
            if (Find<LineEdit>("ach_search") is { } search) { StyleInput(search); search.CustomMinimumSize = new Vector2(0, ClassicProgressionLayout.Row); }
            if (Find<OptionButton>("ach_status") is { } status)
            {
                StyleButton(status, 120); status.Alignment = HorizontalAlignment.Left;
                status.AddThemeConstantOverride("arrow_margin", 6);
                StylePopup(status.GetPopup());
            }
        }
        if (Find<HBoxContainer>("ach_list_row") is { } listRow)
        {
            Place(listRow); listRow.AddThemeConstantOverride("separation", ClassicProgressionLayout.AchievementListGap);
            if (Find<PanelContainer>("ach_category_panel") is { } column)
            {
                var inset = new StyleBoxEmpty(); inset.SetContentMarginAll(0);
                column.AddThemeStyleboxOverride("panel", inset);
                column.CustomMinimumSize = new Vector2(ClassicProgressionLayout.AchievementCategory, 0);
            }
            Find<VBoxContainer>("ach_categories")?.AddThemeConstantOverride("separation", 0);
        }
        var page = Find<VBoxContainer>("ach_summary_page"); Place(page);
        if (page != null) { page.CustomMinimumSize = Vector2.Zero; page.ClipContents = true; }
        var tabs = new[] { Find<Button>("ach_tab_summary") }.Concat(Enumerable.Range(0, 5).Select(i => Find<Button>("ach_tab_" + i))).ToArray();
        for (int i = 0; i < tabs.Length; i++)
            if (tabs[i] is { } tab)
            {
                var rect = ClassicProgressionLayout.Tab(i);
                ClassicVendorSkin.Move(tab, this, rect); tab.SetMeta("progression_expected_rect", rect);
                tab.SetMeta("progression_tab", true);
            }
        SyncAchievements();
    }

    private void SyncAchievements()
    {
        foreach (var tab in _native.OfType<Button>().Where(b => GodotObject.IsInstanceValid(b) && b.HasMeta(HotNames.ProgressionTab)))
        {
            bool selected = tab.GetMeta(HotNames.ProgressionSelected, false).AsBool();
            int waiting = tab.GetMeta(HotNames.ProgressionWaiting, 0).AsInt32();
            // The native refresh replaces the tab plates whenever counts change.
            if (tab.GetThemeStylebox(HotNames.Normal) is StyleBoxFlat || tab.GetMeta(HotNames.ProgressionTabSelected, !selected).AsBool() != selected
                || tab.GetMeta(HotNames.ProgressionTabWaiting, -1).AsInt32() != waiting)
            {
                StyleTab(tab, selected, waiting > 0 ? new Color(ClassicDesign.Karus ? "7a160c" : "ffe094") : null);
                tab.SetMeta(HotNames.ProgressionTabWaiting, waiting);
            }
        }
        if (Find<VBoxContainer>("ach_list") is { } list)
            foreach (var child in list.GetChildren().OfType<Control>())
            {
                if (child.HasMeta(HotNames.ClassicProgressionRow)) continue;
                if (child is Label empty) { Text(empty, 13, ClassicDesign.Muted); empty.SetMeta(HotNames.ClassicProgressionRow, true); }
                else if (child is PanelContainer row && row.HasMeta(HotNames.AchRow)) StyleAchievementRow(row);
            }
        if (Find<VBoxContainer>("ach_categories") is { } categories)
            foreach (var row in categories.GetChildren().OfType<PanelContainer>()) StyleCategoryRow(row);
        if (Find<VBoxContainer>("ach_summary_page") is { } page) StyleSummary(page);
        if (Find<ScrollContainer>("ach_scroll") is { Visible: true } rowsScroll && Find<VBoxContainer>("ach_list") is { } rowsList) WholeRows(rowsScroll, rowsList);
        // The list insets belong to the category pages; the summary draws its own sections.
        bool listShown = Find<HBoxContainer>("ach_list_row") is { Visible: true };
        foreach (var name in new[] { "ach_list_frame", "ach_category_frame" })
            if (Frame.Get(name) is { } frame) frame.Visible = listShown;
    }

    /// <summary>
    /// Shows whole achievement rows only. Rows differ in height, so the list steps from row top to row top,
    /// hides a row cut by either edge and pads its end until the last row can scroll fully into view.
    /// </summary>
    private void WholeRows(ScrollContainer scroll, VBoxContainer list)
    {
        var rows = list.GetChildren().OfType<Control>().Where(c => c.Visible && !c.IsQueuedForDeletion()).ToArray();
        // Rows are laid out a frame after a native rebuild.
        if (rows.Length == 0 || rows.Any(r => r.Size.Y <= 0)) return;
        float page = scroll.Size.Y, height = rows[^1].Position.Y + rows[^1].Size.Y;
        var tops = rows.Select(r => r.Position.Y).ToArray();
        float end = height > page ? tops.Where(t => height - t <= page + .5f).DefaultIfEmpty(tops[^1]).First() : 0;
        float minimum = height > page ? end + page : 0;
        if (Math.Abs(list.CustomMinimumSize.Y - minimum) > .5f) list.CustomMinimumSize = new Vector2(0, minimum);
        var bar = scroll.GetVScrollBar();
        float value = (float)bar.Value;
        if (!Input.IsMouseButtonPressed(MouseButton.Left))
        {
            float target = value >= _achTop ? tops.Where(t => t >= value - .5f).DefaultIfEmpty(end).First() : tops.Where(t => t <= value + .5f).DefaultIfEmpty(0).Last();
            target = Math.Min(target, end);
            if (Math.Abs(value - target) > .01f) bar.Value = value = target;
            _achTop = target;
        }
        foreach (var row in rows)
        {
            bool whole = row.Position.Y >= value - .5f && row.Position.Y + row.Size.Y <= value + page + .5f;
            row.Modulate = whole ? Colors.White : Colors.Transparent;
            foreach (var button in CharacterDetailsSkin.Tree(row).OfType<BaseButton>()) button.MouseFilter = whole ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        }
    }

    /// <summary>A category entry of the list column: a whole 22-pixel row, gold and lit while chosen.</summary>
    private static void StyleCategoryRow(PanelContainer row)
    {
        if (row.IsQueuedForDeletion() || row.GetChildren().OfType<Label>().FirstOrDefault() is not { } label) return;
        // The native rebuild marks the chosen category in its bright gold.
        bool chosen = label.GetThemeColor("font_color").IsEqualApprox(UiTheme.GoldBright) || label.GetMeta("ach_category_chosen", false).AsBool();
        if (row.HasMeta("classic_progression_row")) return;
        row.SetMeta("classic_progression_row", true);
        label.SetMeta("ach_category_chosen", chosen);
        row.SetMeta("ach_category_chosen", chosen);
        var box = new StyleBoxFlat { BgColor = chosen ? ClassicReportDesign.SelectedRow : Colors.Transparent };
        box.SetContentMargin(Side.Left, 6); box.SetContentMargin(Side.Right, 4); box.SetContentMargin(Side.Top, 0); box.SetContentMargin(Side.Bottom, 0);
        row.AddThemeStyleboxOverride("panel", box);
        row.CustomMinimumSize = new Vector2(0, ClassicProgressionLayout.Row);
        Text(label, 12, chosen ? Gold : ClassicReportDesign.Value);
        if (chosen) Font(label, true, 12);
        label.TooltipText = label.Text; label.MouseFilter = MouseFilterEnum.Pass;
    }

    private static void StyleAchievementRow(PanelContainer row)
    {
        row.SetMeta("classic_progression_row", true);
        bool claimed = row.GetMeta("ach_claimed").AsBool(), claimable = row.GetMeta("ach_claimable").AsBool();
        var box = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = RowRule }; box.BorderWidthBottom = 1; box.SetContentMarginAll(0);
        row.AddThemeStyleboxOverride("panel", box);
        if (row.GetChildren().OfType<MarginContainer>().FirstOrDefault() is { } margin)
            foreach (var (side, value) in new[] { ("left", 6), ("top", 5), ("right", 10), ("bottom", 6) }) margin.AddThemeConstantOverride("margin_" + side, value);
        foreach (var columns in CharacterDetailsSkin.Tree(row).OfType<HBoxContainer>()) columns.AddThemeConstantOverride("separation", 8);
        var text = CharacterDetailsSkin.Tree(row).OfType<VBoxContainer>().FirstOrDefault();
        if (text != null)
        {
            text.AddThemeConstantOverride("separation", 1);
            var lines = text.GetChildren().OfType<Label>().ToArray();
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                bool title = i == 0, progress = line.Text.EndsWith(" pts"), reward = line.Text.StartsWith("Title:");
                Font(line, title, title ? 13 : 12);
                line.AddThemeColorOverride("font_color", title ? (claimed ? ClassicDesign.Muted : ClassicReportDesign.Value)
                    : progress ? (claimable ? Gold : ClassicReportDesign.Caption)
                    : reward ? (claimed ? Gold : ClassicDesign.Muted) : ClassicDesign.Muted);
            }
        }
        // The progress gauge: a thin dark track with a gold (complete: green) fill.
        if (text != null)
            foreach (var track in text.GetChildren().OfType<ColorRect>())
            {
                track.Color = new Color("2a241b");
                foreach (var fill in track.GetChildren().OfType<ColorRect>())
                    fill.Color = fill.AnchorRight >= 1 ? Claimed : ClassicReportDesign.Caption;
            }
        foreach (var reward in CharacterDetailsSkin.Tree(row).OfType<ItemSlotView>()) ClassicSlotLook.Attach(reward, CellBox);
        foreach (var action in row.GetChildren().OfType<MarginContainer>().SelectMany(m => m.GetChildren().OfType<HBoxContainer>()).SelectMany(h => h.GetChildren().OfType<Control>()).Where(c => c is Button or Label))
        {
            action.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            if (action is Button claim) { StyleButton(claim, 76); claim.Name = claim.Text == "Equip" ? "ach_equip" : "ach_claim"; }
            else if (action is Label state)
            {
                Font(state, false, 12); state.CustomMinimumSize = new Vector2(76, 22);
                state.HorizontalAlignment = HorizontalAlignment.Center; state.VerticalAlignment = VerticalAlignment.Center;
                state.AddThemeColorOverride("font_color", state.Text == "Worn" ? Gold : claimed ? ClassicDesign.Muted : ClassicReportDesign.Value);
            }
        }
    }

    private static void StyleSummary(Node node)
    {
        if (node.HasMeta("classic_progression_summary")) return;
        if (node is Control control && node.GetParent() != null) control.SetMeta("classic_progression_summary", true);
        switch (node)
        {
            case PanelContainer section:
                var empty = new StyleBoxEmpty(); empty.SetContentMarginAll(9); section.AddThemeStyleboxOverride("panel", empty);
                section.AddChild(new ClassicDetailInset(section)); break;
            case ProgressBar bar:
                if (GaugeBox(0) is { } under) bar.AddThemeStyleboxOverride("background", under);
                if (GaugeBox(1) is { } fill) bar.AddThemeStyleboxOverride("fill", fill);
                bar.CustomMinimumSize = new Vector2(0, 14); break;
            case Label label:
                int size = label.GetThemeFontSize("font_size"); var ink = label.GetThemeColor("font_color");
                bool heading = size >= 20, caption = !heading && ink.R > ink.B * 1.3f && ink.G > ink.B * 1.1f;
                Font(label, heading || caption, heading ? 24 : caption ? 13 : 12);
                label.AddThemeColorOverride("font_color", heading ? Gold : caption ? ClassicReportDesign.Caption : ClassicReportDesign.Value);
                break;
        }
        foreach (var child in node.GetChildren()) StyleSummary(child);
    }

    // ---- Attendance -------------------------------------------------------------------------------

    private void Attendance()
    {
        foreach (var name in new[] { "attendance_daily_title", "attendance_bonus_title" }) { var title = Find<Label>(name); Place(title); Caption(title); }
        var count = Find<Label>("attendance_count"); Place(count); Text(count); if (count != null) count.HorizontalAlignment = HorizontalAlignment.Right;
        var notice = Find<Label>("attendance_notice"); Place(notice); Text(notice, 12, new Color("ff9a80"), true);
        if (notice != null) notice.HorizontalAlignment = HorizontalAlignment.Center;
        foreach (var name in new[] { "attendance_daily", "attendance_bonus" })
            if (Find<GridContainer>(name) is { } grid)
            {
                Place(grid); grid.SizeFlagsHorizontal = SizeFlags.Fill;
                grid.AddThemeConstantOverride("h_separation", 0); grid.AddThemeConstantOverride("v_separation", name == "attendance_daily" ? 3 : 6);
            }
        for (int i = 0; i < 3; i++)
            if (Find<HBoxContainer>("attendance_legend_" + i) is { } item)
            {
                Place(item); item.AddThemeConstantOverride("separation", 6);
                foreach (var sample in item.GetChildren().OfType<Panel>())
                {
                    sample.AddThemeStyleboxOverride("panel", CellBox()); sample.CustomMinimumSize = new Vector2(18, 18);
                    if (i == 1) sample.AddChild(new ClassicProgressionMark(sample));
                }
                foreach (var label in item.GetChildren().OfType<Label>())
                    LegendText(label, 12, label.Text == "Acquired" ? Claimed : label.Text.StartsWith("Ready") ? Gold : ClassicReportDesign.Caption);
            }
        SyncAttendance();
    }

    private void SyncAttendance()
    {
        if (Find<GridContainer>("attendance_bonus") is { } bonus && bonus.Columns != 1) bonus.Columns = 1;
        foreach (var name in new[] { "attendance_daily", "attendance_bonus" })
            if (Find<GridContainer>(name) is { } grid)
                foreach (var cell in grid.GetChildren().OfType<VBoxContainer>())
                    if (!cell.HasMeta(HotNames.ClassicProgressionCell) && cell.HasMeta(HotNames.AttendanceState)) StyleAttendanceCell(cell, name == "attendance_bonus");
    }

    private static void StyleAttendanceCell(VBoxContainer cell, bool bonus)
    {
        cell.SetMeta("classic_progression_cell", true);
        bool claimable = cell.GetMeta("attendance_claimable").AsBool(), claimed = cell.GetMeta("attendance_claimed").AsBool();
        cell.CustomMinimumSize = new Vector2(bonus ? 126 : 65, 0); cell.AddThemeConstantOverride("separation", 2);
        var socket = CharacterDetailsSkin.Tree(cell).OfType<Button>().FirstOrDefault();
        if (socket != null)
        {
            socket.Name = "attendance_socket";
            socket.CustomMinimumSize = new Vector2(ClassicProgressionLayout.Cell, ClassicProgressionLayout.Cell);
            var normal = CellBox(); var lit = CellBox(claimable ? 1.25f : 1);
            foreach (var state in new[] { "normal", "disabled", "focus" }) socket.AddThemeStyleboxOverride(state, normal);
            socket.AddThemeStyleboxOverride("hover", lit); socket.AddThemeStyleboxOverride("pressed", lit);
            socket.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            foreach (var icon in socket.GetChildren().OfType<TextureRect>()) { icon.OffsetLeft = icon.OffsetTop = 3; icon.OffsetRight = icon.OffsetBottom = -3; }
            foreach (var label in socket.GetChildren().OfType<Label>())
            {
                bool count = label.HorizontalAlignment == HorizontalAlignment.Right;
                label.AddThemeFontOverride("font", Plugin.Kit.Bold); label.AddThemeFontSizeOverride("font_size", count ? 11 : 10);
                if (!count) label.AddThemeColorOverride("font_color", claimable ? Gold : ClassicReportDesign.Value);
            }
            if (claimable) socket.AddChild(new ClassicProgressionMark(socket));
        }
        foreach (var caption in cell.GetChildren().OfType<Label>())
        {
            Text(caption, 11, claimable ? Gold : claimed ? Claimed : ClassicReportDesign.Caption, true);
            // A wrapped caption measures its height at its own width, never at one pixel.
            caption.HorizontalAlignment = HorizontalAlignment.Center; caption.CustomMinimumSize = new Vector2(bonus ? 124 : 63, 26);
            caption.AddThemeConstantOverride("line_spacing", -2);
        }
    }

    private static void LegendText(Label label, int size, Color color)
    {
        Text(label, size, color); label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    // ---- Rank -------------------------------------------------------------------------------------

    private void Rank()
    {
        foreach (var side in new[] { "karus", "elmorad" })
        {
            var caption = Find<Label>("rank_caption_" + side); Place(caption); Caption(caption);
            if (caption != null) caption.HorizontalAlignment = HorizontalAlignment.Center;
            if (Find<HBoxContainer>("rank_header_" + side) is { } header) { Place(header); LadderLine(header, true); }
            for (int i = 0; i < ClassicProgressionLayout.Rank.Rows; i++)
                if (Find<HBoxContainer>($"rank_{side}_row_{i}") is { } line) { Place(line); LadderLine(line, false); }
            if (Find<HBoxContainer>($"rank_{side}_own") is { } own) { Place(own); LadderLine(own, false); }
        }
        var notice = Find<Label>("rank_notice"); Place(notice); Text(notice, 12, ClassicDesign.Muted, true);
        if (notice != null) notice.HorizontalAlignment = HorizontalAlignment.Center;
        SyncRank();
    }

    /// <summary>Gives a ladder heading or row the Classic column widths and type; the native labels keep their text.</summary>
    private static void LadderLine(HBoxContainer line, bool heading)
    {
        line.AddThemeConstantOverride("separation", ClassicProgressionLayout.Rank.Gap);
        line.CustomMinimumSize = new Vector2(0, ClassicProgressionLayout.Row);
        var cells = line.GetChildren().OfType<Control>().ToArray();
        var columns = ClassicProgressionLayout.Rank.Columns;
        for (int i = 0; i < cells.Length && i < columns.Length; i++)
        {
            var cell = cells[i];
            cell.CustomMinimumSize = new Vector2(columns[i], i == 1 && !heading ? 20 : 0);
            cell.SizeFlagsHorizontal = SizeFlags.Fill; cell.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            var align = i is 0 or 1 ? HorizontalAlignment.Center : i == 4 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            if (cell is Label label)
            {
                if (heading) Caption(label);
                else
                {
                    Text(label, 12, i == 3 ? ClassicReportDesign.Caption : ClassicReportDesign.Value);
                    if (i == 0) Font(label, true, 12);
                    label.MouseFilter = MouseFilterEnum.Pass;
                }
                label.HorizontalAlignment = align;
            }
            else if (cell is HBoxContainer score)
            {
                // Points and the red kill bonus share the right-aligned score column.
                score.AddThemeConstantOverride("separation", 3);
                score.Alignment = BoxContainer.AlignmentMode.End;
                var parts = score.GetChildren().OfType<Label>().ToArray();
                for (int p = 0; p < parts.Length; p++)
                {
                    Text(parts[p], 12, p == 0 ? Gold : new Color("ff8a7a"));
                    parts[p].HorizontalAlignment = HorizontalAlignment.Right;
                    // Points give way to the bonus when both do not fit; the bonus is never cut.
                    parts[p].ClipText = p == 0; parts[p].SizeFlagsHorizontal = p == 0 ? SizeFlags.ExpandFill : SizeFlags.ShrinkEnd;
                    if (p > 0) parts[p].TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
                }
                score.ClipContents = true;
            }
        }
    }

    private void SyncRank()
    {
        foreach (var side in new[] { "karus", "elmorad" })
            for (int i = 0; i <= ClassicProgressionLayout.Rank.Rows; i++)
            {
                string row = i < ClassicProgressionLayout.Rank.Rows ? $"rank_{side}_row_{i}" : $"rank_{side}_own";
                if (Find<Label>(row + "_rank") is not { } place) continue;
                int.TryParse(place.Text, out int at);
                var ink = at is > 0 and <= 3 ? Gold : ClassicReportDesign.Value;
                if (place.GetThemeColor(HotNames.FontColor) != ink) place.AddThemeColorOverride(HotNames.FontColor, ink);
                foreach (var cell in new[] { "_name", "_clan" })
                    if (Find<Label>(row + cell) is { } label && label.TooltipText != label.Text) label.TooltipText = label.Text;
                // The player's own row reads in the gold of the selected Character-window entries.
                if (i == ClassicProgressionLayout.Rank.Rows && Find<Label>(row + "_name") is { } self && self.GetThemeColor(HotNames.FontColor) != Gold)
                    ThemeNames.SetColor(self, HotNames.FontColor, Gold);
            }
    }

    // ---- Genie ------------------------------------------------------------------------------------

    private void Genie()
    {
        _genieTabs = Find<TabContainer>("genie_tabs");
        // Main, Misc (settings, potions, scrolls, daily reward) and Others (the client's "Set Others" rows and the hammer).
        string others = Find<Label>("genie_others_title")?.Text is { Length: > 0 } title ? title : "Others";
        string[] names = { "main", "misc", "others" }, captions = { "Main", "Misc", others };
        for (int i = 0; i < names.Length; i++)
        {
            var page = new Control { Name = "genie_" + names[i] + "_page", Size = Size, MouseFilter = MouseFilterEnum.Ignore, Visible = i == 0 };
            AddChild(page); _geniePages[i] = page;
            var tab = new ClassicReportButton { Name = "genie_tab_" + names[i], Text = captions[i], ClipText = true }; AddChild(tab); _genieTabButtons[i] = tab;
            var rect = ClassicProgressionLayout.Tab(i); tab.Position = rect.Position; tab.Size = rect.Size;
            tab.SetMeta("progression_expected_rect", rect); int index = i; tab.Pressed += () => GeniePage(index);
        }
        var controls = _native.Where(c => ClassicProgressionLayout.Rect("genie", c.Name) != null && !c.Name.ToString().StartsWith("genie_tab_")).ToArray();
        foreach (var control in controls) Place(control, _geniePages[ClassicProgressionLayout.GeniePage(control.Name)]);
        foreach (var control in controls)
        {
            string name = control.Name;
            switch (control)
            {
                case CheckButton check:
                    StyleCheck(check);
                    // Long native captions end in an ellipsis inside their row, with the full text as tooltip.
                    check.ClipText = true; check.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                    if (string.IsNullOrEmpty(check.TooltipText)) check.TooltipText = check.Text;
                    break;
                case CheckBox party when name.StartsWith("genie_party_"):
                    // The party check under a support cell: the check art alone, no caption.
                    StyleCheck(party); party.AddThemeFontSizeOverride("font_size", 9);
                    party.CustomMinimumSize = Vector2.Zero;
                    break;
                case SpinBox spin: StyleSpin(spin); break;
                case LineEdit edit: StyleInput(edit); edit.TooltipText = edit.PlaceholderText; break;
                case ItemList list: StyleMonsters(list); break;
                case Button slot when name.StartsWith("genie_slot_") || name.StartsWith("genie_scroll_"):
                    foreach (var state in new[] { "normal", "disabled" }) slot.AddThemeStyleboxOverride(state, CellBox());
                    slot.AddThemeStyleboxOverride("hover", CellBox(1.3f)); slot.AddThemeStyleboxOverride("pressed", CellBox(.8f));
                    slot.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
                    Font(slot, true, 15); slot.FocusMode = FocusModeEnum.None;
                    foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color" }) slot.AddThemeColorOverride(state, ClassicReportDesign.Caption);
                    slot.AddThemeConstantOverride("icon_max_width", name.StartsWith("genie_slot_") ? 30 : 39);
                    break;
                case Button button:
                    StyleButton(button);
                    if (name is "genie_hp_item" or "genie_mp_item" or "genie_pet_item") { button.Alignment = HorizontalAlignment.Left; }
                    break;
                case HBoxContainer rule when name.StartsWith("genie_other_"):
                    // A return-to-town rule: its check (clipped, with the full text as tooltip) and the potion count.
                    rule.AddThemeConstantOverride("separation", 8);
                    foreach (var toggle in rule.GetChildren().OfType<CheckButton>())
                    {
                        StyleCheck(toggle); toggle.AddThemeFontSizeOverride("font_size", 12);
                        toggle.ClipText = true; toggle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; toggle.TooltipText = toggle.Text;
                        toggle.CustomMinimumSize = Vector2.Zero; toggle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    }
                    foreach (var count in rule.GetChildren().OfType<SpinBox>()) { StyleSpin(count); count.CustomMinimumSize = new Vector2(80, ClassicProgressionLayout.Row); }
                    break;
                case Label label when name.Contains("_title"): Caption(label); break;
                case Label label when name is "genie_run": Text(label); Font(label, true); label.AddThemeColorOverride("font_color", Gold); break;
                case Label label when name.EndsWith("_label"): Text(label, 12); break;
                case Label label when name.EndsWith("_hint") || name is "genie_scroll_status" or "genie_hammer_status":
                    Text(label, 11, ClassicDesign.Muted, true); label.AddThemeConstantOverride("line_spacing", 0); break;
                case Label label: Text(label, 12, name is "genie_tip" ? ClassicReportDesign.Value : ClassicDesign.Muted, true); break;
            }
        }
        for (int i = 0; Window.HasMeta("genie_picker_" + i); i++)
            if (Window.GetMeta("genie_picker_" + i).AsGodotObject() is PopupMenu menu) StylePopup(menu);
        GeniePage(0);
    }

    private readonly Control[] _geniePages = new Control[3];
    private readonly Button[] _genieTabButtons = new Button[3];

    private void GeniePage(int shown)
    {
        if (_geniePages.Any(p => p == null)) return;
        for (int i = 0; i < _geniePages.Length; i++) { _geniePages[i].Visible = i == shown; StyleTab(_genieTabButtons[i], i == shown); }
        // Misc and Others are both the client's Misc tab.
        if (_genieTabs != null && _genieTabs.GetTabCount() > 1) _genieTabs.CurrentTab = shown == 0 ? 0 : 1;
        foreach (var (node, control) in Frame.Where(n => n.Id.StartsWith("genie_") && n.Type == "classic_section"))
            control.Visible = ClassicProgressionLayout.GenieFramePage(node.Id) == shown;
    }

    public int GenieShownPage => Array.FindIndex(_geniePages, p => p?.Visible == true);
    public bool GenieMiscShown => GenieShownPage == 1;

    private static void StyleMonsters(ItemList list)
    {
        var panel = ClassicDesign.InputBox(); panel.BgColor = Colors.Black; panel.SetContentMarginAll(3);
        list.AddThemeStyleboxOverride("panel", panel); list.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        var selected = new StyleBoxFlat { BgColor = ClassicReportDesign.SelectedRow, BorderColor = new Color("00ff00") }; selected.SetBorderWidthAll(1);
        foreach (var state in new[] { "selected", "selected_focus" }) list.AddThemeStyleboxOverride(state, selected);
        list.AddThemeStyleboxOverride("cursor", new StyleBoxEmpty()); list.AddThemeStyleboxOverride("cursor_unfocused", new StyleBoxEmpty());
        list.AddThemeFontOverride("font", Plugin.Kit.Regular); list.AddThemeFontSizeOverride("font_size", 13);
        list.AddThemeColorOverride("font_color", ClassicReportDesign.Value); list.AddThemeColorOverride("font_selected_color", Gold);
        list.AddThemeColorOverride("font_hovered_color", ClassicReportDesign.Value);
        list.AddThemeStyleboxOverride("hovered", new StyleBoxEmpty());
        list.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        StyleScrollBar(list.GetVScrollBar());
    }

    private static void StyleScrollBar(VScrollBar bar)
    {
        var track = new StyleBoxFlat { BgColor = new Color("17130e"), BorderColor = new Color(ClassicReportDesign.Caption, .6f) }; track.SetBorderWidthAll(1);
        bar.AddThemeStyleboxOverride("scroll", track);
        foreach (string state in new[] { "grabber", "grabber_highlight", "grabber_pressed" })
        { var box = ClassicDesign.ButtonBox(state == "grabber_highlight" ? "hover" : state == "grabber_pressed" ? "pressed" : "normal"); box.SetContentMarginAll(0); bar.AddThemeStyleboxOverride(state, box); }
        bar.CustomMinimumSize = new Vector2(12, 0);
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        // Native refreshes rebuild rows before deferred styling; restore fixed viewports after their minimum sizes settle.
        foreach (var (control, rect) in _fixed) { control.CustomMinimumSize = Vector2.Zero; if (control.Size != rect.Size) control.Size = rect.Size; }
        switch (_id)
        {
            case "achievements": SyncAchievements(); break;
            case "attendance": SyncAttendance(); break;
            case "rank": SyncRank(); break;
            case "genie":
                // The native status keeps its text; the Classic ink follows whether the reward is waiting.
                if (Find<Label>("genie_daily_status") is { } status && Find<Button>("genie_claim") is { } claim)
                    ThemeNames.SetColor(status, HotNames.FontColor, claim.Disabled ? ClassicDesign.Muted : Gold);
                break;
        }
    }
}

/// <summary>One-pixel gold outline that marks a reward ready to claim.</summary>
public partial class ClassicProgressionMark : Control
{
    private readonly Control _owner;
    public ClassicProgressionMark(Control owner) { _owner = owner; Name = "claim_mark"; MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Process(double delta) { if (Size != _owner.Size) { Position = Vector2.Zero; Size = _owner.Size; QueueRedraw(); } }
    public override void _Draw() => DrawRect(new Rect2(Vector2.One * 2, Size - Vector2.One * 4), new Color("ffe094"), false, 1);
}

/// <summary>Godot names used every frame in this file, created once (a literal creates a finalizable wrapper per call).</summary>
file static class HotNames
{
    internal static readonly StringName AchRow = "ach_row";
    internal static readonly StringName AttendanceState = "attendance_state";
    internal static readonly StringName ClassicProgressionCell = "classic_progression_cell";
    internal static readonly StringName ClassicProgressionRow = "classic_progression_row";
    internal static readonly StringName FontColor = "font_color";
    internal static readonly StringName Normal = "normal";
    internal static readonly StringName ProgressionSelected = "progression_selected";
    internal static readonly StringName ProgressionTab = "progression_tab";
    internal static readonly StringName ProgressionTabSelected = "progression_tab_selected";
    internal static readonly StringName ProgressionTabWaiting = "progression_tab_waiting";
    internal static readonly StringName ProgressionWaiting = "progression_waiting";
}
