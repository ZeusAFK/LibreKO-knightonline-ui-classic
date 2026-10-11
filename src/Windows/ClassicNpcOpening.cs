using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Native;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The original NPC opening windows (co_inn_us, co_smith_us, co_change_us): message art, the greeting in white
/// bold Arial and the choices as plain yellow text rows. Shared by the NPC dialogue stand-in and the
/// redistribution menu; the geometry of each NPC lives in <see cref="ClassicNpcOpeningLayout"/>.
/// </summary>
public static class ClassicNpcOpening
{
    /// <summary>The original string's face and size with the shadow the layout strings carry.</summary>
    public static void Ink(Control control, LayoutNode node)
    {
        control.AddThemeFontOverride("font", Plugin.Kit.FontFor(node));
        control.AddThemeFontSizeOverride("font_size", UiKit.FontSize(node));
        control.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .75f));
        control.AddThemeConstantOverride("shadow_offset_x", 1); control.AddThemeConstantOverride("shadow_offset_y", 1);
        control.AddThemeConstantOverride("outline_size", 0);
    }

    public static LayoutNode GreetingInk(ClassicNpcOpeningLayout layout) => Plugin.Kit.Layout(layout.Art).Find("Text_Title")!;
    public static LayoutNode RowInk(ClassicNpcOpeningLayout layout) => Plugin.Kit.Layout(layout.Art).Find(layout.RowInk)!;

    /// <summary>
    /// Shows <paramref name="greeting"/> in the original Text_Title ink and rectangle and returns how far the rows
    /// move down because the client's text needs more lines than the original string had room for.
    /// </summary>
    public static int Greeting(Label greeting, ClassicNpcOpeningLayout layout)
    {
        var title = GreetingInk(layout);
        greeting.AutowrapMode = TextServer.AutowrapMode.WordSmart; greeting.ClipText = true;
        greeting.MouseFilter = Control.MouseFilterEnum.Ignore; greeting.VerticalAlignment = VerticalAlignment.Top;
        greeting.HorizontalAlignment = HorizontalAlignment.Left;
        Ink(greeting, title); greeting.AddThemeColorOverride("font_color", title.Color);
        int shift = layout.Shift(TextHeight(greeting, layout.Greeting.Size.X));
        ClassicNpcLayout.Place(greeting, layout.GreetingRect(shift));
        return shift;
    }

    /// <summary>
    /// Height of every wrapped line of <paramref name="label"/> at <paramref name="width"/> (default: its current
    /// width), measured with its font so it also holds before the label has entered the tree.
    /// </summary>
    public static float TextHeight(Label label, float? width = null)
    {
        var font = label.GetThemeFont("font"); int size = label.GetThemeFontSize("font_size");
        float line = font.GetHeight(size);
        var text = font.GetMultilineStringSize(label.Text, HorizontalAlignment.Left, width ?? label.Size.X, size, -1,
            TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound | TextServer.LineBreakFlag.Adaptive);
        int lines = Math.Max(1, Mathf.RoundToInt(text.Y / line));
        return lines * line + (lines - 1) * label.GetThemeConstant("line_spacing");
    }

    /// <summary>A choice as an original text row: yellow, white under the pointer, frame gold and one pixel lower while pressed, grey when unavailable.</summary>
    public static void Row(Button row, ClassicNpcOpeningLayout layout)
    {
        var ink = RowInk(layout);
        row.FocusMode = Control.FocusModeEnum.None; row.Alignment = HorizontalAlignment.Left; row.Icon = null;
        row.ClipText = true; row.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
        {
            bool down = state is "pressed" or "hover_pressed";
            row.AddThemeStyleboxOverride(state, new StyleBoxEmpty { ContentMarginLeft = layout.CaptionInset, ContentMarginRight = layout.CaptionInset,
                ContentMarginTop = down ? 2 : 1, ContentMarginBottom = down ? 0 : 1 });
        }
        Ink(row, ink);
        row.AddThemeColorOverride("font_color", ink.Color); row.AddThemeColorOverride("font_focus_color", ink.Color);
        row.AddThemeColorOverride("font_hover_color", Colors.White);
        row.AddThemeColorOverride("font_pressed_color", ClassicNpcOpeningLayout.Pressed);
        row.AddThemeColorOverride("font_hover_pressed_color", ClassicNpcOpeningLayout.Pressed);
        row.AddThemeColorOverride("font_disabled_color", ClassicNpcOpeningLayout.Disabled);
    }

    /// <summary>True when the caption fits its row without being cut.</summary>
    public static bool Fits(Button row, ClassicNpcOpeningLayout layout) =>
        row.Size.X <= 1 || row.GetThemeFont("font").GetStringSize(row.Text, HorizontalAlignment.Left, -1, row.GetThemeFontSize("font_size")).X <= row.Size.X - layout.CaptionInset * 2;

    /// <summary>The original art pieces with the middle strip stretched to <paramref name="height"/>.</summary>
    public static void DrawArt(CanvasItem canvas, ClassicNpcOpeningLayout layout, int height)
    {
        foreach (var image in Plugin.Kit.Layout(layout.Art).Children.Where(n => n.IsImage && n.Texture != null))
            canvas.DrawTextureRectRegion(Plugin.Kit.Texture(image.Texture!), ClassicNpcOpeningLayout.Piece(image, height), new Rect2(image.SrcX, image.SrcY, image.SrcW, image.SrcH));
    }
}

/// <summary>Draws an opening window's art at the control's height.</summary>
public partial class ClassicNpcOpeningArt : Control
{
    public ClassicNpcOpeningLayout Layout { get; }

    public ClassicNpcOpeningArt(ClassicNpcOpeningLayout layout)
    {
        Layout = layout; Name = "classic_npc_opening_art"; MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.Nearest;
        Resized += QueueRedraw;
    }

    public override void _Draw() => ClassicNpcOpening.DrawArt(this, Layout, (int)Size.Y);
}

/// <summary>
/// Stands in for the shared Classic NPC dialogue while the client's NPC menu is an opening with an original
/// window (<see cref="NativeNpcOpening"/>): the client's greeting, then one yellow text row per client choice. Each
/// row presses the client's own choice button, so every service opens through the client's callbacks.
/// </summary>
public partial class ClassicNpcOpeningChoice : Control
{
    private readonly HudWindow _window;
    private readonly Control _dialogue;
    private readonly RichTextLabel _speech;
    private readonly VBoxContainer _menu;
    private readonly Label _greeting;
    private readonly List<ClassicNpcOpeningOption> _rows = new();
    private ulong _first;
    private Vector2 _viewport, _placed;

    /// <summary>The opening shown (<see cref="NativeNpcOpening.Inn"/>, <see cref="NativeNpcOpening.Smith"/>), null while the shared dialogue shows.</summary>
    public string? Kind { get; private set; }
    public ClassicNpcOpeningLayout? Layout { get; private set; }
    public bool Active => Layout != null;
    /// <summary>How far a long client greeting moved the rows down.</summary>
    public int Shift { get; private set; }
    public Label Greeting => _greeting;
    public IReadOnlyList<ClassicNpcOpeningOption> Rows => _rows;

    public static ClassicNpcOpeningLayout? For(string? kind) => kind switch
    {
        NativeNpcOpening.Inn => ClassicNpcOpeningLayout.Inn,
        NativeNpcOpening.Smith => ClassicNpcOpeningLayout.Smith,
        _ => null
    };

    public static ClassicNpcOpeningChoice Attach(HudWindow window, Control dialogue)
    {
        var view = new ClassicNpcOpeningChoice(window, dialogue);
        window.AddChild(view);
        return view;
    }

    private ClassicNpcOpeningChoice(HudWindow window, Control dialogue)
    {
        Name = "classic_npc_opening"; _window = window; _dialogue = dialogue; Visible = false;
        MouseFilter = MouseFilterEnum.Stop; TextureFilter = TextureFilterEnum.Nearest;
        _speech = CharacterDetailsSkin.Tree(window.Body).OfType<RichTextLabel>().First();
        _menu = window.Body.GetChildren().OfType<ScrollContainer>().ElementAt(1).GetChild<VBoxContainer>(0);
        _greeting = new Label { Name = "npc_opening_greeting" };
        AddChild(_greeting);
        Resized += QueueRedraw;
    }

    public override void _Process(double delta)
    {
        if (!_window.IsVisibleInTree()) return;
        string? kind = NativeNpcOpening.Kind(_menu);
        if (kind != Kind)
        {
            Kind = kind; Layout = For(kind); Visible = Layout != null; _dialogue.Visible = Layout == null; _first = 0;
            if (Layout == null) { CustomMinimumSize = Vector2.Zero; _window.ResetSize(); }
        }
        if (Layout == null) return;
        ulong first = _menu.GetChild(0).GetInstanceId();
        var viewport = GetViewportRect().Size;
        if (first == _first && viewport == _viewport)
        {
            // The 2.x quest menu may hand the window's size back after this view centred it in the same frame.
            if (_window.Size != _placed) { _placed = _window.Size; _window.Position = ((_viewport - _window.Size) / 2).Floor(); }
            return;
        }
        _first = first; _viewport = viewport;
        Rebuild(Layout);
    }

    private void Rebuild(ClassicNpcOpeningLayout layout)
    {
        foreach (var row in _rows) { RemoveChild(row); row.QueueFree(); }
        _rows.Clear();
        _greeting.Text = _speech.GetParsedText().Trim();
        Shift = ClassicNpcOpening.Greeting(_greeting, layout);
        var choices = _menu.GetChildren().OfType<Button>().ToArray();
        for (int index = 0; index < choices.Length; index++)
        {
            var row = new ClassicNpcOpeningOption(choices[index], layout) { Name = "npc_opening_choice_" + index };
            AddChild(row); ClassicNpcLayout.Place(row, layout.Choice(index, Shift)); _rows.Add(row);
        }
        Size = CustomMinimumSize = new Vector2(ClassicNpcOpeningLayout.Width, layout.Height(choices.Length, Shift));
        _window.ResetSize();
        _placed = _window.Size;
        _window.Position = ((_viewport - _window.Size) / 2).Floor();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Layout != null) ClassicNpcOpening.DrawArt(this, Layout, (int)Size.Y);
    }
}

/// <summary>One of the client's opening choices as an original yellow text row.</summary>
public partial class ClassicNpcOpeningOption : Button
{
    public Button Source { get; }
    private readonly ClassicNpcOpeningLayout _layout;
    private string? _lastText;

    public ClassicNpcOpeningOption(Button source, ClassicNpcOpeningLayout layout)
    {
        Source = source; _layout = layout;
        ClassicNpcOpening.Row(this, layout);
        Pressed += () => { if (IsInstanceValid(Source) && !Source.Disabled) Source.EmitSignal(BaseButton.SignalName.Pressed); };
        Resized += () => _lastText = null;
        Mirror();
    }

    public override void _Process(double delta) => Mirror();

    private void Mirror()
    {
        if (!IsInstanceValid(Source)) { Disabled = true; return; }
        Disabled = Source.Disabled;
        if (Source.Text == _lastText) return;
        _lastText = Source.Text; Text = ClassicNpcOpeningLayout.Caption(Source.Text);
        // The client repeats each caption as its tooltip; a row only needs it when the caption is cut.
        TooltipText = ClassicNpcOpening.Fits(this, _layout) ? "" : Text;
    }
}
