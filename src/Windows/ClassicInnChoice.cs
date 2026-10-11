using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Native;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The Inn Hostess's opening window on the original co_inn_us art. While the client's NPC dialogue holds the
/// inn's choices it stands in for the shared Classic NPC dialogue: the client's greeting in the original white
/// text, then one yellow text row per client choice (storage, VIP storage, clan, seals, leave). Each row presses
/// the client's own choice button, so every service opens through the client's callbacks.
/// </summary>
public partial class ClassicInnChoice : Control
{
    private readonly HudWindow _window;
    private readonly Control _dialogue;
    private readonly RichTextLabel _speech;
    private readonly VBoxContainer _menu;
    private readonly Label _greeting;
    private readonly List<ClassicInnOption> _rows = new();
    private ulong _first;
    private Vector2 _viewport;

    public bool Active { get; private set; }
    public Label Greeting => _greeting;
    public IReadOnlyList<ClassicInnOption> Rows => _rows;

    public static ClassicInnChoice Attach(HudWindow window, Control dialogue)
    {
        var view = new ClassicInnChoice(window, dialogue);
        window.AddChild(view);
        return view;
    }

    private ClassicInnChoice(HudWindow window, Control dialogue)
    {
        Name = "classic_inn_choice"; _window = window; _dialogue = dialogue; Visible = false;
        MouseFilter = MouseFilterEnum.Stop; TextureFilter = TextureFilterEnum.Nearest;
        _speech = CharacterDetailsSkin.Tree(window.Body).OfType<RichTextLabel>().First();
        _menu = window.Body.GetChildren().OfType<ScrollContainer>().ElementAt(1).GetChild<VBoxContainer>(0);
        var title = Plugin.Kit.Layout(ClassicInnLayout.Art).Find("Text_Title")!;
        _greeting = new Label { Name = "inn_greeting", AutowrapMode = TextServer.AutowrapMode.WordSmart, ClipText = true,
            MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Top };
        Ink(_greeting, title);
        _greeting.AddThemeColorOverride("font_color", title.Color);
        AddChild(_greeting); ClassicNpcLayout.Place(_greeting, ClassicInnLayout.Greeting);
        Resized += QueueRedraw;
    }

    /// <summary>The original string's face and size with the shadow the layout strings carry.</summary>
    internal static void Ink(Control control, LayoutNode node)
    {
        control.AddThemeFontOverride("font", Plugin.Kit.FontFor(node));
        control.AddThemeFontSizeOverride("font_size", UiKit.FontSize(node));
        control.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .75f));
        control.AddThemeConstantOverride("shadow_offset_x", 1); control.AddThemeConstantOverride("shadow_offset_y", 1);
        control.AddThemeConstantOverride("outline_size", 0);
    }

    public override void _Process(double delta)
    {
        if (!_window.IsVisibleInTree()) return;
        bool inn = NativeInn.IsInnMenu(_menu);
        if (inn != Active)
        {
            Active = inn; Visible = inn; _dialogue.Visible = !inn; _first = 0;
            if (!inn) { CustomMinimumSize = Vector2.Zero; _window.ResetSize(); }
        }
        if (!inn) return;
        ulong first = _menu.GetChild(0).GetInstanceId();
        var viewport = GetViewportRect().Size;
        if (first == _first && viewport == _viewport) return;
        _first = first; _viewport = viewport;
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var row in _rows) { RemoveChild(row); row.QueueFree(); }
        _rows.Clear();
        _greeting.Text = _speech.GetParsedText().Trim();
        var ink = Plugin.Kit.Layout(ClassicInnLayout.Art).Find("Text_Trade")!;
        var choices = _menu.GetChildren().OfType<Button>().ToArray();
        for (int index = 0; index < choices.Length; index++)
        {
            var row = new ClassicInnOption(choices[index], ink) { Name = "inn_choice_" + index };
            AddChild(row); ClassicNpcLayout.Place(row, ClassicInnLayout.Choice(index)); _rows.Add(row);
        }
        Size = CustomMinimumSize = new Vector2(ClassicInnLayout.Width, ClassicInnLayout.Height(choices.Length));
        _window.ResetSize();
        _window.Position = ((_viewport - _window.Size) / 2).Floor();
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var image in Plugin.Kit.Layout(ClassicInnLayout.Art).Children.Where(n => n.IsImage && n.Texture != null))
            DrawTextureRectRegion(Plugin.Kit.Texture(image.Texture!), ClassicInnLayout.Piece(image, (int)Size.Y), new Rect2(image.SrcX, image.SrcY, image.SrcW, image.SrcH));
    }
}

/// <summary>One of the client's inn choices as an original yellow text row.</summary>
public partial class ClassicInnOption : Button
{
    public Button Source { get; }
    private string? _lastText;

    public ClassicInnOption(Button source, LayoutNode ink)
    {
        Source = source; FocusMode = FocusModeEnum.None; Alignment = HorizontalAlignment.Left;
        ClipText = true; TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
        {
            // The pressed caption sits one pixel lower, as a pushed original button does.
            bool down = state is "pressed" or "hover_pressed";
            AddThemeStyleboxOverride(state, new StyleBoxEmpty { ContentMarginLeft = ClassicInnLayout.CaptionInset, ContentMarginRight = ClassicInnLayout.CaptionInset,
                ContentMarginTop = down ? 2 : 1, ContentMarginBottom = down ? 0 : 1 });
        }
        ClassicInnChoice.Ink(this, ink);
        AddThemeColorOverride("font_color", ink.Color); AddThemeColorOverride("font_focus_color", ink.Color);
        AddThemeColorOverride("font_hover_color", Colors.White);
        AddThemeColorOverride("font_pressed_color", ClassicInnLayout.Pressed); AddThemeColorOverride("font_hover_pressed_color", ClassicInnLayout.Pressed);
        AddThemeColorOverride("font_disabled_color", ClassicInnLayout.Disabled);
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
        _lastText = Source.Text; Text = ClassicInnLayout.Caption(Source.Text);
        var font = GetThemeFont("font"); int size = GetThemeFontSize("font_size");
        bool fits = Size.X <= 1 || font.GetStringSize(Text, HorizontalAlignment.Left, -1, size).X <= Size.X - ClassicInnLayout.CaptionInset * 2;
        // The client repeats each caption as its tooltip; a row only needs it when the caption is cut.
        TooltipText = fits ? "" : Text;
    }
}
