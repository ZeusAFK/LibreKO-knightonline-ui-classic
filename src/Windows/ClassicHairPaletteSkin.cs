using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>The Beauty Shop's hair palette in the plain Classic service frame, beside the shop.</summary>
public static class ClassicHairPaletteSkin
{
    public const string WindowId = "hairpalette";
    public static void Extend(Control body) => body.Ready += () => Callable.From(() => Apply(body)).CallDeferred();
    public static ClassicHairPalettePanel? Apply(Control body)
    {
        Node? node = body; while (node != null && node is not HudWindow) node = node.GetParent();
        if (node is not HudWindow window || window.HasMeta("classic_palette")) return null;
        if (window.GetMeta("classic_palette_controls", 0).AsInt32() != 1) return null;
        window.SetMeta("classic_palette", true);
        var panel = new ClassicHairPalettePanel(window, body); window.AddChild(panel); window.ResetSize(); return panel;
    }

    /// <summary>
    /// The Classic colour field: the picker's own rectangle and hue strip with a hex row, in the service font
    /// and the shared input and button art (the gender change popup uses the same treatment).
    /// </summary>
    public static void StylePicker(ColorPicker picker, bool hex)
    {
        picker.ColorModesVisible = false; picker.SlidersVisible = false; picker.SamplerVisible = false; picker.PresetsVisible = false;
        if (hex) picker.HexVisible = true;
        picker.AddThemeFontOverride("font", Plugin.Kit.Bold); picker.AddThemeFontSizeOverride("font_size", 13);
        foreach (var control in PickerTree(picker).OfType<Control>())
        {
            if (control is LineEdit input)
            {
                ClassicVendorSkin.Input(input);
                input.AddThemeFontOverride("font", Plugin.Kit.Bold); input.AddThemeFontSizeOverride("font_size", 13);
                input.AddThemeColorOverride("font_color", ClassicDesign.Text);
            }
            else if (control is Label label)
            {
                label.AddThemeFontOverride("font", Plugin.Kit.Bold); label.AddThemeFontSizeOverride("font_size", 13);
                label.AddThemeConstantOverride("outline_size", 0); label.AddThemeColorOverride("font_color", ClassicDesign.Text);
            }
            else if (control is Button button)
            {
                ClassicDesign.StyleButton(button);
                if (ClassicDesign.Karus)
                    foreach (var state in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color", "icon_focus_color", "icon_disabled_color" })
                        button.AddThemeColorOverride(state, new Color("17130f"));
            }
        }
    }

    public static IEnumerable<Node> PickerTree(Node node)
    {
        yield return node;
        foreach (var child in node.GetChildren(true)) foreach (var item in PickerTree(child)) yield return item;
    }
}

public partial class ClassicHairPalettePanel : Control
{
    private const float Gap = 8;
    private readonly HudWindow _window;
    private readonly Button _accept, _cancel;
    // Frames left to keep the palette beside the shop after it opened (the window settles its own place once).
    private int _placing;
    public ClassicHairPalettePanel(HudWindow window, Control body)
    {
        _window = window; Name = "classic_hairpalette";
        Size = CustomMinimumSize = ClassicHairPaletteLayout.Size; TextureFilter = TextureFilterEnum.Nearest;
        var native = CharacterDetailsSkin.Tree(body).OfType<Control>().ToArray();
        T Find<T>(string name) where T : Control => native.OfType<T>().Single(c => c.Name == name);
        var bar = window.Header!.GetChildren().OfType<HBoxContainer>().Single();
        var title = bar.GetChildren().OfType<Label>().Last();
        var close = bar.GetChildren().OfType<Button>().Single(b => b.TooltipText == "Close");
        foreach (var child in window.GetChildren().OfType<Control>()) child.Visible = false;
        window.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        AddChild(new ClassicFrame { Size = Size, BackgroundAlpha = 1, BackgroundColor = Colors.Black });
        ClassicPartySkin.Drag(this, bar, new Rect2(14, 8, Size.X - 62, 32));
        Place(title, ClassicFrame.Title(Size.X)); title.AddThemeColorOverride("font_color", ClassicDesign.FrameTitle);
        ClassicMerchantSkin.Button(close, Plugin.Kit.Layout("co_questtalk_us").Find("btn_close")!, this, ClassicFrame.Close(Size.X), "");
        var prompt = Find<Label>("palette_prompt"); Place(prompt, ClassicHairPaletteLayout.Prompt);
        prompt.AddThemeColorOverride("font_color", ClassicDesign.Heading);
        var picker = Find<ColorPicker>("palette_picker");
        ClassicHairPaletteSkin.StylePicker(picker, true);
        Place(picker, ClassicHairPaletteLayout.Picker);
        _accept = Find<Button>("palette_accept"); _cancel = Find<Button>("palette_cancel");
        foreach (var (button, rect, art) in new[] { (_accept, ClassicHairPaletteLayout.Accept, "btn_ok"), (_cancel, ClassicHairPaletteLayout.Cancel, "btn_cancel") })
        {
            ClassicMerchantSkin.Button(button, Plugin.Kit.Layout("co_change_bill_us").Find(art)!, this, rect);
            Font(button); ClassicServiceAction.Disabled(button); button.SetMeta("look_expected_rect", rect);
        }
        _placing = window.Visible ? 2 : 0;
        window.VisibilityChanged += () => _placing = window.Visible ? 2 : 0;
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
    /// <summary>
    /// The palette opens beside the shop, top-aligned with it: on the right as the client intends, or on the
    /// left when the right side has no room. Once the window's own placement has settled it would otherwise
    /// return to its default place over the shop.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_placing <= 0 || !_window.IsVisibleInTree()) return;
        _placing--;
        var shop = _window.GetParent()?.GetChildren().OfType<HudWindow>().FirstOrDefault(w => w.Id == "changehair" && w.Visible);
        if (shop == null) return;
        var screen = GetViewportRect();
        float right = shop.Position.X + shop.Size.X + Gap, left = shop.Position.X - Gap - _window.Size.X;
        float x = right + _window.Size.X <= screen.End.X ? right : left >= screen.Position.X ? left : _window.Position.X;
        float y = Mathf.Clamp(shop.Position.Y, screen.Position.Y, Mathf.Max(screen.Position.Y, screen.End.Y - _window.Size.Y));
        _window.Position = new Vector2(x, y);
    }
    public override void _UnhandledKeyInput(InputEvent ev)
    {
        if (!IsVisibleInTree() || ev is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode is Key.Enter or Key.KpEnter) { GetViewport().SetInputAsHandled(); if (!_accept.Disabled) _accept.EmitSignal(BaseButton.SignalName.Pressed); }
        else if (key.Keycode == Key.Escape) { GetViewport().SetInputAsHandled(); _cancel.EmitSignal(BaseButton.SignalName.Pressed); }
    }
}
