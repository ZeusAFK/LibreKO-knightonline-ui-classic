using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;

namespace KnightOnlineUiClassic.Windows;

public static class ClassicCommunicationSkin
{
    public static readonly string[] WindowIds = { "messenger" };
    public static void Extend(Control body) => body.Ready += () => Callable.From(() => Apply(body)).CallDeferred();
    public static ClassicCommunicationPanel? Apply(Control body)
    {
        Node? root = body; while (root != null && root is not HudWindow) root = root.GetParent();
        if (root is not HudWindow window || window.HasMeta("classic_communication") || window.GetMeta("classic_communication_controls", 0).AsInt32() != 1) return null;
        window.SetMeta("classic_communication", true);
        var panel = new ClassicCommunicationPanel(window, body); window.AddChild(panel); window.ResetSize(); return panel;
    }
}

public partial class ClassicCommunicationPanel : Control
{
    public HudWindow Window { get; }
    private readonly Control[] _native;
    private readonly Button _close;
    private readonly List<Rect2> _sections = new();
    private readonly List<(ScrollContainer Scroll, Rect2 Rect)> _scrolls = new();
    public ClassicCommunicationPanel(HudWindow window, Control body)
    {
        Window = window; Name = "classic_communication"; Size = CustomMinimumSize = ClassicCommunicationLayout.Size(window.Id);
        TextureFilter = TextureFilterEnum.Nearest;
        _native = CharacterDetailsSkin.Tree(body).OfType<Control>().ToArray();
        var header = window.Header!.GetChildren().OfType<HBoxContainer>().Single();
        var title = header.GetChildren().OfType<Label>().Last();
        _close = header.GetChildren().OfType<Button>().Single(b => b.TooltipText == "Close");
        foreach (var child in window.GetChildren().OfType<Control>()) child.Visible = false;
        window.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        AddChild(new ClassicFrame { Size = Size, BackgroundAlpha = 1, BackgroundColor = Colors.Black, ShowBehindParent = true });
        ClassicPartySkin.Drag(this, header, new Rect2(14, 8, Size.X - 64, 32));
        Place(title, ClassicFrame.Title(Size.X)); title.AddThemeColorOverride("font_color", ClassicDesign.FrameTitle);
        ClassicMerchantSkin.Button(_close, Plugin.Kit.Layout("co_questtalk_us").Find("btn_close")!, this, ClassicFrame.Close(Size.X), "");
        _close.SetMeta("communication_expected_rect", ClassicFrame.Close(Size.X));
        Messenger();
    }
    private T Find<T>(string name) where T : Control => _native.OfType<T>().Single(c => GodotObject.IsInstanceValid(c) && !c.IsQueuedForDeletion() && c.Name == name);
    private static void Font(Control control, bool bold = true, int size = 13)
    {
        control.AddThemeFontOverride("font", bold ? Plugin.Kit.Bold : Plugin.Kit.Regular);
        control.AddThemeFontSizeOverride("font_size", size); control.AddThemeConstantOverride("outline_size", 0);
    }
    private void Place(Control control, Rect2 rect)
    {
        ClassicVendorSkin.Move(control, this, rect); Font(control); control.SetMeta("communication_expected_rect", rect);
        if (control is Label label) LabelStyle(label, rect.Size);
    }
    private static void LabelStyle(Label label, Vector2 maximum)
    {
        label.ClipText = label.ClipContents = true; label.CustomMaximumSize = maximum;
        label.AutowrapMode = TextServer.AutowrapMode.Off; label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.AddThemeColorOverride("font_color", ClassicDesign.Text);
    }
    private void Caption(string text, Rect2 rect)
    {
        var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center }; AddChild(label); Place(label, rect);
        label.AddThemeColorOverride("font_color", ClassicDesign.Heading);
    }
    private static void Action(Button button, Control owner, Rect2 rect)
    {
        ClassicMerchantSkin.Button(button, Plugin.Kit.Layout("co_change_bill_us").Find("btn_ok")!, owner, rect, button.Text);
        Font(button); button.Icon = null; button.FocusMode = FocusModeEnum.None; button.SetMeta("communication_expected_rect", rect);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" }) button.AddThemeColorOverride(state, new Color("efd9b4"));
        button.AddThemeColorOverride("font_disabled_color", new Color("887e70"));
    }
    private void Input(LineEdit input, Rect2 rect)
    {
        Place(input, rect); input.TextDirection = TextDirection.Ltr; input.KeepEditingOnTextSubmit = true;
        foreach (var state in new[] { "normal", "focus", "read_only" }) input.AddThemeStyleboxOverride(state, ClassicDesign.InputBox());
        input.AddThemeColorOverride("font_color", ClassicDesign.Text); input.AddThemeColorOverride("font_placeholder_color", ClassicDesign.Muted);
        input.GuiInput += ev => { if (ev is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) { Close(); input.AcceptEvent(); } };
    }
    private void Scroll(ScrollContainer scroll, Rect2 rect, float inset = 0)
    {
        _sections.Add(rect.Grow(1)); rect = rect.GrowSide(Side.Left, -inset);
        Place(scroll, rect); scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled; _scrolls.Add((scroll, rect));
        ClassicDarkFrame.ScrollBar(scroll.GetVScrollBar(), 10);
    }
    private void Messenger()
    {
        Caption("Character", new Rect2(22, 64, 178, 20)); Caption("Status", new Rect2(216, 64, 80, 20));
        Scroll(Find<ScrollContainer>("messenger_scroll"), ClassicCommunicationLayout.BuddyList);
        Watch(Find<VBoxContainer>("messenger_list"), StyleRow);
        Caption("Quick Whisper", new Rect2(16, 350, 388, 20));
        Caption("To", new Rect2(16, 378, 44, 26)); Input(Find<LineEdit>("messenger_to"), new Rect2(68, 378, 336, 26));
        Input(Find<LineEdit>("messenger_message"), new Rect2(16, 416, 292, 26));
        Action(Find<Button>("messenger_send"), this, new Rect2(316, 416, 88, 26));
        Find<LineEdit>("messenger_to").TextSubmitted += _ => Find<LineEdit>("messenger_message").GrabFocus();
    }
    private static void Watch(VBoxContainer rows, Action<Control> style)
    {
        rows.CustomMinimumSize = new Vector2(1, 0); rows.AddThemeConstantOverride("separation", 0);
        foreach (var child in rows.GetChildren().OfType<Control>()) style(child);
        rows.ChildEnteredTree += child => Callable.From(() => { if (GodotObject.IsInstanceValid(child) && !child.IsQueuedForDeletion() && child is Control control) style(control); }).CallDeferred();
    }
    private static void StyleRow(Control child)
    {
        if (child is Label empty) { Font(empty, false); empty.AddThemeColorOverride("font_color", ClassicDesign.Muted); empty.CustomMinimumSize = new Vector2(1, 26); }
        if (child is not PanelContainer row || !row.HasMeta("communication_row") || row.HasMeta("classic_communication_row")) return;
        row.SetMeta("classic_communication_row", true); row.CustomMinimumSize = new Vector2(1, 30);
        bool selected = row.GetMeta("communication_selected", false).AsBool();
        var box = new StyleBoxFlat { BgColor = selected ? new Color("26251f") : Colors.Transparent, BorderColor = selected ? new Color("e7c773") : new Color("544a37") };
        box.BorderWidthBottom = 1; if (selected) box.SetBorderWidthAll(1); box.SetContentMarginAll(0); row.AddThemeStyleboxOverride("panel", box);
        var fields = CharacterDetailsSkin.Tree(row).OfType<Control>().Where(c => c.Name.ToString().StartsWith("communication_")).ToArray();
        foreach (var control in row.GetChildren().OfType<Control>()) control.Visible = false;
        var overlay = new Control { MouseFilter = MouseFilterEnum.Ignore }; row.AddChild(overlay);
        foreach (var field in fields)
        {
            var rect = field.Name.ToString() switch
            {
                "communication_name" => new Rect2(6, 5, 182, 20),
                "communication_state" => new Rect2(200, 5, 84, 20),
                _ => new Rect2(292, 3, 84, 24),
            };
            if (field is Button button) Action(button, overlay, rect);
            else
            {
                ClassicVendorSkin.Move(field, overlay, rect); Font(field); field.SetMeta("communication_expected_rect", rect);
                if (field is Label label)
                {
                    LabelStyle(label, rect.Size);
                    if (label.Name == "communication_state") label.AddThemeColorOverride("font_color", row.GetMeta("communication_online", false).AsBool() ? new Color("91c678") : ClassicDesign.Muted);
                    if (label.Name == "communication_name" && !row.GetMeta("communication_online").AsBool()) label.AddThemeColorOverride("font_color", ClassicDesign.Muted);
                }
            }
        }
    }
    private void Close() => _close.EmitSignal(BaseButton.SignalName.Pressed);
    public override void _UnhandledKeyInput(InputEvent ev)
    {
        if (!Window.IsVisibleInTree() || ev is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        Close(); GetViewport().SetInputAsHandled();
    }
    public override void _Process(double delta)
    {
        foreach (var (scroll, rect) in _scrolls) { scroll.CustomMinimumSize = Vector2.Zero; scroll.Size = rect.Size; }
    }
    public override void _Draw() { foreach (var section in _sections) DrawStyleBox(ClassicDesign.SectionBox(0), section); }
}
