using Godot;
using LibreKO;
using LibreKO.Plugins;
using KnightOnlineUiClassic.Layout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The Classic look of the social windows the client lays out as flows (nation chat, clan and join-request
/// boards, leader transfer, commander, command list). The client's own content keeps its controls, callbacks
/// and flow; the plugin frames it in the plain Classic frame through the client's frame seam and restyles
/// every control with the original pieces the Messenger uses.
/// </summary>
public static class ClassicSocialSkin
{
    public static readonly string[] WindowIds = ClassicSocialLayout.WindowIds;

    public static void Register(PluginUi ui)
    {
        foreach (var id in WindowIds) ui.FrameWindow(id, frame => new ClassicSocialFrame(frame));
    }
}

/// <summary>Plain Classic frame around a native window's content: title row, round close button and even insets.</summary>
public partial class ClassicSocialFrame : Container
{
    private readonly WindowFrame? _frame;
    private readonly Control _content = null!, _grip = null!;
    private readonly Label _title = null!;
    private readonly Button? _close;
    private readonly ClassicFrame _art = null!;

    public ClassicSocialFrame() { }

    public ClassicSocialFrame(WindowFrame frame)
    {
        _frame = frame;
        Name = "classic_social"; MouseFilter = MouseFilterEnum.Stop; TextureFilter = TextureFilterEnum.Nearest;
        _art = new ClassicFrame { Name = "social_frame", BackgroundAlpha = 1, BackgroundColor = Colors.Black };
        AddChild(_art);
        _grip = new Control { Name = "social_drag", MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.Move };
        AddChild(_grip);
        frame.SetDragHandle(_grip);
        _title = new Label { Name = "social_title", Text = frame.Title, MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        _title.ClipText = true; _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        ClassicSocialStyle.Font(_title, true, ClassicSocialLayout.HeadingSize);
        _title.AddThemeColorOverride("font_color", ClassicDesign.FrameTitle);
        AddChild(_title);
        frame.TitleChanged += title => { if (IsInstanceValid(_title)) _title.Text = title; };
        if (frame.Closable)
        {
            _close = new Button { Name = "social_close", TooltipText = "Close", FocusMode = FocusModeEnum.None };
            ClassicVendorSkin.Button(_close, Plugin.Kit.Layout("co_questtalk_us").Find("btn_close")!);
            _close.Text = "";
            _close.Pressed += () => _frame?.Close();
            AddChild(_close);
        }
        _content = frame.Content;
        _content.GetParent()?.RemoveChild(_content);
        foreach (var side in new[] { "left", "top", "right", "bottom" }) _content.AddThemeConstantOverride("margin_" + side, 0);
        AddChild(_content);
        ClassicSocialStyle.Watch(_content);
        if (frame.Id == "command_list") Callable.From(() => CommandRows(_content)).CallDeferred();
        foreach (var part in GetChildren().OfType<Control>())
            part.SizeFlagsHorizontal = part.SizeFlagsVertical = SizeFlags.Fill;
    }

    public Control Content => _content;

    /// <summary>Command list: the group list shows exactly its groups, the command list whole rows that scroll by rows.</summary>
    private static void CommandRows(Control content)
    {
        if (!IsInstanceValid(content)) return;
        var lists = ClassicSocialStyle.Descendants(content).OfType<ItemList>().ToArray();
        for (int i = 0; i < lists.Length && i < 2; i++)
            if (!lists[i].HasNode("classic_list_rows")) lists[i].AddChild(new ClassicListRows(lists[i], fitRows: i == 0));
    }

    public override Vector2 _GetMinimumSize() =>
        _content == null ? Vector2.Zero : ClassicSocialLayout.Frame(_content.GetCombinedMinimumSize());

    public override void _Notification(int what)
    {
        if (what != NotificationSortChildren || _content == null) return;
        var size = Size;
        FitChildInRect(_art, new Rect2(Vector2.Zero, size));
        FitChildInRect(_grip, ClassicSocialLayout.Drag(size.X));
        FitChildInRect(_title, ClassicSocialLayout.Title(size.X));
        if (_close != null) FitChildInRect(_close, ClassicSocialLayout.Close(size.X));
        FitChildInRect(_content, ClassicSocialLayout.Content(size));
        _title.SetMeta("social_expected_rect", ClassicSocialLayout.Title(size.X));
        _close?.SetMeta("social_expected_rect", ClassicSocialLayout.Close(size.X));
        _content.SetMeta("social_expected_rect", ClassicSocialLayout.Content(size));
    }
}

/// <summary>
/// Restyles native flow content with the Classic pieces: Arial and the Classic inks on labels, the original
/// action and tab buttons, black edit fields with the gold line, section boxes, whole-row lists with the
/// Messenger's row rule and selection, and the Classic scroll bar. Rows the client adds later are styled as
/// they arrive; inks the client changes later are mapped again while the window is visible.
/// </summary>
public static class ClassicSocialStyle
{
    public const string Styled = "classic_social_styled";
    public static Color ButtonInk => new("efd9b4");
    public static Color ButtonDisabledInk => new("887e70");
    public static Color Online => new("91c678");
    public static Color Rule => ClassicDarkFrame.Line;
    public static Color RowRule => new("544a37");
    public static Color SelectedEdge => new("e7c773");

    public static void Font(Control control, bool bold, int size)
    {
        control.AddThemeFontOverride("font", bold ? Plugin.Kit.Bold : Plugin.Kit.Regular);
        control.AddThemeFontSizeOverride("font_size", size);
        control.AddThemeConstantOverride("outline_size", 0);
        control.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
    }

    /// <summary>Styles the subtree now, again for nodes that enter it later, and keeps label inks Classic.</summary>
    public static void Watch(Control root)
    {
        Apply(root);
        Callable.From(() => { if (GodotObject.IsInstanceValid(root)) Apply(root); }).CallDeferred();
        root.TreeEntered += () => Callable.From(() => { if (GodotObject.IsInstanceValid(root)) Apply(root); }).CallDeferred();
        Watch(root, root);
        root.AddChild(new ClassicSocialInk(root) { Name = "classic_social_ink" });
    }

    private static void Watch(Control root, Node node)
    {
        node.ChildEnteredTree += child =>
        {
            Watch(root, child);
            Callable.From(() => { if (GodotObject.IsInstanceValid(child) && !child.IsQueuedForDeletion()) Apply(child); }).CallDeferred();
        };
        foreach (var child in node.GetChildren()) Watch(root, child);
    }

    public static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            yield return child;
            foreach (var inner in Descendants(child)) yield return inner;
        }
    }

    public static void Apply(Node node)
    {
        if (node is Control control && !control.HasMeta(Styled))
        {
            control.SetMeta(Styled, true);
            Style(control);
        }
        foreach (var child in node.GetChildren()) Apply(child);
    }

    private static void Style(Control control)
    {
        switch (control)
        {
            case CheckBox or CheckButton: Check((Button)control); break;
            case OptionButton option: Option(option); break;
            case Button button: Button(button); break;
            case LineEdit edit: Input(edit); break;
            case TextEdit text: Text(text); break;
            case ItemList list: List(list); break;
            case ScrollContainer scroll: Scroll(scroll); break;
            case HSeparator rule: rule.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = Rule, Thickness = 1 }); break;
            case PanelContainer panel when control is not HudWindow: Panel(panel); break;
            case Label label: Label(label); break;
        }
    }

    public static void Label(Label label)
    {
        if (label.GetParent() is Button) return;
        int native = label.GetThemeFontSize("font_size");
        bool wrapped = label.AutowrapMode != TextServer.AutowrapMode.Off;
        int size = native >= 15 ? ClassicSocialLayout.NameSize : native >= 13 && !wrapped ? ClassicSocialLayout.HeadingSize : ClassicSocialLayout.FontSize;
        Font(label, !wrapped || native >= 15, size);
        Ink(label);
    }

    /// <summary>Maps a native ink to the Classic palette; inks without a Classic counterpart stay.</summary>
    public static Color? ClassicInk(Color native)
    {
        if (native == UiTheme.Gold || native == UiTheme.GoldBright || native == UiTheme.GoldVivid) return ClassicDarkFrame.Heading;
        if (native == UiTheme.TextHi || native == new Color("f1ead9")) return ClassicDesign.Text;
        if (native == UiTheme.TextLo || native == UiTheme.TextDim) return ClassicDesign.Muted;
        if (native == UiTheme.Good) return Online;
        return null;
    }

    public static void Ink(Label label)
    {
        if (ClassicInk(label.GetThemeColor("font_color")) is { } ink) label.AddThemeColorOverride("font_color", ink);
    }

    /// <summary>The original Messenger action button: one art for both nations, Arial Bold, light ink.</summary>
    public static void Button(Button button)
    {
        if (IconOnly(button)) { Cell(button); return; }
        ClassicVendorSkin.Button(button, Plugin.Kit.Layout("co_change_bill_us").Find("btn_ok")!);
        Pad(button);
        Font(button, true, ClassicSocialLayout.FontSize);
        button.Icon = null; button.FocusMode = Control.FocusModeEnum.None; button.ClipText = false;
        button.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        button.CustomMinimumSize = new Vector2(Math.Max(button.CustomMinimumSize.X, 0), ClassicSocialLayout.ActionHeight);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            button.AddThemeColorOverride(state, ButtonInk);
        button.AddThemeColorOverride("font_disabled_color", ButtonDisabledInk);
        if (!button.ToggleMode) return;
        // Tabs and toggle choices (leader transfer) read like the Messenger's list rows on the dark frame of both
        // nations: the selected one carries the gold selection line, the others the row rule.
        foreach (var state in new[] { "normal", "hover", "disabled" }) button.AddThemeStyleboxOverride(state, RowBox(false));
        foreach (var state in new[] { "pressed", "hover_pressed" }) button.AddThemeStyleboxOverride(state, RowBox(true));
        button.AddThemeColorOverride("font_color", ClassicDesign.Text);
        button.AddThemeColorOverride("font_hover_color", ClassicDarkFrame.Heading);
        button.AddThemeColorOverride("font_pressed_color", ClassicDarkFrame.Heading);
        button.AddThemeColorOverride("font_hover_pressed_color", ClassicDarkFrame.Heading);
        button.Alignment = button.GetParent() is HBoxContainer ? HorizontalAlignment.Center : HorizontalAlignment.Left;
    }

    /// <summary>Captions keep the Messenger buttons' side room instead of touching the art's end caps.</summary>
    private static void Pad(Button button)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
        {
            if (button.GetThemeStylebox(state) is not StyleBox box) continue;
            var padded = (StyleBox)box.Duplicate();
            padded.ContentMarginLeft = padded.ContentMarginRight = ClassicSocialLayout.ButtonPadding;
            button.AddThemeStyleboxOverride(state, padded);
        }
    }

    private static bool IconOnly(Button button) =>
        string.IsNullOrEmpty(button.Text) && button.GetChildren().OfType<TextureRect>().Any();

    /// <summary>Skill and item icons on a button sit in the original inventory cell.</summary>
    private static void Cell(Button button)
    {
        var cell = ClassicSkin.PanelStyle();
        cell.SetContentMarginAll(0);
        var hover = (StyleBoxTexture)cell.Duplicate(); hover.ModulateColor = new Color(1.25f, 1.2f, 1.05f);
        var pressed = (StyleBoxTexture)cell.Duplicate(); pressed.ModulateColor = new Color(.75f, .75f, .75f);
        var disabled = (StyleBoxTexture)cell.Duplicate(); disabled.ModulateColor = new Color(.55f, .55f, .55f);
        button.AddThemeStyleboxOverride("normal", cell);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("hover_pressed", pressed);
        button.AddThemeStyleboxOverride("disabled", disabled);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        foreach (var label in button.GetChildren().OfType<Label>())
        {
            Font(label, true, ClassicSocialLayout.HeadingSize);
            label.AddThemeColorOverride("font_color", Colors.White);
            label.AddThemeColorOverride("font_outline_color", Colors.Black);
            label.AddThemeConstantOverride("outline_size", 3);
        }
    }

    private static void Check(Button check)
    {
        Font(check, true, ClassicSocialLayout.FontSize);
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
            check.AddThemeStyleboxOverride(state, new StyleBoxEmpty { ContentMarginRight = 4 });
        foreach (var state in new[] { "font_color", "font_focus_color" }) check.AddThemeColorOverride(state, ClassicDesign.Text);
        foreach (var state in new[] { "font_hover_color", "font_pressed_color", "font_hover_pressed_color" }) check.AddThemeColorOverride(state, ClassicDarkFrame.Heading);
        check.AddThemeColorOverride("font_disabled_color", ClassicDesign.Muted);
        check.FocusMode = Control.FocusModeEnum.None;
    }

    private static void Option(OptionButton option)
    {
        ClassicVendorSkin.Button(option, Plugin.Kit.Layout("co_change_bill_us").Find("btn_ok")!);
        Pad(option);
        Font(option, true, ClassicSocialLayout.FontSize);
        option.CustomMinimumSize = new Vector2(option.CustomMinimumSize.X, ClassicSocialLayout.ActionHeight);
        option.ClipText = false;
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            option.AddThemeColorOverride(state, ButtonInk);
        option.AddThemeColorOverride("font_disabled_color", ButtonDisabledInk);
        Menu(option.GetPopup());
    }

    /// <summary>The Classic drop-down and context menu: black panel, gold line, selected-row hover.</summary>
    public static void Menu(PopupMenu menu)
    {
        var panel = ClassicDarkFrame.InputBox();
        panel.BgColor = new Color(0, 0, 0, .94f);
        panel.SetContentMarginAll(4);
        menu.AddThemeStyleboxOverride("panel", panel);
        var hover = new StyleBoxFlat { BgColor = ClassicDarkFrame.SelectedRow };
        hover.SetContentMarginAll(0);
        menu.AddThemeStyleboxOverride("hover", hover);
        menu.AddThemeFontOverride("font", Plugin.Kit.Bold);
        menu.AddThemeFontSizeOverride("font_size", ClassicSocialLayout.FontSize);
        menu.AddThemeColorOverride("font_color", ClassicDesign.Text);
        menu.AddThemeColorOverride("font_hover_color", ClassicDarkFrame.Heading);
        menu.AddThemeColorOverride("font_disabled_color", ClassicDesign.Muted);
        menu.AddThemeConstantOverride("v_separation", 6);
        menu.AddThemeConstantOverride("item_start_padding", 6);
        menu.AddThemeConstantOverride("item_end_padding", 6);
    }

    public static void Input(LineEdit input)
    {
        Font(input, true, ClassicDesign.FontSize);
        foreach (var state in new[] { "normal", "focus", "read_only" })
        {
            var box = ClassicDarkFrame.InputBox();
            box.ContentMarginRight += input.GetThemeStylebox("normal") is { } native ? Math.Max(0, native.ContentMarginRight - 6) : 0;
            input.AddThemeStyleboxOverride(state, box);
        }
        input.AddThemeColorOverride("font_color", ClassicDesign.Text);
        input.AddThemeColorOverride("font_uneditable_color", ClassicDesign.Muted);
        input.AddThemeColorOverride("font_placeholder_color", ClassicDesign.Muted);
        input.AddThemeColorOverride("caret_color", ClassicDesign.Text);
        input.CustomMinimumSize = new Vector2(input.CustomMinimumSize.X, ClassicSocialLayout.InputHeight);
        input.TextDirection = Control.TextDirection.Ltr;
    }

    private static void Text(TextEdit text)
    {
        Font(text, false, ClassicDesign.FontSize);
        foreach (var state in new[] { "normal", "focus", "read_only" }) text.AddThemeStyleboxOverride(state, ClassicDarkFrame.InputBox());
        text.AddThemeColorOverride("font_color", ClassicDesign.Text);
        text.AddThemeColorOverride("font_readonly_color", ClassicDesign.Muted);
        text.AddThemeColorOverride("font_placeholder_color", ClassicDesign.Muted);
        text.AddThemeColorOverride("caret_color", ClassicDesign.Text);
        ScrollBar(text.GetVScrollBar());
    }

    private static void List(ItemList list)
    {
        var panel = ClassicDarkFrame.InputBox(); panel.SetContentMarginAll(4);
        list.AddThemeStyleboxOverride("panel", panel);
        list.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        foreach (var state in new[] { "selected", "selected_focus" }) list.AddThemeStyleboxOverride(state, RowBox(true));
        list.AddThemeStyleboxOverride("hovered", new StyleBoxFlat { BgColor = ClassicDarkFrame.SelectedRow });
        list.AddThemeFontOverride("font", Plugin.Kit.Bold);
        list.AddThemeFontSizeOverride("font_size", ClassicSocialLayout.FontSize);
        list.AddThemeColorOverride("font_color", ClassicDesign.Text);
        list.AddThemeColorOverride("font_hovered_color", ClassicDarkFrame.Heading);
        list.AddThemeColorOverride("font_selected_color", ClassicDarkFrame.Heading);
        list.AddThemeConstantOverride("outline_size", 0);
        ScrollBar(list.GetVScrollBar());
    }

    public static void Scroll(ScrollContainer scroll)
    {
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        ScrollBar(scroll.GetVScrollBar());
    }

    /// <summary>The shared dark-frame scroll bar at the Messenger's 10 px width.</summary>
    public static void ScrollBar(ScrollBar bar) => ClassicDarkFrame.ScrollBar(bar, 10);

    /// <summary>A list row: transparent with the Messenger's row rule, or the gold selection frame.</summary>
    public static StyleBoxFlat RowBox(bool selected)
    {
        var box = new StyleBoxFlat
        {
            BgColor = selected ? new Color("26251f") : Colors.Transparent,
            BorderColor = selected ? SelectedEdge : RowRule,
        };
        box.BorderWidthBottom = 1;
        if (selected) box.SetBorderWidthAll(1);
        box.ContentMarginLeft = box.ContentMarginRight = 6;
        box.ContentMarginTop = box.ContentMarginBottom = 2;
        return box;
    }

    private static void Panel(PanelContainer panel)
    {
        if (panel.GetThemeStylebox("panel") is not StyleBoxFlat native) return;
        if (IsRow(panel, native))
        {
            bool selected = native.BorderColor.A > 0 && native.BgColor == UiTheme.RowHover;
            // The row keeps the client's pitch: list heights are whole multiples of it.
            panel.AddThemeStyleboxOverride("panel", RowBox(selected));
            panel.SetMeta("social_row", true);
            panel.SetMeta("social_selected", selected);
            return;
        }
        var section = ClassicDarkFrame.SectionBox(0);
        section.SetContentMargin(Side.Left, 1); section.SetContentMargin(Side.Right, 1);
        section.SetContentMargin(Side.Top, 1); section.SetContentMargin(Side.Bottom, 1);
        panel.AddThemeStyleboxOverride("panel", section);
    }

    /// <summary>Rows are the panels a list stacks: siblings in a vertical box, or panels without a frame of their own.</summary>
    private static bool IsRow(PanelContainer panel, StyleBoxFlat native) =>
        native.BgColor.A == 0 && native.BorderColor.A == 0
        || native.BgColor == UiTheme.RowBg || native.BgColor == UiTheme.RowHover
        || panel.GetParent() is VBoxContainer list && list.GetChildren().OfType<PanelContainer>().Count() > 1;
}

/// <summary>Maps label inks the client changes after the build (online marks, enabled captions) to the Classic palette.</summary>
public partial class ClassicSocialInk : Node
{
    private readonly Control? _root;
    private double _elapsed;

    public ClassicSocialInk() { }

    public ClassicSocialInk(Control root) => _root = root;

    public override void _Process(double delta)
    {
        if (_root == null || !IsInstanceValid(_root) || !_root.IsVisibleInTree()) return;
        _elapsed += delta;
        if (_elapsed < .1) return;
        _elapsed = 0;
        Visit(_root);
    }

    private static void Visit(Node node)
    {
        if (node is Label label && label.HasMeta(ClassicSocialStyle.Styled)) ClassicSocialStyle.Ink(label);
        foreach (var child in node.GetChildren()) Visit(child);
    }
}
