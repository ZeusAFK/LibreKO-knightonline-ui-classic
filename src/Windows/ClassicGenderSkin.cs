using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;

namespace KnightOnlineUiClassic.Windows;

public static class ClassicGenderSkin
{
    public static void Extend(Control body) => body.Ready += () => Callable.From(() => Apply(body)).CallDeferred();
    public static ClassicGenderPanel? Apply(Control body)
    {
        Node? node = body; while (node != null && node is not HudWindow) node = node.GetParent();
        if (node is not HudWindow window || window.HasMeta("classic_gender")) return null;
        if (window.GetMeta("classic_appearance_controls", 0).AsInt32() != 1) return null;
        window.SetMeta("classic_gender", true);
        var panel = new ClassicGenderPanel(window, body); window.AddChild(panel); window.ResetSize(); return panel;
    }
}

public partial class ClassicGenderPanel : Control
{
    private readonly Button _close;
    private readonly ColorPickerButton _colour;
    private readonly Button _accept;
    private readonly bool _beauty;
    private readonly bool _transfer;
    private Button? _palette;
    private ColorRect? _swatch;
    private LookEditor? _editor;
    public ClassicGenderPanel(HudWindow window, Control body)
    {
        _beauty = window.Id == "changehair";
        _transfer = window.Id == "nationtransfer";
        Name = _transfer ? "classic_nationtransfer" : _beauty ? "classic_beauty" : "classic_gender";
        Size = CustomMinimumSize = _transfer ? ClassicNationTransferLayout.Size : ClassicGenderLayout.Size; TextureFilter = TextureFilterEnum.Nearest;
        ShownOnly.Gate(this, Sync);
        var native = CharacterDetailsSkin.Tree(body).OfType<Control>().ToArray();
        T Find<T>(string name) where T : Control => native.OfType<T>().Single(c => c.Name == name);
        var bar = window.Header!.GetChildren().OfType<HBoxContainer>().Single();
        var title = bar.GetChildren().OfType<Label>().Last();
        _close = bar.GetChildren().OfType<Button>().Single(b => b.TooltipText == "Close");
        foreach (var child in window.GetChildren().OfType<Control>()) child.Visible = false;
        window.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        AddChild(new ClassicFrame { Size = Size, BackgroundAlpha = 1, BackgroundColor = Colors.Black });
        ClassicPartySkin.Drag(this, bar, new Rect2(14, 8, Size.X - 62, 32));
        Place(title, ClassicFrame.Title(Size.X)); title.AddThemeColorOverride("font_color", ClassicDesign.FrameTitle);
        ClassicMerchantSkin.Button(_close, Plugin.Kit.Layout("co_questtalk_us").Find("btn_close")!, this, ClassicFrame.Close(Size.X), "");
        Place(Find<LookPreview>("look_preview"), _transfer ? ClassicNationTransferLayout.Preview : ClassicGenderLayout.Preview);
        if (_transfer)
        {
            Place(Find<Label>("transfer_header"), ClassicNationTransferLayout.Header);
            Place(Find<Label>("transfer_characters_heading"), ClassicNationTransferLayout.CharactersHeading);
            Find<Label>("transfer_characters_heading").AddThemeColorOverride("font_color", ClassicDesign.Heading);
            var scroll = Find<ScrollContainer>("transfer_scroll"); Place(scroll, ClassicNationTransferLayout.Characters);
            scroll.CustomMinimumSize = ClassicNationTransferLayout.Characters.Size;
            ClassicDarkFrame.ScrollBar(scroll.GetVScrollBar());
            var candidates = Find<VBoxContainer>("transfer_characters"); candidates.ChildEnteredTree += StyleCandidate;
            foreach (var child in candidates.GetChildren()) StyleCandidate(child);
        }
        if (_beauty)
        {
            Place(Find<Label>("look_name"), ClassicBeautyLayout.Name);
            Place(Find<Label>("look_identity"), ClassicBeautyLayout.Identity);
            Find<Label>("look_name").AddThemeColorOverride("font_color", ClassicDesign.Heading);
        }
        var editor = Find<LookEditor>("look_editor"); Place(editor, _transfer ? ClassicNationTransferLayout.Editor : _beauty ? ClassicBeautyLayout.Editor : ClassicGenderLayout.Editor);
        editor.AddThemeConstantOverride("separation", 8);
        // The Beauty Shop editor shows no race list (since ff78eef it holds a hidden one).
        if (native.OfType<VBoxContainer>().SingleOrDefault(c => c.Name == "look_races") is { } races)
        {
            races.AddThemeConstantOverride("separation", 4);
            races.ChildEnteredTree += StyleRace;
            foreach (var child in races.GetChildren()) StyleRace(child);
        }
        foreach (var label in CharacterDetailsSkin.Tree(editor).OfType<Label>())
        {
            Font(label); label.VerticalAlignment = VerticalAlignment.Center;
            if (label.Name.ToString().EndsWith("_heading")) { label.AddThemeColorOverride("font_color", ClassicDesign.Heading); label.CustomMinimumSize = new Vector2(0, 22); }
        }
        foreach (var name in new[] { "face", "hair" })
        {
            var row = Find<HBoxContainer>("look_" + name + "_row"); row.CustomMinimumSize = new Vector2(0, 26);
            row.AddThemeConstantOverride("separation", 6);
            var caption = row.GetChildren().OfType<Label>().First(); caption.CustomMinimumSize = new Vector2(170, 26);
            foreach (var side in new[] { "previous", "next" }) Arrow(Find<Button>("look_" + name + "_" + side), side == "previous");
            var value = Find<Label>("look_" + name + "_value"); value.CustomMinimumSize = new Vector2(64, 26);
        }
        _colour = Find<ColorPickerButton>("look_colour");
        _colour.CustomMinimumSize = new Vector2(80, 26);
        // Enter belongs to the service, as on the other look buttons: a focused colour button would also open its picker.
        _colour.FocusMode = FocusModeEnum.None;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) _colour.AddThemeStyleboxOverride(state, ClassicDesign.InputBox());
        ClassicHairPaletteSkin.StylePicker(_colour.GetPicker(), false);
        _colour.GetPopup().AddThemeStyleboxOverride("panel", ClassicDesign.InputBox());
        var colourCaption = Find<HBoxContainer>("look_colour_row").GetChildren().OfType<Label>().Single(); colourCaption.CustomMinimumSize = new Vector2(170, 26);
        if (_beauty)
        {
            // The Beauty Shop's colour opens the client's hair palette; the row shows the edited colour as a swatch.
            _palette = Find<Button>("look_palette"); _palette.CustomMinimumSize = new Vector2(80, 26); _palette.FocusMode = FocusModeEnum.None;
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" }) _palette.AddThemeStyleboxOverride(state, ClassicDesign.InputBox());
            _swatch = new ColorRect { Name = "look_palette_swatch", MouseFilter = MouseFilterEnum.Ignore };
            _palette.AddChild(_swatch); _swatch.SetAnchorsPreset(LayoutPreset.FullRect);
            _swatch.OffsetLeft = _swatch.OffsetTop = 4; _swatch.OffsetRight = _swatch.OffsetBottom = -4;
            _editor = editor;
            var zoom = Find<Button>("look_zoom"); ClassicDesign.StyleButton(zoom); Place(zoom, ClassicBeautyLayout.Zoom);
            ClassicPartySkin.KarusButtonContrast(zoom); zoom.TooltipText = "Zoom on the face";
        }
        var statusRect = _transfer ? ClassicNationTransferLayout.Status : ClassicGenderLayout.Status;
        Place(Find<Label>("look_status"), statusRect);
        var status = Find<Label>("look_status"); status.VerticalAlignment = VerticalAlignment.Top;
        status.CustomMaximumSize = statusRect.Size; status.ClipContents = true;
        foreach (var entry in new[] { ("look_accept", _transfer ? ClassicNationTransferLayout.Accept : ClassicGenderLayout.Accept, "btn_ok"), ("look_cancel", _transfer ? ClassicNationTransferLayout.Cancel : ClassicGenderLayout.Cancel, "btn_cancel") })
        {
            var button = Find<Button>(entry.Item1);
            ClassicMerchantSkin.Button(button, Plugin.Kit.Layout("co_change_bill_us").Find(entry.Item3)!, this, entry.Item2);
            Font(button); ClassicServiceAction.Disabled(button); button.SetMeta("look_expected_rect", entry.Item2);
        }
        foreach (var entry in new[] { ("look_turn_left", _transfer ? ClassicNationTransferLayout.TurnLeft : _beauty ? ClassicBeautyLayout.TurnLeft : ClassicGenderLayout.TurnLeft, true), ("look_turn_right", _transfer ? ClassicNationTransferLayout.TurnRight : _beauty ? ClassicBeautyLayout.TurnRight : ClassicGenderLayout.TurnRight, false) })
        {
            var button = Find<Button>(entry.Item1); Place(button, entry.Item2); Arrow(button, entry.Item3);
            button.TooltipText = entry.Item3 ? "Turn left" : "Turn right";
        }
        _accept = Find<Button>("look_accept");
        SetProcessUnhandledKeyInput(true);
    }
    private static void Font(Control control)
    {
        control.AddThemeFontOverride("font", Plugin.Kit.Bold); control.AddThemeFontSizeOverride("font_size", 13);
        control.AddThemeConstantOverride("outline_size", 0); control.AddThemeColorOverride("font_color", ClassicDesign.Text);
    }
    private void Place(Control control, Rect2 rect)
    {
        ClassicVendorSkin.Move(control, this, rect); Font(control); control.SetMeta("look_expected_rect", rect);
        if (control is Label label) label.MouseFilter = MouseFilterEnum.Ignore;
    }
    private static void Arrow(Button button, bool previous)
    {
        ClassicVendorSkin.Button(button, Plugin.Kit.Layout("co_charactercreate_us").Find(previous ? "btn_face_left" : "btn_face_right")!);
        button.Text = ""; button.CustomMinimumSize = new Vector2(19, 19); button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        button.FocusMode = FocusModeEnum.None; button.TooltipText = previous ? "Previous" : "Next";
        if (button.GetThemeStylebox("disabled") is StyleBoxTexture texture)
        {
            var dim = (StyleBoxTexture)texture.Duplicate(); dim.ModulateColor = new Color(1, 1, 1, .45f);
            button.AddThemeStyleboxOverride("disabled", dim);
        }
    }
    private static void StyleRace(Node node)
    {
        if (node is not Button button) return;
        ClassicDesign.StyleButton(button, true); button.CustomMinimumSize = new Vector2(0, 26);
        ClassicPartySkin.KarusButtonContrast(button);
        foreach (var state in new[] { "pressed", "hover_pressed" })
            if (button.GetThemeStylebox(state) is StyleBoxTexture texture) texture.ModulateColor = new Color(1, .88f, .62f);
        // The warm tint alone turns the blue Human art olive; a bright caption marks it as the selection.
        if (!ClassicDesign.Karus)
            foreach (var state in new[] { "font_pressed_color", "font_hover_pressed_color" }) button.AddThemeColorOverride(state, new Color("fff5c5"));
    }
    private static void StyleCandidate(Node node)
    {
        if (node is not Button button) return;
        StyleRace(button);
        button.CustomMinimumSize = new Vector2(0, 52);
        button.ClipText = true;
        button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
    }
    public override void _Process(double delta) => Sync();
    private void Sync()
    {
        if (_swatch == null || _palette == null || _editor == null) return;
        _swatch.Color = _editor.HairColour;
        _swatch.Modulate = _palette.Disabled ? new Color(.45f, .45f, .45f) : Colors.White;
    }
    /// <summary>The hair palette window the Beauty Shop opened, while it is shown.</summary>
    private HudWindow? OpenPalette() => _beauty && GetParent() is HudWindow window
        ? window.GetParent()?.GetChildren().OfType<HudWindow>().FirstOrDefault(w => w.Id == "hairpalette" && w.Visible) : null;
    public override void _UnhandledKeyInput(InputEvent ev)
    {
        if (!IsVisibleInTree() || ev is not InputEventKey { Pressed: true, Echo: false } key) return;
        // An open hair palette answers Enter and Escape itself.
        if (OpenPalette() != null) return;
        if (_beauty && key.Keycode is Key.Enter or Key.KpEnter && !_colour.GetPopup().Visible)
        {
            GetViewport().SetInputAsHandled();
            if (!_accept.Disabled) _accept.EmitSignal(BaseButton.SignalName.Pressed);
            return;
        }
        if (key.Keycode != Key.Escape) return;
        GetViewport().SetInputAsHandled();
        if (_colour.GetPopup().Visible) _colour.GetPopup().Hide();
        else _close.EmitSignal(BaseButton.SignalName.Pressed);
    }
}
