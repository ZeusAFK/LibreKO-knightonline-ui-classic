using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>Original party artwork with live LibreKO controls and callbacks: the party board window.</summary>
public static class ClassicPartySkin
{
    public static void Extend(Control body) => body.Ready += () => Callable.From(() => Apply(body)).CallDeferred();
    public static Control? Apply(Control body)
    {
        Node? owner = body;
        while (owner != null && owner is not HudWindow) owner = owner.GetParent();
        if (owner is not HudWindow window || window.HasMeta("classic_party")) return null;
        window.SetMeta("classic_party", true);
        var grip = window.Header!.GetChildren().OfType<HBoxContainer>().First();
        var close = grip.GetChildren().OfType<Button>().Single(b => b.TooltipText == "Close");
        foreach (var child in window.GetChildren().OfType<Control>()) child.Visible = false;
        window.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        window.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        Control panel = new ClassicPartyBoard(body, grip, close);
        window.AddChild(panel); window.ResetSize();
        return panel;
    }
    internal static Label Caption(Control parent, string text, Rect2 rect, Color? color = null, int size = 12)
    {
        var label = new Label { Text = text, Position = rect.Position, Size = rect.Size, ClipText = true,
            VerticalAlignment = VerticalAlignment.Center, ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", Plugin.Kit.ChatStrong);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? new Color("ffff80"));
        parent.AddChild(label);
        Callable.From(() => label.Size = rect.Size).CallDeferred();
        return label;
    }
    internal static void Drag(Control parent, Control native, Rect2 rect)
    {
        var grip = new Control { Name = "party_drag", Position = rect.Position, Size = rect.Size, MouseDefaultCursorShape = Control.CursorShape.Move };
        parent.AddChild(grip);
        grip.GuiInput += ev => native.EmitSignal(Control.SignalName.GuiInput, ev);
    }
    internal static Button Place(Control parent, Button button, LayoutNode art, string? caption = null)
    {
        ClassicChatControls.SkinButton(button, art);
        button.Icon = null; button.CustomMinimumSize = Vector2.Zero;
        button.AddThemeFontOverride("font", Plugin.Kit.Bold); button.AddThemeFontSizeOverride("font_size", 10);
        var ink = Plugin.Kit.Nation == 1 ? Colors.Black : new Color("ffff80");
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color" }) button.AddThemeColorOverride(state, ink);
        KarusButtonContrast(button);
        if (caption != null) button.Text = caption;
        ClassicVendorSkin.Move(button, parent, new Rect2(art.Position, art.SizeVec));
        button.SetMeta("party_expected_rect", new Rect2(art.Position, art.SizeVec));
        return button;
    }
    internal static void KarusButtonContrast(Button button)
    {
        if (Plugin.Kit.Nation != 1) return;
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color", "font_disabled_color" })
            button.AddThemeColorOverride(state, new Color("17130f"));
        button.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        button.AddThemeConstantOverride("outline_size", 0);
        // Keep inactive captions readable; distinguish state with the original artwork's brightness.
        if (button.GetThemeStylebox("normal") is StyleBoxTexture normal)
        {
            var disabled = (StyleBoxTexture)normal.Duplicate();
            disabled.ModulateColor = new Color(.72f, .72f, .72f, 1);
            button.AddThemeStyleboxOverride("disabled", disabled);
        }
    }
}

public partial class ClassicPartyBoard : Control
{
    private readonly LayoutNode _art;
    private readonly VBoxContainer _list;
    private readonly Label _details;
    private readonly Button _invite, _private;
    private readonly Button _register, _remove;
    private readonly Label _page;
    private readonly Control _recruit = new() { Name = "seek_recruit" }, _filters = new() { Name = "seek_filter_panel" };
    private readonly Label _title;
    private readonly Label? _heading;
    private readonly Button _inviteByName;
    private Control? _selected;
    public ClassicPartyBoard(Control body, Control grip, Button close)
    {
        Name = "classic_party_board"; CustomMinimumSize = new Vector2(320, 481); Size = CustomMinimumSize;
        _art = Plugin.Kit.Layout("{nation}_partyboard_us"); TextureFilter = TextureFilterEnum.Nearest;
        ClassicPartySkin.Drag(this, grip, new Rect2(8, 3, 284, 40));
        var title = _art.Strings.Last(n=>n.Text=="Party Window");
        _title = ClassicPartySkin.Caption(this, "Party Window", new Rect2(title.Position,title.SizeVec),title.Color,title.Size);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        foreach (var item in new[] { ("ID",14,112), ("Level",131,61), ("Job",196,110) })
            ClassicPartySkin.Caption(this, item.Item1, new Rect2(item.Item2, 52, item.Item3, 22),title.Color,11).HorizontalAlignment = HorizontalAlignment.Center;
        var nodes = CharacterDetailsSkin.Tree(body).ToArray();
        Button Native(string text) => nodes.OfType<Button>().Single(b => b.Text == text);
        ClassicPartySkin.Place(this, close, _art.Find("btn_exit")!, "");
        ClassicPartySkin.Place(this, Native("Refresh"), _art.Find("btn_refresh")!);
        ClassicPartySkin.Place(this, Native("◄ Prev"), _art.Find("btn_page_up")!, "");
        ClassicPartySkin.Place(this, Native("Next ►"), _art.Find("btn_page_down")!, "");
        var register = Native("Look for a party"); _register = register;
        // Keep the server-driven text state on the native control; use the original short caption above it.
        register.Text = "Register"; register.ClipText = true;
        ClassicPartySkin.Place(this, register, _art.Find("btn_add")!);
        var remove = new Button { Text = "Delete", FocusMode = FocusModeEnum.None }; _remove = remove;
        AddChild(remove); ClassicPartySkin.Place(this, remove, _art.Find("btn_delete")!);
        remove.Pressed += () => { if (register.HasMeta("seeking") && register.GetMeta("seeking").AsBool()) register.EmitSignal(BaseButton.SignalName.Pressed); };
        register.TooltipText = "Register or remove your seek-party listing";
        _invite = new Button { Text = "Invite", FocusMode = FocusModeEnum.None, TooltipText = "Invite the selected player, or a player by name" }; AddChild(_invite);
        _private = new Button { Text = "Private", Disabled = true, FocusMode = FocusModeEnum.None }; AddChild(_private);
        ClassicPartySkin.Place(this, _invite, _art.Find("btn_party")!); ClassicPartySkin.Place(this, _private, _art.Find("btn_whisper")!);
        _inviteByName = nodes.OfType<Button>().Single(b => b.Name == "seek_invite_name");
        _invite.Pressed += () => { if (Selected) Forward("Invite"); else _inviteByName.EmitSignal(BaseButton.SignalName.Pressed); };
        _private.Pressed += () => Forward("Private");
        var page = nodes.OfType<Label>().Single(l => l.Text == "Page 1"); _page = page;
        ClassicVendorSkin.Font(page,10); page.HorizontalAlignment = HorizontalAlignment.Center;
        ClassicVendorSkin.Move(page, this, new Rect2(131,383, 62, 28));
        _details = ClassicPartySkin.Caption(this, "Select a player to invite or send a private message.", new Rect2(18, 283, 284, 84), new Color("ffff80"), 11);
        _details.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _details.VerticalAlignment = VerticalAlignment.Top;
        _list = nodes.OfType<VBoxContainer>().Single(n => n.Name == "seek_members");
        _list.Reparent(this); _list.Position = new Vector2(14,79); _list.CustomMinimumSize = new Vector2(292,0);
        _list.AddThemeConstantOverride("separation",0); _list.Size = new Vector2(292,180);
        _list.ChildEnteredTree += _ => Callable.From(Refresh).CallDeferred();
        _list.ChildExitingTree += _ => Callable.From(Refresh).CallDeferred();
        // Recruiting is a LibreKO extension, kept inside the unused lower table area.
        var opt = nodes.OfType<OptionButton>().Single(n => n.Name == "seek_wanted_class"); var msg = nodes.OfType<LineEdit>().Single(n => n.Name == "seek_wanted_message"); var post = Native("Post");
        _recruit.Position = new Vector2(18,283); _recruit.Size = new Vector2(284,84); _recruit.Visible = false; AddChild(_recruit);
        ClassicPartySkin.Caption(_recruit, "Recruit (party leader only)", new Rect2(0,0,284,18),new Color("ffff80"),11);
        ClassicPartySkin.Caption(_recruit, "Class", new Rect2(0,23,72,18),new Color("ffff80"),10);
        ClassicVendorSkin.Input(msg); ClassicVendorSkin.Font(opt,10); ClassicVendorSkin.Button(post, _art.Find("btn_party"));
        ClassicChatControls.SkinButton(opt, _art.Find("btn_party")!);
        ClassicPartySkin.KarusButtonContrast(opt); ClassicPartySkin.KarusButtonContrast(post);
        var inputStyle=ClassicDesign.InputBox(); inputStyle.SetContentMarginAll(1);
        msg.AddThemeStyleboxOverride("normal", inputStyle); msg.AddThemeStyleboxOverride("focus", inputStyle);
        ClassicVendorSkin.Move(opt,_recruit,new Rect2(76,23,134,18));
        ClassicVendorSkin.Move(msg,_recruit,new Rect2(0,50,210,22));
        ClassicVendorSkin.Move(post,_recruit,new Rect2(218,50,66,22));
        var recruitToggle=new Button { Text="Recruit",FocusMode=FocusModeEnum.None }; AddChild(recruitToggle);
        ClassicPartySkin.Place(this,recruitToggle,_art.Find("btn_party")!);
        recruitToggle.Position=new Vector2(238,387); recruitToggle.Size=new Vector2(68,18);
        recruitToggle.SetMeta("party_expected_rect",new Rect2(new Vector2(238,387),_art.Find("btn_party")!.SizeVec));
        recruitToggle.Pressed+=()=>Lower(_recruit.Visible?null:_recruit);
        // The board filters and the Full Moon Rift actions share the lower table area behind a second toggle.
        _filters.Position = new Vector2(18,283); _filters.Size = new Vector2(284,84); _filters.Visible = false; AddChild(_filters);
        ClassicPartySkin.Caption(_filters, "Filter", new Rect2(0,0,284,18),new Color("ffff80"),11);
        foreach (var (row, rect) in new[] { (nodes.OfType<HBoxContainer>().Single(n => n.Name == "seek_filters"), new Rect2(0,23,284,20)), (nodes.OfType<HBoxContainer>().Single(n => n.Name == "seek_full_moon"), new Rect2(0,50,284,20)) })
        {
            bool shown = row.Visible;
            ClassicVendorSkin.Move(row, _filters, rect); row.Visible = shown; row.AddThemeConstantOverride("separation", 8);
            foreach (var control in row.GetChildren().OfType<Button>())
            {
                control.CustomMinimumSize = new Vector2(0, 18); control.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                if (control is CheckBox check)
                {
                    ClassicVendorSkin.Font(check, 10);
                    foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" }) check.AddThemeColorOverride(state, new Color("ffff80"));
                    continue;
                }
                ClassicChatControls.SkinButton(control, _art.Find("btn_party")!); ClassicVendorSkin.Font(control, 10);
                control.CustomMinimumSize = new Vector2(Math.Max(68, control.CustomMinimumSize.X), 18);
                foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color" }) control.AddThemeColorOverride(state, Plugin.Kit.Nation == 1 ? Colors.Black : new Color("ffff80"));
                ClassicPartySkin.KarusButtonContrast(control);
                if (control is OptionButton option) ClassicSocialStyle.Menu(option.GetPopup());
            }
        }
        var filterToggle=new Button { Text="Filter",FocusMode=FocusModeEnum.None,Name="seek_filter_toggle" }; AddChild(filterToggle);
        ClassicPartySkin.Place(this,filterToggle,_art.Find("btn_party")!);
        filterToggle.Position=new Vector2(14,387); filterToggle.Size=new Vector2(68,18);
        filterToggle.SetMeta("party_expected_rect",new Rect2(new Vector2(14,387),_art.Find("btn_party")!.SizeVec));
        filterToggle.Pressed+=()=>Lower(_filters.Visible?null:_filters);
        _heading = nodes.OfType<Label>().SingleOrDefault(n => n.Name == "seek_heading");
        Resized += () => Size = CustomMinimumSize;
    }
    public override void _Ready() => Refresh();
    private bool Selected => _selected != null && GodotObject.IsInstanceValid(_selected) && _selected.IsInsideTree();
    /// <summary>Shows one lower panel (recruit or filter) instead of the selected player's details, or the details again.</summary>
    private void Lower(Control? panel)
    {
        _recruit.Visible = panel == _recruit; _filters.Visible = panel == _filters; _details.Visible = panel == null;
    }
    public override void _Process(double delta)
    {
        // The Full Moon Rift board names itself; the normal board keeps the original title.
        string heading = _heading != null && _heading.Visible && _heading.Text.Length > 0 ? _heading.Text : "Party Window";
        if (_title.Text != heading) _title.Text = heading;
        bool seeking = _register.HasMeta("seeking") && _register.GetMeta("seeking").AsBool();
        _register.Text = "Register"; _register.Disabled = seeking; _remove.Disabled = !seeking;
        if (_page.HasMeta("page_caption")) _page.Text = _page.GetMeta("page_caption").AsString();
    }
    private void Forward(string text)
    {
        if (_selected == null || !GodotObject.IsInstanceValid(_selected) || !_selected.IsInsideTree()) return;
        CharacterDetailsSkin.Tree(_selected).OfType<Button>().Single(b => b.Text == text).EmitSignal(BaseButton.SignalName.Pressed);
    }
    private void Refresh()
    {
        if (!IsInsideTree()) return;
        if (_selected != null && (!GodotObject.IsInstanceValid(_selected) || _selected.GetParent() != _list))
        { _selected = null; _private.Disabled = true; _details.Text = "Select a player to invite or send a private message."; }
        // An empty page keeps the client's own reason (nobody listed, or nobody matching the filters).
        if (_selected == null) _details.Text = _list.GetChildren().OfType<Control>().Any(c=>c.HasMeta("seek_name"))
            ? "Select a player to invite or send a private message."
            : _list.GetChildren().OfType<Label>().FirstOrDefault(l => !l.IsQueuedForDeletion())?.Text ?? "Nobody is seeking a party right now.";
        foreach (var row in _list.GetChildren().OfType<Control>())
        {
            if (!row.HasMeta("seek_name"))
            {
                if (row is Label empty) empty.Visible = false;
                continue;
            }
            if (row.HasMeta("classic_seek_row")) continue;
            row.SetMeta("classic_seek_row",true);
            foreach (var child in row.GetChildren().OfType<Control>()) child.Visible = false;
            if(row is PanelContainer panel) panel.AddThemeStyleboxOverride("panel",new StyleBoxEmpty());
            var content = new Control { CustomMinimumSize = new Vector2(292,18), MouseFilter = MouseFilterEnum.Ignore };
            row.AddChild(content); row.CustomMinimumSize = new Vector2(292,18);
            // Cells keep the same 4px inset from the column rules as the header captions' table.
            var name = ClassicPartySkin.Caption(content,row.GetMeta("seek_name").AsString(),new Rect2(4,0,111,18),Colors.White,10);
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            ClassicPartySkin.Caption(content,row.GetMeta("seek_level").AsString(),new Rect2(118,0,60,18),Colors.White,10).HorizontalAlignment=HorizontalAlignment.Center;
            ClassicPartySkin.Caption(content,row.GetMeta("seek_class").AsString(),new Rect2(186,0,106,18),Colors.White,10);
            row.TooltipText=row.GetMeta("seek_name").AsString()+"\n"+row.GetMeta("seek_detail").AsString();
            row.GuiInput += ev => { if(ev is InputEventMouseButton {Pressed:true,ButtonIndex:MouseButton.Left})
            { _selected=row; _private.Disabled=false; _details.Text=row.TooltipText; QueueRedraw(); } };
        }
        QueueRedraw();
    }
    public override void _Draw()
    {
        var image=_art.Images.First();
        DrawTextureRectRegion(Plugin.Kit.Texture(image.Texture!),new Rect2(Vector2.Zero,CustomMinimumSize),new Rect2(image.SrcX,image.SrcY,image.SrcW,image.SrcH));
        // The server supplies ten rows per page. Use the remainder for selected-player details.
        // Cover the table's column rules down to the inner frame line.
        DrawRect(new Rect2(15,276,290,99),Colors.Black);
        DrawLine(new Vector2(15,276),new Vector2(305,276),new Color("8c7950"),1);
        if(_selected != null && GodotObject.IsInstanceValid(_selected) && _selected.GetParent()==_list)
            DrawRect(new Rect2(_list.Position+_selected.Position,new Vector2(292,18)),new Color("00ff00"),false,1);
    }
}

