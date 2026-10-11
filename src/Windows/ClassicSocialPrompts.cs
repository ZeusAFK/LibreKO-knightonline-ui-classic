using Godot;
using LibreKO.Plugins;
using KnightOnlineUiClassic.Layout;
using Message = KnightOnlineUiClassic.Layout.ClassicOverlayLayout.Message;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The Classic player menu for the client's player-menu seam: the shared <see cref="ClassicContextMenu"/> with the
/// player's name over the client's own rows. The client places it on its own layer and closes it.
/// </summary>
public partial class ClassicPlayerMenu : ClassicContextMenu
{
    public ClassicPlayerMenu() { }

    public ClassicPlayerMenu(PlayerMenuRequest request)
        : base("player_menu", request.Name, null, request.Position,
            request.Rows.Select(r => new ClassicMenuRow(r.Key, r.Caption, r.Run)).ToList(), request.Close) { }
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
