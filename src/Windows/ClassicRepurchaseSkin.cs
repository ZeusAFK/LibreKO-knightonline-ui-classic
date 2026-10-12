using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Native;
using Spec = KnightOnlineUiClassic.Layout.ClassicRepurchaseLayout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// Classic skin of the client's buy-back window ("Item repurchase", Kaishan). It has no 1.298 art of its own, so it is
/// composed like the other plain-frame item windows: the message-box frame, a gold-lined list inset with the column
/// captions and ten whole rows, the service button art for Buy and Refresh and the dark-frame scroll bar. Item names
/// keep the client's grade colours; a price above the wallet and a passed deletion time turn red. The client's own
/// labels, buttons and scroll bar are placed on <see cref="ClassicRepurchaseLayout"/>; their callbacks stay native.
/// </summary>
public static class ClassicRepurchaseSkin
{
    public const string ExpectedRect = "repurchase_expected_rect";

    public static void Extend(Control body) => body.Ready += () => Callable.From(() => Apply(body)).CallDeferred();

    public static ClassicRepurchasePanel? Apply(Control body)
    {
        Node? owner = body;
        while (owner != null && owner is not HudWindow) owner = owner.GetParent();
        if (owner is not HudWindow window || window.HasMeta("classic_repurchase") || !window.HasMeta(NativeRepurchase.PreparedMeta)) return null;
        window.SetMeta("classic_repurchase", true);
        var panel = new ClassicRepurchasePanel(window, body); window.AddChild(panel); window.ResetSize();
        return panel;
    }
}

public partial class ClassicRepurchasePanel : Control
{
    private const string ExpectedRect = ClassicRepurchaseSkin.ExpectedRect;
    private static readonly Color Warning = new("ff6a5a"), RowLine = new(ClassicDarkFrame.Line, .4f);
    public HudWindow Window { get; }
    private readonly Control[] _native;
    private readonly Label[] _prices = new Label[Spec.Rows], _times = new Label[Spec.Rows], _names = new Label[Spec.Rows];
    private readonly ColorRect[] _rules = new ColorRect[Spec.Rows];
    private VScrollBar _scroll = null!;
    private float _grabber = -1;

    public ClassicRepurchasePanel(HudWindow window, Control body)
    {
        Window = window; Name = "classic_repurchase"; TextureFilter = TextureFilterEnum.Nearest;
        Size = CustomMinimumSize = Spec.Size;
        _native = CharacterDetailsSkin.Tree(body).OfType<Control>().ToArray();
        var grip = window.Header!.GetChildren().OfType<HBoxContainer>().First();
        var title = grip.GetChildren().OfType<Label>().Last();
        var close = grip.GetChildren().OfType<Button>().Single(b => b.TooltipText == "Close");
        foreach (var child in window.GetChildren().OfType<Control>()) child.Visible = false;
        window.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        AddChild(new ClassicFrame { Name = "repurchase_frame", Size = Size, BackgroundAlpha = 1, BackgroundColor = Colors.Black });
        var list = new Panel { Name = "repurchase_list", Position = Spec.List.Position, Size = Spec.List.Size, MouseFilter = MouseFilterEnum.Ignore };
        list.AddThemeStyleboxOverride("panel", ClassicDarkFrame.SectionBox()); AddChild(list);
        for (int row = 0; row < Spec.Rows; row++)
        {
            var rule = Spec.Rule(row);
            AddChild(_rules[row] = new ColorRect { Name = "repurchase_rule_" + row, Position = rule.Position, Size = rule.Size, MouseFilter = MouseFilterEnum.Ignore,
                Color = row == 0 ? new Color(ClassicDarkFrame.Line, .9f) : RowLine });
        }
        ClassicPartySkin.Drag(this, grip, new Rect2(16, 8, Size.X - 52, 30));
        Place(title, ClassicFrame.Title(Size.X), 12);
        title.HorizontalAlignment = HorizontalAlignment.Left; title.AddThemeColorOverride("font_color", ClassicDesign.FrameTitle);
        ClassicMerchantSkin.Button(close, Plugin.Kit.Layout("co_questtalk_us").Find("btn_close")!, this, ClassicFrame.Close(Size.X), "");
        close.SetMeta(ExpectedRect, ClassicFrame.Close(Size.X));

        for (int column = 0; column < 4; column++)
            if (Maybe<Label>("rb_head_" + column) is { } heading)
            {
                Place(heading, Spec.Heading(column), 11);
                heading.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                heading.AddThemeColorOverride("font_color", ClassicDarkFrame.Caption);
                heading.HorizontalAlignment = Align(column);
                heading.TooltipText = heading.Text; heading.MouseFilter = MouseFilterEnum.Pass;
            }
        for (int row = 0; row < Spec.Rows; row++)
        {
            _names[row] = Find<Label>("rb_name_" + row); _prices[row] = Find<Label>("rb_price_" + row); _times[row] = Find<Label>("rb_time_" + row);
            Place(_names[row], Spec.Cell(0, row), 12);
            _names[row].TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            Place(_prices[row], Spec.Cell(1, row), 12); _prices[row].HorizontalAlignment = Align(1);
            Place(_times[row], Spec.Cell(2, row), 11); _times[row].HorizontalAlignment = Align(2);
            Button(Find<Button>("rb_buy_" + row), Spec.Buy(row), "Buy");
        }
        var empty = Find<Label>("rb_empty"); Place(empty, Spec.Empty, 12);
        empty.HorizontalAlignment = HorizontalAlignment.Center; empty.VerticalAlignment = VerticalAlignment.Center;
        empty.AddThemeColorOverride("font_color", ClassicDesign.Muted);
        var scroll = _scroll = Find<VScrollBar>("rb_scroll");
        bool scrolls = scroll.Visible;
        ClassicVendorSkin.Move(scroll, this, Spec.Scroll); scroll.Visible = scrolls; scroll.SetMeta(ExpectedRect, Spec.Scroll);
        ClassicDarkFrame.ScrollBar(scroll);
        if (Maybe<Button>("rb_refresh") is { } refresh) { Button(refresh, Spec.Refresh, "Refresh"); refresh.TooltipText = "Refresh"; }
        Sync();
    }

    private static HorizontalAlignment Align(int column) => column switch
    {
        1 => HorizontalAlignment.Right,
        2 or 3 => HorizontalAlignment.Center,
        _ => HorizontalAlignment.Left,
    };

    private T Find<T>(string name) where T : Control => _native.OfType<T>().Single(c => GodotObject.IsInstanceValid(c) && c.Name == name);
    private T? Maybe<T>(string name) where T : Control => _native.OfType<T>().FirstOrDefault(c => GodotObject.IsInstanceValid(c) && c.Name == name);

    private void Place(Control control, Rect2 rect, int size)
    {
        bool visible = control.Visible;
        ClassicVendorSkin.Move(control, this, rect);
        control.Visible = visible;
        control.AddThemeFontOverride("font", Plugin.Kit.Bold); control.AddThemeFontSizeOverride("font_size", size);
        control.AddThemeConstantOverride("outline_size", 0);
        control.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        if (control is Label label) { label.VerticalAlignment = VerticalAlignment.Center; label.ClipText = true; }
        control.SetMeta(ExpectedRect, rect);
    }

    /// <summary>The service windows' button art with light ink on the dark plain frame, for both nations.</summary>
    private void Button(Button button, Rect2 rect, string text)
    {
        bool visible = button.Visible;
        ClassicMerchantSkin.Button(button, Plugin.Kit.Layout("co_change_bill_us").Find("btn_ok")!, this, rect, text);
        button.Visible = visible;
        button.AddThemeFontOverride("font", Plugin.Kit.Bold); button.AddThemeFontSizeOverride("font_size", 12);
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" }) button.AddThemeColorOverride(state, ClassicDarkFrame.Value);
        button.AddThemeColorOverride("font_disabled_color", new Color("918576"));
        button.FocusMode = FocusModeEnum.None;
        button.SetMeta(ExpectedRect, rect);
    }

    public override void _Process(double delta)
    {
        if (IsVisibleInTree()) Sync();
    }

    /// <summary>
    /// The client re-renders the rows (scrolling, refresh) with its own inks; the Classic inks follow the row states
    /// the preparer mirrors: item names keep their grade colours, prices and times read in the frame's value gold or red.
    /// </summary>
    private void Sync()
    {
        // The client scrolls by one row without a page, so its grabber would be a sliver; the grabber art gets the
        // height of the shown share of the list (ten rows of all), and the client's values stay untouched.
        int count = Plugin.Active ? Plugin.Kit.Game.Repurchase.Rows.Count : 0;
        float grabber = count > Spec.Rows ? Mathf.Round(Spec.Scroll.Size.Y * Spec.Rows / count) : Spec.Scroll.Size.Y;
        if (Math.Abs(_grabber - grabber) > .5f)
        {
            _grabber = grabber;
            foreach (var state in new[] { "grabber", "grabber_highlight", "grabber_pressed" })
                if (_scroll.GetThemeStylebox(state) is StyleBoxTexture box) { box.ContentMarginTop = grabber / 2; box.ContentMarginBottom = grabber / 2; }
        }
        for (int row = 0; row < Spec.Rows; row++)
        {
            // Rules separate the shown rows only, so an empty or short list has no empty grid.
            if (row > 0 && _rules[row].Visible != _names[row - 1].Visible) _rules[row].Visible = _names[row - 1].Visible;
            Ink(_prices[row], _prices[row].GetMeta(NativeRepurchase.TooDear, false).AsBool() ? Warning : ClassicDarkFrame.Value);
            Ink(_times[row], _times[row].GetMeta(NativeRepurchase.Expired, false).AsBool() ? Warning : ClassicDarkFrame.Value);
        }
    }

    private static void Ink(Label label, Color color)
    {
        if (label.GetThemeColor("font_color") != color) label.AddThemeColorOverride("font_color", color);
    }
}
