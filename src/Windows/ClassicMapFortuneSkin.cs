using NativeApi = KnightOnlineUiClassic.Native.Native;
using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// World Map and Fortune Teller on the nation Zone List artwork. Neither has an original UIF; both reuse
/// the warp frame, inset, title, list rows, chat scroll bar and message buttons around the client's own
/// controls, which keep their callbacks.
/// </summary>
public static class ClassicMapFortuneSkin
{
    public static readonly string[] WindowIds = NativeMapFortune.WindowIds;
    public const string ExpectedRect = "mapfortune_expected_rect";
    public static void Extend(Control body) => body.Ready += () => Callable.From(() => Apply(body)).CallDeferred();
    public static ClassicMapFortunePanel? Apply(Control body)
    {
        if (NativeApi.WindowOf(body) is not { } window || window.HasMeta("classic_map_fortune")) return null;
        if (window.GetMeta(NativeMapFortune.Prepared, 0).AsInt32() != 1)
        {
            ClassicSkin.Apply(window);
            return null;
        }
        window.SetMeta("classic_map_fortune", true);
        ClassicMapFortunePanel panel = window.Id == "globalmap" ? new ClassicMapPanel(window) : new ClassicFortunePanel(window);
        window.AddChild(panel); window.ResetSize();
        return panel;
    }
}

/// <summary>Shared Zone List frame, warp title, original close button and native header dragging.</summary>
public abstract partial class ClassicMapFortunePanel : Control
{
    public HudWindow Window { get; }
    public Button CloseButton { get; }
    protected readonly Control[] Controls;

    protected ClassicMapFortunePanel(HudWindow window, Vector2 size, float insetBottom)
    {
        Window = window; Name = "classic_map_fortune"; TextureFilter = TextureFilterEnum.Nearest;
        Size = CustomMinimumSize = size;
        Controls = CharacterDetailsSkin.Tree(window).OfType<Control>().ToArray();
        var bar = window.Header!.GetChildren().OfType<HBoxContainer>().First();
        foreach (var child in window.GetChildren().OfType<Control>()) child.Visible = false;
        window.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        AddChild(new ClassicZoneFrame { Name = "mapfortune_frame", Size = size, InsetBottom = insetBottom });
        ClassicPartySkin.Drag(this, bar, ClassicMapFortuneLayout.Grip(size.X));
        var title = Find<Label>("mapfortune_title");
        Place(title, ClassicMapFortuneLayout.Title);
        Font(title, 16, ClassicDesign.Karus ? Colors.White : new Color("f9e486"));
        title.MouseFilter = MouseFilterEnum.Ignore;
        CloseButton = Find<Button>("mapfortune_close");
        var close = ClassicMapFortuneLayout.Close(size.X);
        ClassicMerchantSkin.Button(CloseButton, Plugin.Kit.Layout("co_questtalk_us").Find("btn_close")!, this, close, "");
        CloseButton.SetMeta(ClassicMapFortuneSkin.ExpectedRect, close);
        CloseButton.TooltipText = "Close";
    }

    protected T Find<T>(string name) where T : Control => Controls.OfType<T>().Single(c => c.Name == name);

    /// <summary>Moves a native control onto the artwork; its visibility stays native-owned.</summary>
    protected void Place(Control control, Rect2 rect, Control? parent = null)
    {
        bool visible = control.Visible;
        ClassicVendorSkin.Move(control, parent ?? this, rect);
        control.Visible = visible;
        control.SetMeta(ClassicMapFortuneSkin.ExpectedRect, rect);
    }

    /// <summary>Positions a control inside a native container by panel coordinates, without reparenting it.</summary>
    protected void Local(Control control, Rect2 rect)
    {
        var offset = Vector2.Zero;
        for (var node = control.GetParent() as Control; node != null && node != this; node = node.GetParent() as Control) offset += node.Position;
        control.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        control.CustomMinimumSize = Vector2.Zero;
        control.Position = rect.Position - offset; control.Size = rect.Size;
        Callable.From(() => control.Size = rect.Size).CallDeferred();
        control.SetMeta(ClassicMapFortuneSkin.ExpectedRect, rect);
    }

    /// <summary>Moves a native control into a stacking container; its visibility stays native-owned.</summary>
    protected static void Stack(Control control, Container parent)
    {
        bool visible = control.Visible;
        control.Reparent(parent, false); control.Visible = visible;
        control.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        control.CustomMinimumSize = Vector2.Zero; control.Position = Vector2.Zero;
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    public static void Font(Control control, int size, Color ink, bool bold = true)
    {
        control.AddThemeFontOverride("font", bold ? Plugin.Kit.Bold : Plugin.Kit.Regular);
        control.AddThemeFontSizeOverride("font_size", size);
        control.AddThemeConstantOverride("outline_size", 0);
        control.AddThemeColorOverride("font_color", ink);
        control.AddThemeColorOverride("font_shadow_color", Colors.Black);
        control.AddThemeConstantOverride("shadow_offset_x", 1); control.AddThemeConstantOverride("shadow_offset_y", 1);
        if (control is not Label label) return;
        label.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        label.AddThemeConstantOverride("line_spacing", 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        label.ClipText = true; label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
    }
}

/// <summary>World Map: the native zone population rows as a Zone List table with the nation scroll bar.</summary>
public partial class ClassicMapPanel : ClassicMapFortunePanel
{
    public static Color Karus => new("ff9292");
    public static Color ElMorad => new("9cd8f0");
    private const string TotalCaption = "Total players online";
    private readonly VBoxContainer _rows;
    private readonly Label _total;
    public ClassicMapScrollRail Rail { get; }

    public ClassicMapPanel(HudWindow window) : base(window, ClassicMapFortuneLayout.Map.Size, ClassicMapFortuneLayout.Map.InsetBottom)
    {
        for (int i = 0; i < ClassicMapFortuneLayout.Map.Columns.Length; i++)
        {
            var head = Find<Label>("map_head_" + i);
            Place(head, ClassicMapFortuneLayout.Map.Head(i));
            Font(head, 13, ClassicDesign.Heading);
            Column(head, i);
        }
        AddChild(new ClassicSurface(true) { Name = "map_head_rule", Position = ClassicMapFortuneLayout.Map.HeadRule.Position, Size = ClassicMapFortuneLayout.Map.HeadRule.Size });
        var scroll = Find<ScrollContainer>("map_scroll");
        Place(scroll, ClassicMapFortuneLayout.Map.List);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        scroll.ClipContents = true; scroll.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _rows = Find<VBoxContainer>("map_rows");
        _rows.AddThemeConstantOverride("separation", 0);
        Rail = new ClassicMapScrollRail(scroll, Plugin.Kit.Layout("{nation}_chat_us").Find("scroll")!) { Name = "map_scroll_rail" };
        AddChild(Rail);
        Rail.Position = ClassicMapFortuneLayout.Map.Scroll.Position; Rail.Size = ClassicMapFortuneLayout.Map.Scroll.Size;
        Rail.SetMeta(ClassicMapFortuneSkin.ExpectedRect, ClassicMapFortuneLayout.Map.Scroll);
        _total = new Label { Name = "map_total", HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_total);
        Font(_total, 13, ClassicDesign.Heading);
        _total.Position = ClassicMapFortuneLayout.Map.Total.Position; _total.Size = ClassicMapFortuneLayout.Map.Total.Size;
        // The theme font resolves in the tree; settle the size once it has.
        Callable.From(() => _total.Size = ClassicMapFortuneLayout.Map.Total.Size).CallDeferred();
        _total.SetMeta(ClassicMapFortuneSkin.ExpectedRect, ClassicMapFortuneLayout.Map.Total);
        MoveChild(CloseButton, -1);
        ShownOnly.Gate(this, StyleRows);
    }

    private static void Column(Label cell, int column)
    {
        var inset = new StyleBoxEmpty { ContentMarginLeft = column == 0 ? 4 : 0 };
        cell.AddThemeStyleboxOverride("normal", inset);
        cell.HorizontalAlignment = column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Center;
    }

    /// <summary>Native rows are rebuilt on every reply; new rows get the list columns and Classic ink.</summary>
    private void StyleRows()
    {
        var live = _rows.GetChildren().OfType<Control>().Where(c => !c.IsQueuedForDeletion()).ToArray();
        string total = "";
        foreach (var child in live)
        {
            if (child is Label note)
            {
                // The native total moves below the inset; the empty-list notice stays in the list.
                if (note.Text.StartsWith(TotalCaption, StringComparison.Ordinal)) { note.Visible = false; total = note.Text; }
                else if (!note.HasMeta("map_row"))
                {
                    note.SetMeta("map_row", true);
                    Font(note, 13, ClassicDesign.Muted, false);
                    note.HorizontalAlignment = HorizontalAlignment.Center;
                    note.CustomMinimumSize = new Vector2(0, ClassicMapFortuneLayout.Map.List.Size.Y);
                }
                continue;
            }
            if (child is not PanelContainer row || row.HasMeta("map_row")) continue;
            row.SetMeta("map_row", true);
            row.MouseFilter = MouseFilterEnum.Pass;
            row.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            row.CustomMinimumSize = new Vector2(0, ClassicMapFortuneLayout.Row);
            if (row.GetChildren().OfType<HBoxContainer>().FirstOrDefault() is not { } line) continue;
            line.AddThemeConstantOverride("separation", 0);
            var cells = line.GetChildren().OfType<Label>().ToArray();
            for (int i = 0; i < cells.Length && i < ClassicMapFortuneLayout.Map.Columns.Length; i++)
            {
                var cell = cells[i];
                var ink = i switch
                {
                    0 => ClassicDesign.Text,
                    1 => cell.Text is "0" ? ClassicDesign.Muted : ClassicDesign.Text,
                    _ => cell.Text switch { "Karus" => Karus, "El Morad" => ElMorad, _ => ClassicDesign.Muted },
                };
                Font(cell, 13, ink, false);
                Column(cell, i);
                cell.CustomMinimumSize = new Vector2(ClassicMapFortuneLayout.Map.Columns[i], ClassicMapFortuneLayout.Row);
                cell.SizeFlagsHorizontal = i == 0 ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
                cell.TooltipText = i == 0 ? cell.Text : "";
                cell.MouseFilter = i == 0 ? MouseFilterEnum.Pass : MouseFilterEnum.Ignore;
            }
        }
        _total.Text = total;
    }

    public override void _Process(double delta) => StyleRows();
}

/// <summary>Fortune Teller: the native card fan, turn and reveal animations and reading inside the Zone List inset.</summary>
public partial class ClassicFortunePanel : ClassicMapFortunePanel
{
    public ClassicFortunePanel(HudWindow window) : base(window, ClassicMapFortuneLayout.Fortune.Size, ClassicMapFortuneLayout.Fortune.InsetBottom)
    {
        var canvas = Find<Control>("fortune_canvas");
        ClassicVendorSkin.Move(canvas, this, new Rect2(Vector2.Zero, Size));
        canvas.ClipContents = false; canvas.MouseFilter = MouseFilterEnum.Ignore;
        // Cards keep their native step and hover lift; the client only rewrites their vertical position.
        var pick = Find<Control>("fortune_pick");
        pick.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft); pick.MouseFilter = MouseFilterEnum.Ignore;
        float drop = ClassicMapFortuneLayout.Fortune.CardTop - ClassicMapFortuneLayout.Fortune.NativeCardTop;
        pick.Position = new Vector2(0, drop); pick.Size = Size - new Vector2(0, drop);
        var cards = pick.GetChildren().OfType<TextureButton>().ToArray();
        float step = cards.Length > 1 ? cards[1].Position.X - cards[0].Position.X : 0;
        for (int i = 0; i < cards.Length; i++)
        {
            var rect = new Rect2(ClassicMapFortuneLayout.Fortune.CardLeft + i * step, ClassicMapFortuneLayout.Fortune.CardTop, cards[i].Size);
            cards[i].Position = new Vector2(rect.Position.X, cards[i].Position.Y);
            cards[i].SetMeta(ClassicMapFortuneSkin.ExpectedRect, rect);
            cards[i].MouseDefaultCursorShape = CursorShape.PointingHand;
        }
        var greeting = Find<Label>("fortune_greeting");
        Local(greeting, ClassicMapFortuneLayout.Fortune.Greeting); Font(greeting, 14, ClassicDesign.Heading);
        greeting.HorizontalAlignment = HorizontalAlignment.Center;
        var prompt = Find<Label>("fortune_prompt");
        Local(prompt, ClassicMapFortuneLayout.Fortune.Prompt); Font(prompt, 13, ClassicDesign.Text);
        prompt.HorizontalAlignment = HorizontalAlignment.Center;

        var stage = Find<Control>("fortune_stage");
        stage.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft); stage.MouseFilter = MouseFilterEnum.Ignore;
        stage.Position = Vector2.Zero; stage.Size = Size;
        foreach (string name in new[] { "fortune_face", "fortune_spin", "fortune_final" }) Local(Find<Control>(name), ClassicMapFortuneLayout.Fortune.Face);
        var reveal = Find<Label>("fortune_reveal");
        Local(reveal, ClassicMapFortuneLayout.Fortune.Reveal); Font(reveal, 13, ClassicDesign.Muted);
        reveal.HorizontalAlignment = HorizontalAlignment.Center;
        // Stars, title and the wrapped reading lines stack in one column centred beside the card face.
        var reading = new VBoxContainer { Name = "fortune_reading", Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        reading.AddThemeConstantOverride("separation", 8);
        stage.AddChild(reading);
        Local(reading, ClassicMapFortuneLayout.Fortune.Reading);
        var stars = Find<HBoxContainer>("fortune_stars");
        Stack(stars, reading); stars.CustomMinimumSize = new Vector2(0, 18);
        var title = Find<Label>("fortune_name");
        Stack(title, reading); Font(title, 15, ClassicDesign.Heading);
        title.CustomMinimumSize = new Vector2(0, 22); title.HorizontalAlignment = HorizontalAlignment.Center;
        var lines = new VBoxContainer { Name = "fortune_lines", MouseFilter = MouseFilterEnum.Ignore };
        lines.AddThemeConstantOverride("separation", 2);
        reading.AddChild(lines);
        foreach (var line in Controls.OfType<Label>().Where(l => l.Name.ToString().StartsWith("fortune_line_")).OrderBy(l => l.Name.ToString()))
        {
            Stack(line, lines);
            Font(line, 13, ClassicDesign.Text);
            line.ClipText = false; line.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
            line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            line.HorizontalAlignment = HorizontalAlignment.Center;
        }
        var ok = Find<Button>("fortune_ok");
        bool shown = ok.Visible;
        ClassicMerchantSkin.Button(ok, Plugin.Kit.Layout("{nation}_warp_us").Find("Btn_Ok")!, stage, ClassicMapFortuneLayout.Fortune.Ok, "OK");
        ok.Visible = shown;
        ok.AddThemeFontOverride("font", Plugin.Kit.Bold); ok.AddThemeFontSizeOverride("font_size", 15);
        ok.SetMeta(ClassicMapFortuneSkin.ExpectedRect, ClassicMapFortuneLayout.Fortune.Ok);
        MoveChild(CloseButton, -1);
    }
}

/// <summary>
/// The nation Zone List frame at any size: title band with the opening inset, inset sides, the inset's
/// closing strip, the plain lower band and the base. Side columns keep native pixels; the middle and the
/// vertical runs tile the original strips.
/// </summary>
public partial class ClassicZoneFrame : Control
{
    private const float Side = 40;
    public float InsetBottom { get; set; }
    // Row of the inset's lower border inside the warp closing strip, measured on the original artwork.
    private static float ClosingLine => ClassicDesign.Karus ? 23 : 15;

    public ClassicZoneFrame() { MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.Nearest; Resized += QueueRedraw; }

    public override void _Draw()
    {
        var pieces = Plugin.Kit.Layout("{nation}_warp_us").Children.Where(n => n.IsImage).ToArray();
        if (pieces.Length != 7) return;
        LayoutNode top = pieces[0], closing = pieces[2], bottom = pieces[3], inset = pieces[4], plain = pieces[6];
        DrawRect(new Rect2(3, 3, Size.X - 6, Size.Y - 6), Colors.Black);
        float closeY = InsetBottom - ClosingLine, plainY = closeY + closing.SrcH, baseY = Size.Y - bottom.SrcH;
        Strip(top, 0, top.SrcH);
        Strip(inset, top.SrcH, closeY - top.SrcH);
        Strip(closing, closeY, closing.SrcH);
        Strip(plain, plainY, baseY - plainY);
        Strip(bottom, baseY, bottom.SrcH);
    }

    private void Strip(LayoutNode piece, float y, float height)
    {
        if (height <= 0 || Plugin.Kit.Texture(piece.Texture!) is not { } texture) return;
        float middle = piece.SrcW - Side * 2;
        for (float dy = 0; dy < height; dy += piece.SrcH)
        {
            float h = Math.Min(piece.SrcH, height - dy);
            void Part(float x, float sx, float w) =>
                DrawTextureRectRegion(texture, new Rect2(x, y + dy, w, h), new Rect2(piece.SrcX + sx, piece.SrcY, w, h));
            Part(0, 0, Side);
            for (float x = Side; x < Size.X - Side; x += middle) Part(x, Side, Math.Min(middle, Size.X - Side - x));
            Part(Size.X - Side, piece.SrcW - Side, Side);
        }
    }
}

/// <summary>The nation chat scroll bar bound to a native list; it steps and snaps by whole rows.</summary>
public partial class ClassicMapScrollRail : Control
{
    private readonly ScrollContainer _scroll;
    private readonly ClassicScrollTrack _track;
    public VScrollBar Bar => _scroll.GetVScrollBar();
    public TextureButton Up { get; }
    public TextureButton Down { get; }

    public ClassicMapScrollRail(ScrollContainer scroll, LayoutNode source)
    {
        _scroll = scroll; MouseFilter = MouseFilterEnum.Stop;
        var arrows = source.Children.Where(n => n.IsButton).OrderBy(n => n.Y).ToArray();
        Up = Arrow(arrows[0], "map_scroll_up"); Down = Arrow(arrows[1], "map_scroll_down");
        Up.Pressed += () => Bar.Value -= ClassicMapFortuneLayout.Row;
        Down.Pressed += () => Bar.Value += ClassicMapFortuneLayout.Row;
        _track = new ClassicScrollTrack(Bar, source.Children.First(n => n.Type == "trackbar"), 18, false, ClassicMapFortuneLayout.Row) { Name = "map_scroll_track" };
        AddChild(Up); AddChild(Down); AddChild(_track);
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
        Up.Position = Vector2.Zero; Up.Size = new Vector2(Size.X, 18);
        Down.Position = new Vector2(0, Size.Y - 18); Down.Size = new Vector2(Size.X, 18);
        _track.Position = new Vector2(0, 18); _track.Size = new Vector2(Size.X, Size.Y - 36);
    }

    public override void _Ready() { Arrange(); ShownOnly.Gate(this, Sync); }

    public override void _Process(double delta) => Sync();

    private void Sync()
    {
        if (_track.Size.Y != Size.Y - 36) Arrange();
        // The list is a whole number of rows; keep a whole row at its top.
        double row = ClassicMapFortuneLayout.Row, snapped = Math.Round(Bar.Value / row) * row;
        if (!Input.IsMouseButtonPressed(MouseButton.Left) && Math.Abs(Bar.Value - snapped) > .01) Bar.Value = snapped;
        bool needed = Bar.MaxValue - Bar.MinValue > Bar.Page + .5;
        Up.Disabled = !needed || Bar.Value <= Bar.MinValue;
        Down.Disabled = !needed || Bar.Value >= Math.Max(Bar.MinValue, Bar.MaxValue - Bar.Page);
        Up.SelfModulate = Up.Disabled ? new Color(1, 1, 1, .7f) : Colors.White;
        Down.SelfModulate = Down.Disabled ? new Color(1, 1, 1, .7f) : Colors.White;
        _track.QueueRedraw();
    }
}
