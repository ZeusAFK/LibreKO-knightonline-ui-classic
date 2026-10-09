using Godot;
using LibreKO.Plugins;
using KnightOnlineUiClassic.Layout;

namespace KnightOnlineUiClassic.Windows;

// Live server-owned confirmations in the plain Classic frame with the original fee-dialog actions. The box fits
// its message with the message-box rhythm: title band, 15 px, the wrapped message, 15 px, the 97 x 29 actions
// and the original 10 px bottom, so a short message leaves no empty band.
public partial class ClassicServiceNotice : Control
{
    public const float Width = 344, TransferWidth = 420, TitleTop = 10, TitleHeight = 15, Gap = 15, ActionHeight = 29, Bottom = 10, Side = 18;
    /// <summary>Top of the message: the title band plus the even gap.</summary>
    public const float MessageTop = TitleTop + TitleHeight + Gap;
    private readonly DialogRequest _request;
    private readonly Control _panel = new() { Size = new Vector2(Width, 196) };
    private readonly Label _message;
    private readonly List<Button> _actions = new();
    private readonly bool _transfer;
    private bool _positioned, _dragging;
    private Vector2 _dragOffset;
    public ClassicServiceNotice(DialogRequest request)
    {
        _request = request; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _transfer = request.Title == "Nation Transfer";
        if (_transfer) _panel.Size = new Vector2(TransferWidth, 244);
        var blocker = new ColorRect { Color = new Color(0, 0, 0, .35f), MouseFilter = MouseFilterEnum.Stop };
        blocker.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(blocker); AddChild(_panel);
        var frame = new ClassicFrame { Name = "service_notice_frame", Size = _panel.Size, BackgroundColor = Colors.Black, BackgroundAlpha = 1 };
        _panel.AddChild(frame); _panel.Resized += () => frame.Size = _panel.Size;
        var title = new Label { Text = request.Title, Position = new Vector2(Side, TitleTop), Size = new Vector2(_panel.Size.X - Side * 2, TitleHeight), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        _panel.AddChild(title); ClassicVendorSkin.Font(title); title.AddThemeColorOverride("font_color", new Color("e4c58b"));
        _message = new Label { Text = request.Message, Position = new Vector2(Side, MessageTop), Size = new Vector2(_panel.Size.X - Side * 2, 1),
            AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
        _panel.AddChild(_message); ClassicVendorSkin.Font(_message); _message.AddThemeColorOverride("font_color", new Color("e4d9bf"));
        _message.ClipContents = true;
        var art = Plugin.Kit.Layout("co_change_bill_us");
        foreach (bool accept in request.CancelText == null ? new[] { true } : new[] { true, false })
        {
            var button = new Button(); _panel.AddChild(button); _actions.Add(button);
            ClassicMerchantSkin.Button(button, art.Find(accept ? "btn_ok" : "btn_cancel")!, _panel,
                new Rect2(request.CancelText == null ? (_panel.Size.X - 97) / 2 : accept ? Side : _panel.Size.X - Side - 97, 0, 97, ActionHeight), accept ? request.ConfirmText : request.CancelText ?? "Cancel");
            ClassicVendorSkin.Font(button);
            foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" }) button.AddThemeColorOverride(state, new Color("efd9b4"));
            if (accept) button.Pressed += request.Confirm; else button.Pressed += request.Cancel;
        }
        request.MessageChanged += UpdateMessage;
        // Every service confirmation uses the Classic Arial Bold; the NPC body font is not part of this family.
        foreach (var control in CharacterDetailsSkin.Tree(_panel).OfType<Control>().Where(c => c is Label or Button))
        {
            control.AddThemeFontOverride("font", Plugin.Kit.Bold);
            control.AddThemeFontSizeOverride("font_size", 13);
        }
        Fit();
        if (_transfer || request.Title is "Rebirth" or "Clan Cape" or "Familiar Hatching" or "Familiar Transformation") _panel.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse || mouse.Position.Y > MessageTop - 4) return;
            _dragging = true; _dragOffset = mouse.Position; _positioned = true; _panel.AcceptEvent();
        };
    }
    /// <summary>Height of a box whose wrapped message is <paramref name="message"/> pixels high.</summary>
    public static float HeightFor(float message) => Mathf.Ceil(MessageTop + message + Gap + ActionHeight + Bottom);
    // The message keeps its wrapped height; the box and its actions follow it. Very long text is bounded by the
    // viewport and clipped inside the message band.
    private void Fit()
    {
        _message.Size = new Vector2(_panel.Size.X - Side * 2, 1);
        float text = MessageHeight(_message);
        float room = IsInsideTree() ? GetViewportRect().Size.Y - 40 : 4096;
        text = Math.Min(text, Math.Max(17, room - HeightFor(0)));
        _message.Size = new Vector2(_panel.Size.X - Side * 2, text);
        _panel.Size = new Vector2(_panel.Size.X, HeightFor(text));
        foreach (var button in _actions) button.Position = new Vector2(button.Position.X, _panel.Size.Y - Bottom - ActionHeight);
    }
    /// <summary>Wrapped height of an autowrapping label at its current width (Godot reports no minimum height for it).</summary>
    public static float MessageHeight(Label label)
    {
        int lines = Math.Max(1, label.GetLineCount());
        return Mathf.Ceil(lines * label.GetLineHeight() + (lines - 1) * label.GetThemeConstant("line_spacing"));
    }
    private void UpdateMessage(string text) { _message.Text = text; Fit(); }
    public override void _Ready() => Fit();
    public override void _ExitTree() => _request.MessageChanged -= UpdateMessage;
    public override void _Process(double delta)
    {
        var room = GetViewportRect().Size;
        if (!_positioned) _panel.Position = ((room - _panel.Size) / 2).Round();
        else _panel.Position = _panel.Position.Clamp(Vector2.Zero, (room - _panel.Size).Max(Vector2.Zero)).Round();
    }
    public override void _Input(InputEvent ev)
    {
        if (_dragging && ev is InputEventMouseMotion motion)
        {
            _panel.Position = (motion.Position - _dragOffset).Round(); GetViewport().SetInputAsHandled(); return;
        }
        if (_dragging && ev is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left })
        { _dragging = false; GetViewport().SetInputAsHandled(); return; }
        if (ev is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode is Key.Enter or Key.KpEnter) _request.Confirm();
        else if (key.Keycode == Key.Escape) _request.Cancel(); else return;
        GetViewport().SetInputAsHandled();
    }
}
