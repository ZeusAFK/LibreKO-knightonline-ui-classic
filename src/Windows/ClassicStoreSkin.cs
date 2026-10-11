using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;
namespace KnightOnlineUiClassic.Windows;

public static class ClassicStoreSkin
{
    public static void Extend(Control body) => body.Ready += () => Callable.From(() => Apply(body)).CallDeferred();
    public static Control? Apply(Control body)
    {
        Node? root = body; while (root != null && root is not HudWindow) root = root.GetParent();
        if (root is not HudWindow window || window.HasMeta("classic_store") || window.GetMeta("classic_store_controls", 0).AsInt32() != 1) return null;
        window.SetMeta("classic_store", true);
        // The original Power Up Store art is optional local content; without it the store keeps the bundled Classic design.
        Control panel = ClassicStoreMykoLayout.Art != null ? new ClassicStoreMykoPanel(window, body) : new ClassicStorePanel(window, body);
        window.AddChild(panel);
        var overlay = window.GetChildren().OfType<Control>().Single(c => c.Name == "pus_overlay"); window.MoveChild(overlay, window.GetChildCount() - 1);
        window.ResetSize(); return panel;
    }
}

/// <summary>The plain straight Classic frame on an opaque black ground (used by the store and its modals).</summary>
public partial class ClassicStoreFrame : Control
{
    public ClassicStoreFrame() { MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.Nearest; Resized += QueueRedraw; }
    public override void _Draw() => Paint(this, Size);
    public static void Paint(CanvasItem canvas, Vector2 size)
    {
        canvas.DrawRect(new Rect2(Vector2.Zero, size), Colors.Black);
        var texture = Plugin.Kit.Texture("ui_message_us.png"); if (texture == null) return;
        foreach (var (source, target, horizontal) in ClassicStoreLayout.FrameParts(size))
        {
            float run = horizontal ? source.Size.X : source.Size.Y, length = horizontal ? target.Size.X : target.Size.Y;
            for (float at = 0; at < length; at += run)
            {
                float step = Math.Min(run, length - at);
                var from = horizontal ? new Rect2(source.Position, new Vector2(step, source.Size.Y)) : new Rect2(source.Position, new Vector2(source.Size.X, step));
                var to = horizontal ? new Rect2(target.Position + new Vector2(at, 0), new Vector2(step, target.Size.Y)) : new Rect2(target.Position + new Vector2(0, at), new Vector2(target.Size.X, step));
                canvas.DrawTextureRectRegion(texture, to, from);
            }
        }
    }
}

public partial class ClassicStorePanel : Control
{
    public HudWindow Window { get; }
    private readonly Control _body, _overlay, _grip, _tools, _wallet;
    private readonly VBoxContainer _categoryList;
    private readonly Control _tabs;
    private readonly PanelContainer _well;
    private readonly List<Button> _plates = new();
    private readonly Label _title;
    private readonly Button _close;
    private readonly PanelContainer[] _modals;
    private bool _tabsQueued;
    private static LayoutNode TabArt => Plugin.Kit.Layout("{nation}_various_frame_us").All().First(n => n.IsButton && n.Id == "btn_quest");
    public ClassicStorePanel(HudWindow window, Control body)
    {
        Window = window; _body = body; Name = "classic_store"; TextureFilter = TextureFilterEnum.Nearest;
        var theme = StoreTheme();
        foreach (bool featured in new[] { false, true }) foreach (bool active in new[] { false, true })
        {
            var box = ClassicStoreLayout.Box(8, active); box.ContentMarginTop = 26;
            if (featured) box.BorderColor = new Color("bd9b59");
            theme.SetStylebox("pus_card_" + (featured ? "featured_" : "") + (active ? "active" : "normal"), "PanelContainer", box);
        }
        Theme = theme; window.Theme = theme;
        _overlay = CharacterDetailsSkin.Tree(window).OfType<Control>().Single(c => c.Name == "pus_overlay");
        _modals = Modals(_overlay);
        var header = window.Header!.GetChildren().OfType<HBoxContainer>().Single();
        _title = header.GetChildren().OfType<Label>().Last(); _close = header.GetChildren().OfType<Button>().Single(b => b.TooltipText == "Close");
        foreach (var child in window.GetChildren().OfType<Control>().Where(c => c != _overlay)) child.Visible = false;
        window.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        body.Reparent(this); body.Visible = true;
        _title.Reparent(this); _title.MouseFilter = MouseFilterEnum.Ignore; _title.CustomMinimumSize = Vector2.Zero;
        _title.HorizontalAlignment = HorizontalAlignment.Center; _title.VerticalAlignment = VerticalAlignment.Center;
        _title.ClipText = true; _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _title.AddThemeColorOverride("font_color", ClassicDesign.FrameTitle); Font(_title, 13);
        ClassicPartySkin.Drag(this, header, ClassicStoreLayout.Drag(1024)); _grip = GetChildren().OfType<Control>().Single(c => c.Name == "party_drag");
        ClassicMerchantSkin.Button(_close, Plugin.Kit.Layout("co_shoppingmall_us").Find("btn_close")!, this, ClassicStoreLayout.Close(1024), "");
        _close.FocusMode = FocusModeEnum.All;
        DescriptionScroll(_overlay);
        Watch(this, body, Escape); Watch(this, _overlay, Escape);
        T Part<T>(string name) where T : Node => CharacterDetailsSkin.Tree(body).OfType<T>().Single(c => c.Name == "pus_" + name);
        // The original store keeps the search row at the top of the window and the cash display at the top of the sidebar.
        _tools = Part<BoxContainer>("tools"); _tools.SetMeta("pus_header_row", true); ((BoxContainer)_tools).Vertical = false;
        foreach (var spacer in _tools.GetChildren().OfType<Control>().Where(c => c is not Container && c is not Button)) spacer.Visible = false;
        _tools.Reparent(this);
        _wallet = (Control)Part<Label>("cash").GetParent().GetParent().GetParent(); _wallet.Name = "pus_wallet"; _wallet.Reparent(this);
        // Categories become the original tab strip over the item well; the native list stays the source of truth.
        _categoryList = Part<VBoxContainer>("category_list"); Part<Control>("categories").Visible = false;
        var column = (Control)Part<ScrollContainer>("grid_scroll").GetParent(); var columns = column.GetParent();
        var main = Owned(new VBoxContainer { Name = "pus_main", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill });
        main.AddThemeConstantOverride("separation", 0);
        columns.AddChild(main); columns.MoveChild(main, column.GetIndex());
        // A plain control: the tab rows follow the well width and never widen the store themselves.
        _tabs = Owned(new Control { Name = "pus_tabs", MouseFilter = MouseFilterEnum.Ignore });
        main.AddChild(_tabs); _tabs.Resized += PlaceTabs;
        var well = _well = Owned(new PanelContainer { Name = "pus_well", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill });
        well.AddThemeStyleboxOverride("panel", ClassicStoreLayout.Box(ClassicStoreLayout.WellMargin)); main.AddChild(well);
        column.Reparent(well); well.Draw += OpenSelectedTab;
        _categoryList.ChildEnteredTree += _ => QueueTabs(); _categoryList.ChildExitingTree += _ => QueueTabs(); QueueTabs();
        FrameModals(_modals);
        body.MinimumSizeChanged += UpdateMinimumSize; _tools.MinimumSizeChanged += Arrange; _wallet.MinimumSizeChanged += Arrange; Resized += Arrange;
        window.VisibilityChanged += () => { if (!window.Visible) foreach (var option in CharacterDetailsSkin.Tree(this).Concat(CharacterDetailsSkin.Tree(_overlay)).OfType<OptionButton>()) option.GetPopup().Hide(); };
    }
    internal static Theme StoreTheme()
    {
        var theme = new Theme { DefaultFont = Plugin.Kit.Bold, DefaultFontSize = 13 };
        theme.SetStylebox("pus_contact_normal", "PanelContainer", ClassicStoreLayout.Box(6));
        theme.SetStylebox("pus_contact_active", "PanelContainer", ClassicStoreLayout.Box(6, true));
        return theme;
    }
    internal static PanelContainer[] Modals(Control overlay) => CharacterDetailsSkin.Tree(overlay).OfType<PanelContainer>().Where(c => c.Name.ToString() is "pus_details" or "pus_gift_box").ToArray();
    /// <summary>The details and gift modals use the plain store frame.</summary>
    internal static void FrameModals(IEnumerable<PanelContainer> modals)
    {
        foreach (var modal in modals)
        {
            modal.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            var inset = modal.GetChildren().OfType<MarginContainer>().First();
            inset.AddThemeConstantOverride("margin_left", 16); inset.AddThemeConstantOverride("margin_top", 14);
            inset.AddThemeConstantOverride("margin_right", 16); inset.AddThemeConstantOverride("margin_bottom", 16);
            modal.AddChild(new ClassicStoreFrame { Name = "pus_modal_frame", ShowBehindParent = true });
            CharacterDetailsSkin.Tree(modal).OfType<HBoxContainer>().First().GetChildren().OfType<Label>().FirstOrDefault()?.AddThemeColorOverride("font_color", ClassicDesign.FrameTitle);
        }
    }
    /// <summary>The details description scrolls inside five whole lines.</summary>
    internal static void DescriptionScroll(Control overlay)
    {
        var description = CharacterDetailsSkin.Tree(overlay).OfType<Label>().Single(c => c.Name == "pus_details_description");
        var parent = description.GetParent(); int index = description.GetIndex();
        var scroll = new ScrollContainer { Name = "pus_description_scroll", CustomMinimumSize = new Vector2(0, ClassicStoreLayout.DescriptionHeight), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, Visible = description.Visible };
        // The text keeps the Classic six-pixel gap before the scroll bar.
        var gap = new MarginContainer { Name = "pus_description_inset", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        gap.AddThemeConstantOverride("margin_right", 6 + 10); gap.AddThemeConstantOverride("margin_left", 6 + 10);
        parent.AddChild(scroll); parent.MoveChild(scroll, index); scroll.AddChild(gap); description.Reparent(gap); description.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        description.VisibilityChanged += () => scroll.Visible = description.Visible;
    }
    /// <summary>Plugin-owned controls carry the store marker so the native restyling pass leaves them alone.</summary>
    internal static T Owned<T>(T control) where T : Control { control.SetMeta("classic_store_control", true); return control; }
    public override Vector2 _GetMinimumSize()
    {
        var size = (_body?.GetCombinedMinimumSize() ?? Vector2.Zero) + ClassicStoreLayout.Chrome;
        if (_tools == null || _wallet == null) return size;
        return new Vector2(Math.Max(size.X, 2 * ClassicStoreLayout.Inset + _tools.GetCombinedMinimumSize().X + 16 + _wallet.GetCombinedMinimumSize().X), size.Y);
    }
    public override void _Ready() => Arrange();
    private void Arrange()
    {
        var rect = ClassicStoreLayout.Body(Size); _body.Position = rect.Position; _body.Size = rect.Size;
        _close.Position = ClassicStoreLayout.Close(Size.X).Position; _close.Size = ClassicStoreLayout.Close(Size.X).Size;
        var title = ClassicStoreLayout.Title(Size.X); _title.Position = title.Position; _title.Size = title.Size;
        var drag = ClassicStoreLayout.Drag(Size.X); _grip.Position = drag.Position; _grip.Size = drag.Size;
        _tools.Position = ClassicStoreLayout.Tools; _tools.Size = new Vector2(_tools.GetCombinedMinimumSize().X, ClassicStoreLayout.ToolsHeight);
        var wallet = ClassicStoreLayout.Wallet(Size.X, _wallet.GetCombinedMinimumSize()); _wallet.Position = wallet.Position; _wallet.Size = wallet.Size;
        QueueRedraw();
    }
    private void QueueTabs()
    {
        if (_tabsQueued) return; _tabsQueued = true;
        Callable.From(BuildTabs).CallDeferred();
    }
    /// <summary>Mirrors the native category buttons as Classic tabs; a tab press is the native button's press.</summary>
    private void BuildTabs()
    {
        _tabsQueued = false; if (!IsInstanceValid(_tabs)) return;
        foreach (var old in _plates) { _tabs.RemoveChild(old); old.QueueFree(); }
        _plates.Clear();
        foreach (var native in _categoryList.GetChildren().OfType<Button>().Where(b => !b.IsQueuedForDeletion()))
        {
            bool selected = native.GetMeta("pus_category_selected", false).AsBool();
            var tab = Owned(new Button
            {
                Name = native.Name.ToString().Replace("pus_category_", "pus_tab_"), Text = native.Text, TooltipText = native.TooltipText,
                ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, FocusMode = FocusModeEnum.None,
                Icon = native.Icon, IconAlignment = HorizontalAlignment.Right, Alignment = HorizontalAlignment.Center,
            });
            tab.SetMeta("pus_tab_selected", selected);
            ClassicReportDesign.StyleButton(tab, true, TabArt); tab.AddThemeFontSizeOverride("font_size", ClassicStoreLayout.TabFont);
            foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed" })
            {
                var box = (StyleBox)tab.GetThemeStylebox(state).Duplicate(); box.ContentMarginLeft = box.ContentMarginRight = ClassicStoreLayout.TabPadding; tab.AddThemeStyleboxOverride(state, box);
            }
            if (selected)
            {
                var open = tab.GetThemeStylebox("pressed"); tab.AddThemeStyleboxOverride("normal", open); tab.AddThemeStyleboxOverride("hover", open);
            }
            // The open tab carries the strong ink; the others step back so the selection reads on both nations' art.
            foreach (string state in new[] { "font_color", "font_pressed_color", "font_hover_pressed_color" }) tab.AddThemeColorOverride(state, ClassicStoreLayout.TabInk(selected));
            tab.AddThemeColorOverride("font_hover_color", ClassicStoreLayout.TabInk(true));
            if (tab.Icon != null)
            {
                tab.AddThemeConstantOverride("icon_max_width", ClassicStoreLayout.TabIcon); tab.AddThemeConstantOverride("h_separation", 4);
                var clock = new Color(ClassicDesign.Karus ? "7a4c00" : "f2c94c");
                foreach (string state in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color", "icon_focus_color" }) tab.AddThemeColorOverride(state, clock);
            }
            float caption = Plugin.Kit.Bold.GetStringSize(tab.Text, HorizontalAlignment.Left, -1, ClassicStoreLayout.TabFont).X;
            tab.SetMeta("pus_tab_width", ClassicStoreLayout.TabWidth(caption, tab.Icon != null));
            tab.Pressed += () => { if (IsInstanceValid(native)) native.EmitSignal(BaseButton.SignalName.Pressed); };
            _tabs.AddChild(tab); _plates.Add(tab);
        }
        PlaceTabs();
    }
    private void PlaceTabs()
    {
        float height = Platform.TouchUi ? ClassicStoreLayout.TouchTabHeight : ClassicStoreLayout.TabHeight;
        var (rects, total) = ClassicStoreLayout.TabRects(_plates.Select(p => (float)p.GetMeta("pus_tab_width").AsInt32()).ToArray(), _tabs.Size.X > 0 ? _tabs.Size.X : float.MaxValue, height);
        for (int i = 0; i < _plates.Count; i++) { _plates[i].Position = rects[i].Position; _plates[i].Size = rects[i].Size; }
        if (_tabs.CustomMinimumSize.Y != total) _tabs.CustomMinimumSize = new Vector2(0, total);
        _well.QueueRedraw();
    }
    /// <summary>Opens the well's top edge under a selected tab of the bottom row, so the tab joins the item well.</summary>
    private void OpenSelectedTab()
    {
        var tab = _plates.FirstOrDefault(p => p.GetMeta("pus_tab_selected").AsBool());
        if (tab == null || tab.GetRect().End.Y < _tabs.Size.Y - 0.5f) return;
        _well.DrawRect(new Rect2(tab.Position.X + 1, 0, tab.Size.X - 2, 1), Colors.Black);
    }
    internal static void Font(Control control, int size = 13)
    {
        control.AddThemeFontOverride("font", Plugin.Kit.Bold); control.AddThemeFontSizeOverride("font_size", size); control.AddThemeConstantOverride("outline_size", 0);
    }
    /// <summary>Gives every native store control (now and later) the Classic fonts, inputs, buttons and scroll bars.</summary>
    internal static void Watch(Control host, Node node, Action escape)
    {
        if (node.HasMeta("classic_store_control")) return; node.SetMeta("classic_store_control", true);
        Style(host, node, escape); foreach (var child in node.GetChildren()) Watch(host, child, escape); node.ChildEnteredTree += n => Watch(host, n, escape);
    }
    private static void Style(Control host, Node node, Action escape)
    {
        if (node is not Control control) return;
        int size = control is Label l ? Math.Clamp(l.GetThemeFontSize("font_size"), 11, 13) : 13;
        if (control.Name.ToString() is "pus_details_name" or "pus_recipient_name") size = 16;
        Font(control, size);
        if (control is LineEdit edit)
        {
            ClassicVendorSkin.Input(edit); Font(edit); edit.TextDirection = TextDirection.Ltr; edit.KeepEditingOnTextSubmit = true;
            edit.GuiInput += ev => { if (ev is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) { escape(); edit.AcceptEvent(); } };
        }
        if (control is Button button)
        {
            if (button.TooltipText is "Close" or "Remove from the cart" or "Buy for yourself instead")
            {
                button.Icon = null; button.Text = ""; button.CustomMinimumSize = Platform.TouchUi ? new Vector2(40, 40) : new Vector2(24, 24);
                ClassicVendorSkin.Button(button, Plugin.Kit.Layout("co_questtalk_us").Find("btn_close")!);
            }
            else
            {
                var icon = button.Icon;
                ClassicReportDesign.StyleButton(button, false, Plugin.Kit.Layout("co_change_bill_us").Find("btn_ok")!); button.Icon = icon;
                foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" }) button.AddThemeColorOverride(state, new Color("ead7a6"));
                button.AddThemeColorOverride("font_disabled_color", new Color("a0937c"));
                if (button.HasMeta("pus_category_selected")) button.TooltipText = button.Text + (button.TooltipText.Length > 0 ? "\n" + button.TooltipText : "");
                if (button.HasMeta("pus_category_selected") && button.GetMeta("pus_category_selected").AsBool()) button.AddThemeStyleboxOverride("normal", button.GetThemeStylebox("pressed"));
                foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
                {
                    var box = (StyleBox)button.GetThemeStylebox(state).Duplicate(); box.ContentMarginLeft = 6; box.ContentMarginRight = 6; button.AddThemeStyleboxOverride(state, box);
                }
                float height = Platform.TouchUi ? Math.Max(40, button.CustomMinimumSize.Y) : button.TooltipText is "One more" or "One less" ? 24 : 28;
                button.CustomMinimumSize = new Vector2(button.CustomMinimumSize.X, height);
            }
            Font(button); button.FocusMode = FocusModeEnum.All;
            if (button is OptionButton option)
            {
                var popup = option.GetPopup(); popup.AddThemeFontOverride("font", Plugin.Kit.Bold); popup.AddThemeFontSizeOverride("font_size", 13);
                popup.AddThemeColorOverride("font_color", ClassicDesign.Text); popup.AddThemeStyleboxOverride("panel", ClassicStoreLayout.Box(4)); popup.AddThemeStyleboxOverride("hover", ClassicStoreLayout.Box(4, true));
            }
        }
        if (control is ScrollContainer sc)
        {
            // Four whole 44 px recipient rows (4 px apart) on desktop.
            if (sc.GetParent().Name == "pus_gift_pick")
            {
                sc.CustomMinimumSize = new Vector2(0, Platform.TouchUi ? 240 : 188);
                // Desktop: four whole rows that scroll by rows; the Friend/Clan tags keep the Classic gap before the scroll bar.
                if (!Platform.TouchUi) Callable.From(() => host.AddChild(new ClassicRowScroll(sc, 188, 48))).CallDeferred();
            }
            ClassicDarkFrame.ScrollBar(sc.GetVScrollBar(), 10);
        }
        if (control is PanelContainer panel)
        {
            if (panel.HasMeta("pus_card_id"))
            {
                string style = "pus_card_" + (panel.GetMeta("pus_card_featured", false).AsBool() ? "featured_" : "") + (panel.GetMeta("pus_card_active", false).AsBool() ? "active" : "normal");
                panel.AddThemeStyleboxOverride("panel", panel.GetThemeStylebox(style)); return;
            }
            if (panel.Name.ToString() is "pus_card_icon" || panel.GetChildren().OfType<TextureRect>().Any(t => t.Name == "pus_details_icon")) panel.AddThemeStyleboxOverride("panel", ClassicStoreLayout.IconBox());
            else if (panel.Name.ToString() is "pus_categories" or "pus_cart" or "pus_gift_banner" or "pus_gift_card" || panel.Name.ToString().StartsWith("pus_cart_line_")) panel.AddThemeStyleboxOverride("panel", ClassicStoreLayout.Box());
            else if (panel.GetThemeStylebox("panel") is StyleBoxFlat flat)
            {
                var box = (StyleBoxFlat)flat.Duplicate(); box.SetCornerRadiusAll(0); box.ShadowSize = 0; panel.AddThemeStyleboxOverride("panel", box);
            }
        }
    }
    private void Escape() { if (!EscapeOverlay(this, _overlay, _modals)) _close.EmitSignal(BaseButton.SignalName.Pressed); }
    /// <summary>Escape closes an open sort popup, then an open modal; false when the store itself should close.</summary>
    internal static bool EscapeOverlay(Control root, Control overlay, PanelContainer[] modals)
    {
        var option = CharacterDetailsSkin.Tree(root).Concat(CharacterDetailsSkin.Tree(overlay)).OfType<OptionButton>().FirstOrDefault(o => o.GetPopup().Visible);
        if (option != null) { option.GetPopup().Hide(); return true; }
        var modal = modals.FirstOrDefault(m => m.Visible && overlay.Visible);
        if (modal == null) return false;
        CharacterDetailsSkin.Tree(modal).OfType<Button>().Single(b => b.TooltipText == "Close").EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }
    public override void _UnhandledKeyInput(InputEvent ev)
    {
        if (!Window.IsVisibleInTree() || ev is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        Escape(); GetViewport().SetInputAsHandled();
    }
    public override void _Draw() { if (Size.X >= 400) ClassicStoreFrame.Paint(this, Size); }
}
