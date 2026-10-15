using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>Castle siege NPC and list windows on the original warfare artwork; native controls keep their callbacks.</summary>
public static class ClassicSiegeSkin
{
    public static readonly string[] WindowIds = NativeSiege.WindowIds;
    public static void Extend(Control body) => body.Ready += () => Callable.From(() => Apply(body)).CallDeferred();
    public static ClassicSiegePanel? Apply(Control body)
    {
        Node? root = body;
        while (root != null && root is not HudWindow) root = root.GetParent();
        if (root is not HudWindow window || window.HasMeta("classic_siege")) return null;
        if (window.GetMeta(NativeSiege.Prepared, 0).AsInt32() != 1) return null;
        window.SetMeta("classic_siege", true);
        var panel = new ClassicSiegePanel(window, body); window.AddChild(panel); window.ResetSize(); return panel;
    }
}

public partial class ClassicSiegePanel : Control
{
    public static readonly Color Heading = new("e2d9c9"), Menu = new("80ffff"), MenuHover = new("ffe094"), Muted = new("c0c0c0");
    public HudWindow Window { get; }
    private readonly LayoutNode _art;
    private readonly string _artName;
    private readonly ClassicSiegeLayout.Table? _table;
    private readonly VBoxContainer? _rows;
    private readonly Control[] _controls;

    public ClassicSiegePanel(HudWindow window, Control body)
    {
        Window = window; Name = "classic_siege"; TextureFilter = TextureFilterEnum.Nearest;
        var speech = ClassicSiegeLayout.SpeechFor(window.Id);
        _table = ClassicSiegeLayout.TableFor(window.Id);
        _artName = speech?.Art ?? _table!.Art;
        _art = Plugin.Kit.Layout(_artName);
        Size = CustomMinimumSize = speech?.Size ?? ClassicSiegeLayout.Table.Size;
        _controls = CharacterDetailsSkin.Tree(window).OfType<Control>().ToArray();
        var bar = window.Header!.GetChildren().OfType<HBoxContainer>().Single();
        foreach (var child in window.GetChildren().OfType<Control>()) child.Visible = false;
        window.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        ClassicPartySkin.Drag(this, bar, speech?.Drag ?? ClassicSiegeLayout.Table.Drag);
        if (speech != null) BuildSpeech(speech);
        else BuildTable(_table!, out _rows);
        ShownOnly.Gate(this, StyleRows);
    }

    private T Find<T>(string name) where T : Control => _controls.OfType<T>().Single(c => c.Name == name);

    private void BuildSpeech(ClassicSiegeLayout.Speech layout)
    {
        // Speech sizes follow the original strings (10 pt on the guard, 11 pt on the Lord of Delos).
        var source = _art.Children.First(n => n.IsString && n.Bold);
        Text(Find<Label>("siege_greeting"), layout.Greeting, Plugin.Kit.Bold, UiKit.FontSize(source), Heading, HorizontalAlignment.Center, true);
        Text(Find<Label>("siege_question"), layout.Question, Plugin.Kit.Bold, UiKit.FontSize(source), Heading, HorizontalAlignment.Center, true);
        var option = _art.Children.First(n => n.IsString && !n.Bold);
        for (int i = 0; i < layout.Options.Length; i++)
        {
            var button = Find<Button>("siege_option_" + i);
            Place(button, layout.OptionRect(i));
            foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" }) button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            button.Alignment = HorizontalAlignment.Center; button.Icon = null;
            button.ClipText = true; button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            button.AddThemeFontOverride("font", Plugin.Kit.Regular); button.AddThemeFontSizeOverride("font_size", UiKit.FontSize(option));
            button.AddThemeConstantOverride("outline_size", 0); button.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
            button.AddThemeColorOverride("font_color", Menu); button.AddThemeColorOverride("font_focus_color", Menu);
            foreach (string state in new[] { "font_hover_color", "font_hover_pressed_color" }) button.AddThemeColorOverride(state, MenuHover);
            button.AddThemeColorOverride("font_pressed_color", Heading); button.AddThemeColorOverride("font_disabled_color", Muted);
            button.MouseDefaultCursorShape = CursorShape.PointingHand;
        }
    }

    private void BuildTable(ClassicSiegeLayout.Table layout, out VBoxContainer rows)
    {
        var title = Find<Label>("siege_title");
        Text(title, ClassicSiegeLayout.Table.Title, Plugin.Kit.Bold, 16, Heading, HorizontalAlignment.Center);
        Shadow(title, Colors.Black);
        ArtButton(Find<Button>("siege_close"), _art.Find("btn_close")!, ClassicSiegeLayout.Table.Close);
        for (int i = 0; i < layout.Heads.Length; i++)
        {
            var head = Find<Label>("siege_head_" + i);
            Text(head, layout.Heads[i], Plugin.Kit.Bold, 13, Heading, HorizontalAlignment.Center);
            // The original 31 px Members string cannot hold the word at the headings' 13 px; the short form keeps
            // every heading at one size and the full word on hover.
            if (head.Text == "Members") { head.Text = "Mem."; head.TooltipText = "Members"; head.MouseFilter = MouseFilterEnum.Pass; }
            // System fonts measure reliably once the heading is in the tree.
            Callable.From(() => Fit(head, 13, 10)).CallDeferred();
        }
        var scroll = Find<ScrollContainer>("siege_scroll");
        Place(scroll, layout.List);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        scroll.ClipContents = true; scroll.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        rows = Find<VBoxContainer>("siege_rows");
        rows.AddThemeConstantOverride("separation", 0);
        var rail = new ClassicSiegeScrollRail(scroll, _art.Find("scrollbar")!) { Name = "siege_scroll_rail", Position = layout.Scroll.Position, Size = layout.Scroll.Size };
        AddChild(rail); rail.SetMeta("siege_expected_rect", layout.Scroll);
        if (Window.Id == "siegechallengers")
        {
            string[] notes = { "siege_signed_up", "siege_chosen", "siege_fee", "siege_period" };
            for (int i = 0; i < notes.Length; i++)
            {
                var note = Find<Label>(notes[i]);
                Text(note, ClassicSiegeLayout.ChallengerNotes[i], Plugin.Kit.Regular, 12, Menu, HorizontalAlignment.Left, i is 1 or 3);
            }
            ArtButton(Find<Button>("siege_apply"), _art.Find("btn_application")!, layout.Actions[0]);
            ArtButton(Find<Button>("siege_apply_cancel"), _art.Find("btn_application_cancel")!, layout.Actions[0]);
            var defenders = Find<Button>("siege_defenders");
            ArtButton(defenders, _art.Find("btn_castlekeep")!, layout.Actions[1]);
            defenders.AutowrapMode = TextServer.AutowrapMode.WordSmart; defenders.ClipText = false;
        }
        else if (Window.Id == "siegeschedule")
        {
            ArtButton(Find<Button>("siege_ok"), _art.Find("btn_ok")!, layout.Actions[0]);
            ArtButton(Find<Button>("siege_refresh"), _art.Find("btn_refresh")!, layout.Actions[1]);
        }
        else
        {
            ArtButton(Find<Button>("siege_challengers"), _art.Find("btn_application")!, layout.Actions[0]);
            ArtButton(Find<Button>("siege_refresh"), _art.Find("btn_refresh")!, layout.Actions[1]);
        }
    }

    /// <summary>Moves a native control onto the artwork; its own visibility stays native-owned.</summary>
    private void Place(Control control, Rect2 rect)
    {
        bool visible = control.Visible;
        ClassicVendorSkin.Move(control, this, rect);
        control.Visible = visible;
        control.SetMeta("siege_expected_rect", rect);
    }

    private void Text(Label label, Rect2 rect, Font font, int size, Color color, HorizontalAlignment align, bool wrap = false)
    {
        Place(label, rect);
        label.AddThemeFontOverride("font", font); label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color); label.AddThemeConstantOverride("outline_size", 0);
        label.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); label.AddThemeConstantOverride("line_spacing", 0);
        label.HorizontalAlignment = align; label.VerticalAlignment = VerticalAlignment.Center;
        label.AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
    }

    private static void Shadow(Control control, Color color)
    {
        control.AddThemeColorOverride("font_shadow_color", color);
        control.AddThemeConstantOverride("shadow_offset_x", 1); control.AddThemeConstantOverride("shadow_offset_y", 1);
    }

    /// <summary>Steps a heading down to the largest size whose caption fits its cell.</summary>
    private static void Fit(Label label, int size, int minimum) => label.AddThemeFontSizeOverride("font_size", Fitting(label, size, minimum, label.Size.X));

    private static int Fitting(Label label, int size, int minimum, float width)
    {
        var font = label.GetThemeFont("font");
        // Bold is drawn emboldened; keep a little room beyond the measured advance.
        float room = font == Plugin.Kit.Bold ? 3 : 0;
        while (size > minimum && font.GetStringSize(label.Text, HorizontalAlignment.Left, -1, size).X + room > width) size--;
        return size;
    }

    /// <summary>Original stone button: four state images, black caption over the original light shadow.</summary>
    private void ArtButton(Button button, LayoutNode source, Rect2 rect)
    {
        Place(button, rect);
        string[] states = { "normal", "pressed", "hover", "disabled" };
        for (int i = 0; i < states.Length; i++)
        {
            var image = source.Images.FirstOrDefault(n => n.Tag == i) ?? source.Images.First();
            var style = new StyleBoxTexture { Texture = Plugin.Kit.Texture(image.Texture!), RegionRect = new Rect2(image.SrcX, image.SrcY, image.SrcW, image.SrcH) };
            style.SetContentMarginAll(0);
            if (states[i] == "disabled") style.ModulateColor = new Color(.72f, .72f, .72f);
            button.AddThemeStyleboxOverride(states[i], style);
            if (states[i] == "pressed") button.AddThemeStyleboxOverride("hover_pressed", style);
        }
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Icon = null; button.FocusMode = FocusModeEnum.None; button.Alignment = HorizontalAlignment.Center;
        button.ClipText = true; button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        var caption = source.Children.FirstOrDefault(n => n.IsString && n.Color == Colors.Black);
        button.AddThemeFontOverride("font", Plugin.Kit.Bold);
        button.AddThemeFontSizeOverride("font_size", caption != null ? UiKit.FontSize(caption) : 13);
        button.AddThemeConstantOverride("outline_size", 0);
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            button.AddThemeColorOverride(state, Colors.Black);
        button.AddThemeColorOverride("font_disabled_color", new Color("3c3832"));
        Shadow(button, Muted);
    }

    /// <summary>Native rows are rebuilt on every reply; each new row gets the original list columns.</summary>
    private void StyleRows()
    {
        if (_table == null || _rows == null) return;
        var rows = _rows.GetChildren().OfType<PanelContainer>().ToArray();
        if (rows.All(r => r.HasMeta("siege_row"))) return;
        foreach (var row in rows)
        {
            if (row.HasMeta("siege_row")) continue;
            row.SetMeta("siege_row", true);
            row.MouseFilter = MouseFilterEnum.Pass;
            row.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            row.CustomMinimumSize = new Vector2(0, ClassicSiegeLayout.Table.RowHeight);
            if (row.GetChildren().OfType<HBoxContainer>().FirstOrDefault() is not { } line) continue;
            line.AddThemeConstantOverride("separation", (int)_table.Gap);
            var cells = line.GetChildren().OfType<Label>().ToArray();
            for (int i = 0; i < cells.Length && i < _table.Columns.Length; i++)
            {
                var cell = cells[i];
                cell.CustomMinimumSize = new Vector2(_table.Columns[i], ClassicSiegeLayout.Table.RowHeight);
                cell.SizeFlagsHorizontal = i == 0 ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
                cell.AddThemeFontOverride("font", Plugin.Kit.Regular); cell.AddThemeFontSizeOverride("font_size", 13);
                cell.AddThemeColorOverride("font_color", Colors.White); cell.AddThemeConstantOverride("outline_size", 0);
                var inset = new StyleBoxEmpty(); inset.ContentMarginLeft = i == 0 ? 3 : 0; cell.AddThemeStyleboxOverride("normal", inset);
                cell.HorizontalAlignment = i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                cell.ClipText = true; cell.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                cell.TooltipText = i == 0 ? cell.Text : ""; cell.MouseFilter = i == 0 ? MouseFilterEnum.Pass : MouseFilterEnum.Ignore;
            }
        }
        // Short columns (day, nation, members) step down together so one long value never shrinks alone.
        var columns = rows.Select(r => CharacterDetailsSkin.Tree(r).OfType<Label>().ToArray()).ToArray();
        for (int i = 1; i < _table.Columns.Length; i++)
        {
            var cells = columns.Where(c => c.Length > i).Select(c => c[i]).ToArray();
            if (cells.Length == 0) continue;
            int size = cells.Min(c => Fitting(c, 13, 10, _table.Columns[i] - 2));
            if (size < 12 && cells.All(c => Weekdays.Contains(c.Text) || c.Text.Length <= 3))
            {
                // "Wednesday" cannot stay readable in the original 57 px day column; every day takes its short form.
                foreach (var cell in cells.Where(c => c.Text.Length > 3))
                { cell.TooltipText = cell.Text; cell.Text = cell.Text[..3]; cell.MouseFilter = MouseFilterEnum.Pass; }
                size = cells.Min(c => Fitting(c, 13, 10, _table.Columns[i] - 2));
            }
            foreach (var cell in cells) cell.AddThemeFontSizeOverride("font_size", size);
        }
    }

    private static readonly HashSet<string> Weekdays = new() { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

    public override void _Process(double delta) => StyleRows();

    public override void _Draw()
    {
        var backing = new Rect2(3, 3, Size.X - 6, Size.Y - 6);
        var pieces = _art.Children.Where(n => n.IsImage)
            .Select(part => (Plugin.Kit.Texture(part.Texture!)!, new Rect2(part.Position, part.SizeVec), new Rect2(part.SrcX, part.SrcY, part.SrcW, part.SrcH)));
        // The list windows straighten the crest and curls around the close button; talk windows keep their art.
        var straight = ClassicStraightArt.For(_artName);
        if (straight != null) { DrawTexture(ClassicStraightArt.Compose(_artName, (Vector2I)Size, backing, pieces, straight), Vector2.Zero); return; }
        DrawRect(backing, Colors.Black);
        foreach (var (texture, rect, source) in pieces) DrawTextureRectRegion(texture, rect, source);
    }
}

/// <summary>The warfare list scroll bar: original arrows and thumb bound to the native list's range.</summary>
public partial class ClassicSiegeScrollRail : Control
{
    private readonly ScrollContainer _scroll;
    private readonly TextureButton _up, _down;
    private readonly ClassicScrollTrack _track;
    private readonly Vector2 _upSize, _downSize;
    public VScrollBar Bar => _scroll.GetVScrollBar();
    public TextureButton Up => _up;
    public TextureButton Down => _down;
    public ClassicSiegeScrollRail(ScrollContainer scroll, LayoutNode source)
    {
        _scroll = scroll; MouseFilter = MouseFilterEnum.Stop;
        var arrows = source.Children.Where(n => n.IsButton).OrderBy(n => n.Y).ToArray();
        _up = Arrow(arrows[0], "siege_scroll_up"); _down = Arrow(arrows[1], "siege_scroll_down");
        _upSize = arrows[0].SizeVec; _downSize = arrows[1].SizeVec;
        _up.Pressed += () => Bar.Value -= ClassicSiegeLayout.Table.RowHeight;
        _down.Pressed += () => Bar.Value += ClassicSiegeLayout.Table.RowHeight;
        _track = new ClassicScrollTrack(Bar, source.Children.First(n => n.Type == "trackbar"), 16, false, ClassicSiegeLayout.Table.RowHeight) { Name = "siege_scroll_track" };
        AddChild(_up); AddChild(_down); AddChild(_track);
        Resized += Arrange;
    }
    private static TextureButton Arrow(LayoutNode source, string name) => new()
    {
        Name = name,
        TextureNormal = ClassicChatControls.Atlas(source.Images.First(n => n.Tag == 0)),
        TextureDisabled = ClassicChatControls.Atlas(source.Images.First(n => n.Tag == 0)),
        TexturePressed = ClassicChatControls.Atlas(source.Images.First(n => n.Tag == 1)),
        TextureHover = ClassicChatControls.Atlas(source.Images.First(n => n.Tag == 2)),
        FocusMode = FocusModeEnum.None, IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.Scale,
    };
    private void Arrange()
    {
        _up.Position = Vector2.Zero; _up.Size = _upSize;
        _down.Position = new Vector2(0, Size.Y - _downSize.Y); _down.Size = _downSize;
        _track.Position = new Vector2(0, _upSize.Y); _track.Size = new Vector2(Size.X, Size.Y - _upSize.Y - _downSize.Y);
    }
    public override void _Ready() { Arrange(); ShownOnly.Gate(this, Sync); }
    public override void _Process(double delta) => Sync();
    private void Sync()
    {
        if (_track.Size.Y != Size.Y - _upSize.Y - _downSize.Y) Arrange();
        // Keep whole rows at the top of the list; the list height is a whole number of rows.
        double row = ClassicSiegeLayout.Table.RowHeight, snapped = Math.Round(Bar.Value / row) * row;
        if (!Input.IsMouseButtonPressed(MouseButton.Left) && Math.Abs(Bar.Value - snapped) > .01) Bar.Value = snapped;
        _up.Disabled = Bar.Value <= Bar.MinValue;
        _down.Disabled = Bar.Value >= Math.Max(Bar.MinValue, Bar.MaxValue - Bar.Page);
        _up.SelfModulate = _up.Disabled ? new Color(1, 1, 1, .7f) : Colors.White;
        _down.SelfModulate = _down.Disabled ? new Color(1, 1, 1, .7f) : Colors.White;
        _track.QueueRedraw();
    }
}
