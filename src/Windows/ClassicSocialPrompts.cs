using Godot;
using LibreKO.Plugins;
using KnightOnlineUiClassic.Layout;
using Message = KnightOnlineUiClassic.Layout.ClassicOverlayLayout.Message;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The Classic player menu for the client's player-menu seam: the player's name over the client's own rows
/// on the Classic menu panel (black, the gold line, the selected-row hover). A click outside or Escape closes it.
/// </summary>
public partial class ClassicPlayerMenu : Control
{
    private readonly PlayerMenuRequest _request = null!;
    private readonly PanelContainer _panel = null!;

    public ClassicPlayerMenu() { }

    public ClassicPlayerMenu(PlayerMenuRequest request)
    {
        _request = request;
        Name = "classic_player_menu";
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        GuiInput += ev => { if (ev is InputEventMouseButton { Pressed: true }) { _request.Close(); AcceptEvent(); } };
        _panel = new PanelContainer { Name = "player_menu_panel", MouseFilter = MouseFilterEnum.Stop };
        var box = ClassicDarkFrame.InputBox(); box.BgColor = new Color(0, 0, 0, .94f); box.SetContentMarginAll(ClassicSocialLayout.MenuPadding);
        _panel.AddThemeStyleboxOverride("panel", box);
        AddChild(_panel);
        var rows = new VBoxContainer { Name = "player_menu_rows" };
        rows.AddThemeConstantOverride("separation", 0);
        _panel.AddChild(rows);
        var title = new Label { Name = "player_menu_name", Text = request.Name, MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(ClassicSocialLayout.MenuWidth, ClassicSocialLayout.MenuRow) };
        title.VerticalAlignment = VerticalAlignment.Center; title.ClipText = true; title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        ClassicSocialStyle.Font(title, true, ClassicSocialLayout.HeadingSize);
        title.AddThemeColorOverride("font_color", ClassicDarkFrame.Heading);
        title.AddThemeStyleboxOverride("normal", new StyleBoxEmpty { ContentMarginLeft = ClassicSocialLayout.MenuInset });
        rows.AddChild(title);
        rows.AddChild(new HSeparator { Name = "player_menu_rule" });
        ((HSeparator)rows.GetChild(1)).AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = ClassicSocialStyle.Rule, Thickness = 1 });
        foreach (var row in request.Rows)
        {
            var button = new Button { Name = "player_menu_" + row.Key, Text = row.Caption, FocusMode = FocusModeEnum.None, Alignment = HorizontalAlignment.Left };
            button.CustomMinimumSize = new Vector2(ClassicSocialLayout.MenuWidth, ClassicSocialLayout.MenuRow);
            ClassicSocialStyle.Font(button, true, ClassicSocialLayout.FontSize);
            var normal = new StyleBoxEmpty { ContentMarginLeft = ClassicSocialLayout.MenuInset, ContentMarginRight = ClassicSocialLayout.MenuInset };
            var hover = new StyleBoxFlat { BgColor = ClassicDarkFrame.SelectedRow, ContentMarginLeft = ClassicSocialLayout.MenuInset, ContentMarginRight = ClassicSocialLayout.MenuInset };
            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            foreach (var state in new[] { "hover", "pressed", "hover_pressed" }) button.AddThemeStyleboxOverride(state, hover);
            button.AddThemeColorOverride("font_color", ClassicDesign.Text);
            foreach (var state in new[] { "font_hover_color", "font_pressed_color", "font_hover_pressed_color" }) button.AddThemeColorOverride(state, ClassicDarkFrame.Heading);
            var run = row.Run;
            button.Pressed += () => run();
            rows.AddChild(button);
        }
    }

    public override void _Ready() => Callable.From(Place).CallDeferred();

    /// <summary>Opens at the cursor and stays on screen, as the client's menu does.</summary>
    private void Place()
    {
        if (!IsInsideTree()) return;
        var room = GetViewportRect().Size;
        var size = _panel.GetCombinedMinimumSize();
        var at = _request.Position + new Vector2(2, 2);
        at.X = Mathf.Clamp(at.X, 0, Mathf.Max(0, room.X - size.X));
        at.Y = Mathf.Clamp(at.Y, 0, Mathf.Max(0, room.Y - size.Y));
        _panel.Position = at.Round(); _panel.Size = size;
    }

    public override void _UnhandledKeyInput(InputEvent ev)
    {
        if (ev is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        _request.Close();
        GetViewport().SetInputAsHandled();
    }
}

/// <summary>
/// The Classic text prompt for the client's text-input seam (invite by name and other one-line questions):
/// the original message box sized to its content, the message, the original edit field and the OK and
/// Cancel art. Enter submits, Escape cancels.
/// </summary>
public partial class ClassicTextPrompt : Control
{
    private readonly TextRequest _request = null!;
    private readonly ClassicMessagePanel _panel = null!;
    private readonly LineEdit _input = null!;
    private bool _done;

    public ClassicTextPrompt() { }

    public ClassicTextPrompt(TextRequest request)
    {
        _request = request;
        Name = "classic_text_prompt";
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var dim = new ColorRect { Color = new Color(0, 0, 0, .45f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);
        var message = new Label { Name = "prompt_message", Text = request.Message, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        ClassicWorldOverlays.Text(message, 15, ClassicWorldOverlays.MessageText);
        float text = Math.Max(ClassicSocialLayout.PromptLine, ClassicWorldOverlays.Wrapped(message, Message.Text.Size.X));
        var size = ClassicSocialLayout.PromptSize(text);
        _panel = new ClassicMessagePanel(size, centre: true) { Name = "prompt_panel" };
        AddChild(_panel);
        var title = new Label { Name = "prompt_title", Text = request.Title, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        ClassicWorldOverlays.Text(title, 13, ClassicWorldOverlays.MessageTitle);
        Place(title, Message.Title);
        Place(message, ClassicSocialLayout.PromptText(text));
        _input = new LineEdit
        {
            Name = "prompt_input", Text = request.Initial, PlaceholderText = request.Placeholder, Secret = request.Secret,
            MaxLength = request.MaxLength, Alignment = HorizontalAlignment.Center, TextDirection = TextDirection.Ltr,
        };
        // The original 23 px edit field (Edit_Common) with the message boxes' Arial Bold.
        var field = ClassicDarkFrame.InputBox(); field.ContentMarginTop = field.ContentMarginBottom = 0;
        _input.AddThemeStyleboxOverride("normal", field); _input.AddThemeStyleboxOverride("focus", field);
        _input.AddThemeFontOverride("font", Plugin.Kit.Bold); _input.AddThemeFontSizeOverride("font_size", 12);
        _input.AddThemeColorOverride("font_color", ClassicWorldOverlays.MessageText);
        _input.AddThemeColorOverride("font_placeholder_color", ClassicDesign.Muted);
        _input.AddThemeColorOverride("caret_color", ClassicWorldOverlays.MessageText);
        _input.TextSubmitted += _ => Submit();
        Place(_input, ClassicSocialLayout.PromptEdit(text));
        var ok = new Button { Name = "prompt_confirm", Text = request.ConfirmText };
        var cancel = new Button { Name = "prompt_cancel", Text = request.CancelText };
        Place(ok, Message.Left(size.Y)); ClassicWorldOverlays.MessageButton(ok);
        Place(cancel, Message.Right(size.Y)); ClassicWorldOverlays.MessageButton(cancel, cancel: true);
        ok.Pressed += Submit;
        cancel.Pressed += Cancel;
    }

    private void Place(Control control, Rect2 rect)
    {
        _panel.AddChild(control);
        control.Position = rect.Position; control.Size = rect.Size;
        // Sizes set before the prompt enters the tree are clamped to stale theme minimums; set them again in it.
        control.Ready += () => { control.Position = rect.Position; control.Size = rect.Size; };
        // A label may grow below its band by its font's line spacing; its glyphs stay top-aligned in the band.
        if (control is Label label) { label.VerticalAlignment = VerticalAlignment.Top; return; }
        control.SetMeta(ClassicWorldOverlays.ExpectedRect, rect);
    }

    public override void _Ready() => _input.CallDeferred(Control.MethodName.GrabFocus);

    private void Submit()
    {
        if (_done) return;
        _done = true;
        _request.Submit(_input.Text);
    }

    private void Cancel()
    {
        if (_done) return;
        _done = true;
        _request.Cancel();
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        GetViewport().SetInputAsHandled();
        Cancel();
    }
}
