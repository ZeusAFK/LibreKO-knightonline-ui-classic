using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;
using NativeApi = KnightOnlineUiClassic.Native.Native;
using Spec = KnightOnlineUiClassic.Layout.ClassicQuestHudLayout.Notice;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The quest notice the client opens at login (<c>quest_available</c>): one quest offer per page with the client's
/// answers, a pager ("Quests available (1 of N)") and Close all. It is a plain notice like the original 1.298 login
/// tip (<c>co_rookietip_us</c>): the thin message-box frame, the gold title and round close knob of the plain
/// window family, the quest name as the yellow notice heading between the original Level Guide page arrows, the
/// state caption in its quest ink, the text in white Arial Bold and one service button row per answer. The
/// client keeps rebuilding the page; every rebuilt row is styled as it arrives. It opens centred and keeps its top
/// edge while the pages change.
/// </summary>
public static class ClassicQuestNotice
{
    public const string WindowId = "quest_available", Skinned = "classic_quest_notice";
    public const string Previous = "Previous quest", Next = "Next quest";

    public static void Extend(Control body) => body.Ready += () => Callable.From(() => Apply(body)).CallDeferred();

    public static ClassicFrame? Apply(Control body)
    {
        if (NativeApi.WindowOf(body) is not { } window || window.HasMeta(Skinned)) return null;
        window.SetMeta(Skinned, true);
        // The window places itself (centred on open, top edge kept while paging); the client's default stays unused.
        window.SetMeta("content_open_anchor", true);
        window.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var frame = new ClassicFrame { Name = "classic_frame", BackgroundColor = Colors.Black, BackgroundAlpha = 1 };
        window.AddChild(frame);
        window.MoveChild(frame, 0);
        ClassicEventFamily.Header(window);
        if (window.Body.GetParent() is MarginContainer content)
        {
            content.AddThemeConstantOverride("margin_left", Spec.Margin);
            content.AddThemeConstantOverride("margin_top", Spec.ContentGap);
            content.AddThemeConstantOverride("margin_right", Spec.Margin);
            content.AddThemeConstantOverride("margin_bottom", Spec.Margin);
        }
        window.Body.CustomMinimumSize = new Vector2(Spec.BodyWidth, 0);
        window.Body.AddThemeConstantOverride("separation", Spec.Gap);
        foreach (var child in window.Body.GetChildren()) Style(child);
        // Rows arrive fully built; styling them as they enter keeps the first drawn frame Classic.
        window.Body.ChildEnteredTree += Style;
        window.AddChild(new ClassicQuestNoticePlacement(window) { Name = "classic_notice_placement" });
        window.ResetSize();
        return frame;
    }

    private static void Style(Node node)
    {
        switch (node)
        {
            case HBoxContainer pager: Pager(pager); break;
            case Label caption: Caption(caption); break;
            case RichTextLabel text: Text(text); break;
            case Button answer: Answer(answer); break;
        }
    }

    private static void Pager(HBoxContainer row)
    {
        row.AddThemeConstantOverride("separation", Spec.PagerGap);
        row.CustomMinimumSize = new Vector2(0, Spec.PagerHeight);
        foreach (var button in row.GetChildren().OfType<Button>())
            Arrow(button, button.TooltipText == Next, Spec.Arrow);
        foreach (var title in row.GetChildren().OfType<Label>())
        {
            ClassicEventPanel.Bold(title, ClassicWorldOverlays.MessageTitle, Spec.FontSize);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.AutowrapMode = TextServer.AutowrapMode.Off;
            title.ClipText = true; title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            title.CustomMinimumSize = new Vector2(1, 0);
            title.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        }
    }

    private static void Caption(Label label)
    {
        ClassicEventPanel.Bold(label, ClassicQuestStatus.Ink(LibreKO.Network.QuestViewState.Available), Spec.FontSize);
        label.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        // The preparer marks the caption with its quest state; the state decides the ink.
        if (label.HasMeta("quest_status")) ClassicQuestStatus.StyleCaption(label);
    }

    private static void Text(RichTextLabel text)
    {
        foreach (var font in new[] { "normal_font", "bold_font" }) text.AddThemeFontOverride(font, Plugin.Kit.Bold);
        text.AddThemeFontSizeOverride("normal_font_size", Spec.FontSize);
        text.AddThemeFontSizeOverride("bold_font_size", Spec.FontSize);
        text.AddThemeColorOverride("default_color", ClassicWorldOverlays.MessageText);
        text.AddThemeColorOverride("font_shadow_color", Colors.Black);
        text.AddThemeConstantOverride("shadow_offset_x", 1); text.AddThemeConstantOverride("shadow_offset_y", 1);
        text.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        // The client sizes the text for its wider window; it wraps at the notice width instead.
        text.CustomMinimumSize = new Vector2(1, 0);
    }

    private static void Answer(Button button)
    {
        ClassicEventFamily.Action(button);
        button.SetMeta("classic_quest_answer", true);
        // One whole-width row per answer; a long answer is trimmed, never widening the notice.
        button.CustomMinimumSize = new Vector2(1, Spec.AnswerHeight);
        button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        button.AddThemeFontSizeOverride("font_size", Spec.ButtonFont);
        button.ClipText = true; button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        // A trimmed caption keeps the button's caption inset instead of running into the end caps.
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
            if (button.GetThemeStylebox(state) is StyleBox box && box.ContentMarginLeft < Spec.CaptionInset)
            {
                var inset = (StyleBox)box.Duplicate();
                inset.ContentMarginLeft = inset.ContentMarginRight = Spec.CaptionInset;
                button.AddThemeStyleboxOverride(state, inset);
            }
        if (button.TooltipText.Length == 0) button.TooltipText = button.Text;
    }

    /// <summary>The original Level Guide page arrow (<c>btn_down</c> back, <c>btn_up</c> forward) with its states.</summary>
    internal static void Arrow(Button button, bool next, Vector2 size)
    {
        var art = Plugin.Kit.Layout("{nation}_levelguide_us").Find(next ? "btn_up" : "btn_down")!;
        string[] states = { "normal", "pressed", "hover", "disabled" };
        for (int tag = 0; tag < states.Length; tag++)
        {
            var image = art.Images.FirstOrDefault(n => n.Tag == tag) ?? art.Images.First();
            var box = new StyleBoxTexture { Texture = Plugin.Kit.Texture(image.Texture!), RegionRect = new Rect2(image.SrcX, image.SrcY, image.SrcW, image.SrcH) };
            box.SetContentMarginAll(0);
            // The original disabled art equals the normal one; a dimmed arrow shows the end of the pages.
            if (states[tag] == "disabled") box.ModulateColor = new Color(.45f, .45f, .45f);
            button.AddThemeStyleboxOverride(states[tag], box);
            if (states[tag] == "pressed") button.AddThemeStyleboxOverride("hover_pressed", box);
        }
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Text = ""; button.Icon = null; button.FocusMode = Control.FocusModeEnum.None;
        button.CustomMinimumSize = size;
        button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
    }
}

/// <summary>
/// Opens the notice centred once its rebuilt page has settled (hidden until then), keeps its top edge while the
/// pages change and keeps the whole window inside the screen.
/// </summary>
public partial class ClassicQuestNoticePlacement : Node
{
    private readonly HudWindow _window;
    private bool _opening = true;
    private int _stable;
    private Vector2 _lastSize;
    public int OpeningFrames { get; private set; }

    public ClassicQuestNoticePlacement() { _window = null!; }

    public ClassicQuestNoticePlacement(HudWindow window)
    {
        _window = window;
        window.VisibilityChanged += () =>
        {
            if (!window.Visible) return;
            _opening = true; _stable = 0; OpeningFrames = 0; _lastSize = default;
            window.Modulate = Colors.Transparent;
        };
        if (window.Visible) window.Modulate = Colors.Transparent;
    }

    public override void _Process(double delta)
    {
        if (_window == null || !GodotObject.IsInstanceValid(_window) || !_window.IsVisibleInTree()) return;
        var screen = _window.GetViewportRect().Size;
        if (_opening)
        {
            OpeningFrames++;
            _window.ResetSize();
            _stable = _window.Size == _lastSize ? _stable + 1 : 0;
            _lastSize = _window.Size;
            if (_stable < 1) return;
            _window.Position = ((screen - _window.Size) / 2).Floor();
            _opening = false;
            _window.Modulate = Colors.White;
            return;
        }
        float margin = ClassicQuestHudLayout.Notice.ScreenMargin;
        var kept = new Vector2(Mathf.Clamp(_window.Position.X, 0, Math.Max(0, screen.X - _window.Size.X)),
            Mathf.Clamp(_window.Position.Y, 0, Math.Max(0, screen.Y - _window.Size.Y - margin)));
        if (kept != _window.Position) _window.Position = kept;
    }
}
