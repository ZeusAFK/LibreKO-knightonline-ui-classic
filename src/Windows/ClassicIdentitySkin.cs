using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Native;

namespace KnightOnlineUiClassic.Windows;

public static class ClassicIdentitySkin
{
    public static readonly string[] WindowIds = { "creat_clan", "namechange", "disguise" };
    public static void Extend(Control body) => body.Ready += () => Callable.From(() => Apply(body)).CallDeferred();
    public static ClassicIdentityPanel? Apply(Control body)
    {
        Node? root = body;
        while (root != null && root is not HudWindow) root = root.GetParent();
        if (root is not HudWindow window || window.HasMeta("classic_identity")) return null;
        if (window.GetMeta("classic_identity_controls", 0).AsInt32() != 1) return null;
        window.SetMeta("classic_identity", true);
        var panel = new ClassicIdentityPanel(window, body); window.AddChild(panel); window.ResetSize();
        return panel;
    }
}

// Original artwork is drawn around independently editable, live game controls.
public partial class ClassicIdentityPanel : Control
{
    public HudWindow Window { get; }
    private readonly Control[] _native;
    private readonly LayoutNode _art;
    private readonly Label _title;
    private ScrollContainer? _forms;
    private ClassicIdentityScrollRail? _formsRail;
    // The original transformation lists use dense 19-pixel one-line rows.
    public const float DisguiseRow = 19;
    private Label? _note, _hint;
    public ClassicIdentityPanel(HudWindow window, Control body)
    {
        Window = window; Name = "classic_identity";
        Size = CustomMinimumSize = ClassicIdentityLayout.Size(window.Id);
        TextureFilter = TextureFilterEnum.Nearest;
        _art = Plugin.Kit.Layout(ClassicIdentityLayout.Artwork(window.Id));
        _native = CharacterDetailsSkin.Tree(body).OfType<Control>().ToArray();
        var bar = window.Header!.GetChildren().OfType<HBoxContainer>().Single();
        _title = bar.GetChildren().OfType<Label>().Last();
        foreach (var child in window.GetChildren().OfType<Control>()) child.Visible = false;
        window.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        if (ClassicIdentityLayout.Plain(window.Id))
        {
            AddChild(new ClassicFrame { Size = Size, BackgroundAlpha = 1, BackgroundColor = Colors.Black, ShowBehindParent = true });
            Place(_title, ClassicIdentityLayout.Title(window.Id)); Font(_title, 13, true);
            _title.AddThemeColorOverride("font_color", ClassicDesign.FrameTitle);
            _title.HorizontalAlignment = HorizontalAlignment.Left; _title.VerticalAlignment = VerticalAlignment.Center;
            _title.ClipText = true; _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        }
        ClassicPartySkin.Drag(this, bar, new Rect2(4, 2, Size.X - 8, ClassicIdentityLayout.Plain(window.Id) ? 36 : 23));
        switch (window.Id)
        {
            case "creat_clan": Clan(); break;
            case "namechange": Rename(); break;
            case "disguise": Disguise(); break;
        }
        ShownOnly.Gate(this, Sync);
    }
    private T Native<T>(string name) where T : Control => _native.OfType<T>().Single(c => GodotObject.IsInstanceValid(c) && !c.IsQueuedForDeletion() && c.Name == name);
    private static void Font(Control control, int size = 13, bool bold = false)
    {
        control.AddThemeFontOverride("font", bold ? Plugin.Kit.Bold : Plugin.Kit.Regular);
        control.AddThemeFontSizeOverride("font_size", size);
        control.AddThemeConstantOverride("outline_size", 0);
    }
    private void Place(Control control, Rect2 rect)
    {
        ClassicVendorSkin.Move(control, this, rect); Font(control);
        control.SetMeta("identity_expected_rect", rect);
    }
    private void Original(Control control, string field)
    {
        Place(control, ClassicIdentityLayout.Field(Window.Id, field));
        var reference = _art.Find(field)!;
        Font(control, reference.Size > 0 ? UiKit.FontSize(reference) : 13, reference.Bold);
        if (control is Label label) label.AddThemeColorOverride("font_color", reference.Color);
    }
    private void Action(Button button, string field, string text)
    {
        var rect = ClassicIdentityLayout.Field(Window.Id, field);
        if (Window.Id == "namechange")
        {
            // The rename buttons were plain hot spots on the ornamented plate; use the plain-frame service actions.
            ClassicMerchantSkin.Button(button, Plugin.Kit.Layout("co_change_bill_us").Find(field)!, this, rect, text);
            Font(button, 13, true); ClassicServiceAction.Disabled(button);
            button.SetMeta("identity_expected_rect", rect);
            return;
        }
        ClassicMerchantSkin.Button(button, _art.Find(field)!, this, rect, text);
        Font(button, Window.Id == "disguise" ? 13 : 15, true);
        button.AddThemeColorOverride("font_shadow_color", Colors.Black);
        button.AddThemeConstantOverride("shadow_offset_x", 1); button.AddThemeConstantOverride("shadow_offset_y", 1);
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            button.AddThemeColorOverride(state, new Color(Window.Id == "namechange" ? "80ffff" : Window.Id == "disguise" ? "ffddfe" : "f9e9b0"));
        // The light disguise stone needs dark ink once dimmed; the message-frame buttons keep the muted caption.
        ClassicServiceAction.Disabled(button, Window.Id == "disguise" ? new Color("2e2924") : null);
        if (Window.Id == "disguise") button.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        button.SetMeta("identity_expected_rect", rect);
    }
    private void Input(LineEdit input, string field)
    {
        Original(input, field); Font(input, 13);
        input.TextDirection = TextDirection.Ltr; input.Alignment = HorizontalAlignment.Left;
        input.AddThemeColorOverride("font_color", Colors.White);
        input.AddThemeColorOverride("font_placeholder_color", new Color("8e8e89"));
        // The original input's own atlas background remains behind the native text editor.
        foreach (string state in new[] { "normal", "focus", "read_only" })
            input.AddThemeStyleboxOverride(state, new StyleBoxEmpty { ContentMarginLeft = 2, ContentMarginRight = 2 });
        input.CustomMinimumSize = Vector2.Zero;
        input.Size = ClassicIdentityLayout.Field(Window.Id, field).Size;
    }
    private void Clan()
    {
        Original(_title, "Text_title"); _title.Text = "Create a Clan";
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        var message = Native<Label>("clan_create_message");
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll); Place(scroll, ClassicIdentityLayout.Field(Window.Id, "Text_Message")); Scroll(scroll);
        message.Reparent(scroll, false); message.Visible = true; Font(message, 14, true);
        message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        message.CustomMinimumSize = new Vector2(1, 0); message.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        message.VerticalAlignment = VerticalAlignment.Center;
        Input(Native<LineEdit>("clan_create_name"), "Edit_Clan");
        Action(Native<Button>("clan_create_accept"), "btn_yes", "OK");
        Action(Native<Button>("clan_create_cancel"), "btn_no", "Cancel");
    }
    private void Rename()
    {
        var hint = _hint = Native<Label>("name_change_hint"); Original(hint, "text_explanation");
        SyncRename();
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart; hint.ClipText = false;
        var label = new Label { Text = "ID", MouseFilter = MouseFilterEnum.Ignore }; AddChild(label);
        Place(label, ClassicIdentityLayout.RenameId); Font(label, 16, true);
        Input(Native<LineEdit>("name_change_name"), "edit_id");
        var status = Native<Label>("name_change_status"); Place(status, ClassicIdentityLayout.RenameStatus); Font(status, 12);
        status.HorizontalAlignment = HorizontalAlignment.Center;
        Action(Native<Button>("name_change_accept"), "btn_ok", "OK");
        Action(Native<Button>("name_change_cancel"), "btn_cancel", "Cancel");
    }
    private void Disguise()
    {
        var groupRows = Native<VBoxContainer>("disguise_groups");
        var formRows = Native<VBoxContainer>("disguise_forms");
        var groups = (ScrollContainer)groupRows.GetParent();
        _forms = (ScrollContainer)formRows.GetParent();
        Place(groups, Rows("list_group")); Rail(groups, "list_group");
        Place(_forms, Rows("list_monster")); _formsRail = Rail(_forms, "list_monster");
        foreach (var rows in new[] { groupRows, formRows })
        {
            rows.CustomMinimumSize = new Vector2(1, 0); rows.AddThemeConstantOverride("separation", 0);
            Watch(rows);
        }
        _note = Native<Label>("disguise_note");
        Place(_note, new Rect2(17, 468, 141, 40)); Font(_note, 11);
        _note.AutowrapMode = TextServer.AutowrapMode.WordSmart; _note.ClipContents = true;
        _note.CustomMaximumSize = new Vector2(141, 40);
        Action(Native<Button>("disguise_accept"), "btn_ok", "OK");
        Action(Native<Button>("disguise_cancel"), "btn_close", "Close");
    }
    // Whole rows only: the visible list height is trimmed to a multiple of the row, beside the original scroll art.
    private Rect2 Rows(string field, float reserved = 0)
    {
        var list = ClassicIdentityLayout.Field(Window.Id, field);
        var bar = _art.Find(field)!.Children.First(n => n.Type == "scrollbar");
        float height = Mathf.Floor((list.Size.Y - reserved) / DisguiseRow) * DisguiseRow;
        return new Rect2(list.Position, new Vector2(bar.X - _art.X - list.Position.X, height));
    }
    private ClassicIdentityScrollRail Rail(ScrollContainer scroll, string field)
    {
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        var source = _art.Find(field)!.Children.First(n => n.Type == "scrollbar");
        var rail = new ClassicIdentityScrollRail(scroll, source, DisguiseRow) { Name = field + "_rail" };
        AddChild(rail); rail.Position = source.Position - _art.Position; rail.Size = source.SizeVec;
        rail.SetMeta("identity_expected_rect", new Rect2(rail.Position, rail.Size));
        return rail;
    }
    private static void Scroll(ScrollContainer scroll)
    {
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        var bar = scroll.GetVScrollBar();
        var track = new StyleBoxFlat { BgColor = new Color("15120f"), BorderColor = new Color("92785b") };
        track.SetBorderWidthAll(1); bar.AddThemeStyleboxOverride("scroll", track);
        foreach (string state in new[] { "grabber", "grabber_highlight", "grabber_pressed" })
        {
            var grip = ClassicDesign.ButtonBox(state == "grabber_highlight" ? "hover" : state == "grabber_pressed" ? "pressed" : "normal");
            grip.SetContentMarginAll(0); bar.AddThemeStyleboxOverride(state, grip);
        }
        bar.CustomMinimumSize = new Vector2(10, 0); bar.FocusMode = FocusModeEnum.None;
    }
    private static void Watch(Node root)
    {
        void Visit(Node node)
        {
            if (node.HasMeta("identity_watched")) return;
            node.SetMeta("identity_watched", true);
            if (node is Button button)
            {
                Font(button, 13); button.CustomMinimumSize = new Vector2(0, 19);
                button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                button.ClipText = true;
                bool selected = button.GetMeta("identity_selected", false).AsBool();
                foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
                {
                    var box = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = state != "normal" || selected ? new Color("dfc893") : Colors.Transparent };
                    box.SetBorderWidthAll(1); box.SetContentMarginAll(1); button.AddThemeStyleboxOverride(state, box);
                }
                foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color" })
                    button.AddThemeColorOverride(state, selected ? new Color("ffffa0") : new Color("eee3cf"));
            }
            node.ChildEnteredTree += child => Callable.From(() => { if (GodotObject.IsInstanceValid(child)) Visit(child); }).CallDeferred();
            foreach (var child in node.GetChildren()) Visit(child);
        }
        Visit(root);
    }
    public const string RenameHint = "Enter your new name. Requires a Scroll of Identity.\nYour old name will no longer be valid after the change. Choose carefully. Names must have 3-20 characters.";
    public const string ClanRenameHint = "Enter the new name of your clan.\nThe old clan name will no longer be valid after the change. Choose carefully. Names must have 3-20 characters.";
    /// <summary>The client rewrites its hint on every open; the Classic text follows the character or clan mode.</summary>
    private void SyncRename()
    {
        if (_hint == null) return;
        bool clan = NativeIdentity.RenamesClan(Window);
        string hint = clan ? ClanRenameHint : RenameHint, title = clan ? "Change Clan Name" : "Change Name";
        if (_hint.Text != hint) _hint.Text = hint;
        if (_title.Text != title) _title.Text = title;
    }
    public override void _Process(double delta) => Sync();
    private void Sync()
    {
        SyncRename();
        if (_forms == null || _note == null) return;
        bool note = _note.Text.Length > 0; _note.Visible = note;
        _note.TooltipText = _note.Text;
        var bounds = Rows("list_monster", note ? 44 : 0);
        _forms.Size = bounds.Size; _forms.SetMeta("identity_expected_rect", bounds);
        if (_formsRail != null)
        {
            var source = _art.Find("list_monster")!.Children.First(n => n.Type == "scrollbar");
            _formsRail.Size = new Vector2(source.W, source.H - (note ? 44 : 0));
            _formsRail.SetMeta("identity_expected_rect", new Rect2(_formsRail.Position, _formsRail.Size));
        }
        var accept = Native<Button>("disguise_accept");
        accept.Disabled = !Native<VBoxContainer>("disguise_forms").GetChildren().OfType<Button>().Any(b => !b.IsQueuedForDeletion());
    }
    public override void _UnhandledKeyInput(InputEvent ev)
    {
        if (!Window.IsVisibleInTree() || ev is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Escape)
        {
            Native<Button>(Window.Id == "creat_clan" ? "clan_create_cancel" : Window.Id == "namechange" ? "name_change_cancel" : "disguise_cancel")
                .EmitSignal(BaseButton.SignalName.Pressed);
            GetViewport().SetInputAsHandled(); return;
        }
        if (key.Keycode is not (Key.Enter or Key.KpEnter) || GetViewport().GuiGetFocusOwner() is LineEdit) return;
        var accept = Native<Button>(Window.Id == "creat_clan" ? "clan_create_accept" : Window.Id == "namechange" ? "name_change_accept" : "disguise_accept");
        if (!accept.Disabled) accept.EmitSignal(BaseButton.SignalName.Pressed);
        GetViewport().SetInputAsHandled();
    }
    public override void _Draw()
    {
        if (Window.Id == "namechange")
        {
            // Only the original ID bar and the field art remain; the plain frame replaces the quest-frame bands.
            var bar = _art.Children.First(n => n.IsImage && n.SrcY == 379);
            DrawTextureRectRegion(Plugin.Kit.Texture(bar.Texture!), ClassicIdentityLayout.RenameBar, new Rect2(bar.SrcX, bar.SrcY, bar.SrcW, bar.SrcH));
            var edit = _art.Find("edit_id")!.Children.First(n => n.IsImage);
            DrawTextureRectRegion(Plugin.Kit.Texture(edit.Texture!), ClassicIdentityLayout.Field(Window.Id, "edit_id"), new Rect2(edit.SrcX, edit.SrcY, edit.SrcW, edit.SrcH));
            return;
        }
        if (Window.Id == "disguise")
        {
            // The original list boxes without the crest and curls of the outer art.
            var ring = Plugin.Kit.Texture("disguise_ring_us.png")!;
            DrawTextureRectRegion(ring, ClassicIdentityLayout.DisguiseGroupBox, new Rect2(15, 59, 148, 205));
            var form = ClassicIdentityLayout.DisguiseFormBox;
            DrawTextureRectRegion(ring, new Rect2(form.Position, new Vector2(form.Size.X, 243)), new Rect2(15, 269, 148, 243));
            DrawTextureRectRegion(ring, new Rect2(form.Position.X, form.Position.Y + 243, form.Size.X, 1), new Rect2(192, 2, 148, 1));
            return;
        }
        var artworkBounds = _art.Children.Where(n => n.IsImage)
            .Select(n => new Rect2(n.Position - _art.Position, n.SizeVec)).Aggregate((a, b) => a.Merge(b));
        DrawRect(artworkBounds.Grow(-4), Colors.Black);
        foreach (var piece in _art.Children.Where(n => n.IsImage))
            DrawTextureRectRegion(Plugin.Kit.Texture(piece.Texture!), new Rect2(piece.Position - _art.Position, piece.SizeVec), new Rect2(piece.SrcX, piece.SrcY, piece.SrcW, piece.SrcH));
        foreach (var field in _art.All().Where(n => n.Type == "edit"))
            foreach (var piece in field.Children.Where(n => n.IsImage))
                DrawTextureRectRegion(Plugin.Kit.Texture(piece.Texture!), new Rect2(piece.Position - _art.Position, piece.SizeVec), new Rect2(piece.SrcX, piece.SrcY, piece.SrcW, piece.SrcH));
    }
}

/// <summary>Original co_disguisering arrows and track bound to a native list; it scrolls by whole rows.</summary>
public partial class ClassicIdentityScrollRail : Control
{
    private readonly ScrollContainer _scroll;
    private readonly TextureButton _up, _down;
    private readonly ClassicScrollTrack _track;
    private readonly Vector2 _upSize, _downSize;
    private readonly float _row;
    public VScrollBar Bar => _scroll.GetVScrollBar();
    public ClassicIdentityScrollRail(ScrollContainer scroll, LayoutNode source, float row)
    {
        _scroll = scroll; _row = row; MouseFilter = MouseFilterEnum.Stop;
        var arrows = source.Children.Where(n => n.IsButton).OrderBy(n => n.Y).ToArray();
        _up = Arrow(arrows[0]); _down = Arrow(arrows[1]);
        _upSize = arrows[0].SizeVec; _downSize = arrows[1].SizeVec;
        _up.Pressed += () => Bar.Value -= _row; _down.Pressed += () => Bar.Value += _row;
        _track = new ClassicScrollTrack(Bar, source.Children.First(n => n.Type == "trackbar"), 16, false, row);
        AddChild(_up); AddChild(_down); AddChild(_track);
        Resized += Arrange;
        ShownOnly.Gate(this, Sync);
    }
    private static TextureButton Arrow(LayoutNode source) => new()
    {
        TextureNormal = ClassicChatControls.Atlas(source.Images.First(n => n.Tag == 0)),
        TextureDisabled = ClassicChatControls.Atlas(source.Images.First(n => n.Tag == 0)),
        TexturePressed = ClassicChatControls.Atlas(source.Images.First(n => n.Tag == 1)),
        TextureHover = ClassicChatControls.Atlas(source.Images.First(n => n.Tag == 2)),
        FocusMode = FocusModeEnum.None, IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.Scale,
    };
    private void Arrange()
    {
        _up.Position = Vector2.Zero; _up.Size = _upSize;
        _down.Position = new Vector2(0, Size.Y - _downSize.Y); _down.Size = _downSize;
        _track.Position = new Vector2(0, _upSize.Y); _track.Size = new Vector2(Size.X, Size.Y - _upSize.Y - _downSize.Y);
    }
    public override void _Ready() => Arrange();
    public override void _Process(double delta) => Sync();
    private void Sync()
    {
        if (_track.Size.Y != Size.Y - _upSize.Y - _downSize.Y) Arrange();
        double snapped = Math.Round(Bar.Value / _row) * _row;
        if (!Input.IsMouseButtonPressed(MouseButton.Left) && Math.Abs(Bar.Value - snapped) > .01) Bar.Value = snapped;
        _up.Disabled = Bar.Value <= Bar.MinValue;
        _down.Disabled = Bar.Value >= Math.Max(Bar.MinValue, Bar.MaxValue - Bar.Page);
        _up.SelfModulate = _up.Disabled ? new Color(1, 1, 1, .7f) : Colors.White;
        _down.SelfModulate = _down.Disabled ? new Color(1, 1, 1, .7f) : Colors.White;
        _track.QueueRedraw();
    }
}
