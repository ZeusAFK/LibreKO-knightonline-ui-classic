using Godot;
using LibreKO;
using LibreKO.Plugins;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Native;
using Spec = KnightOnlineUiClassic.Layout.ClassicChatRoomLayout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The Classic chat room (the client's group-chat style room window of 3277df5). The client keeps its room:
/// posts, voice notes, the voice call, the roster and every callback stay native. The plugin frames it in the
/// plain Classic frame through the client's frame seam and dresses it with the whisper and messenger pieces:
/// black fields with the gold line, Classic list rows, the Messenger action buttons, item cells for the icon
/// actions, Arial on the Classic inks, the original whisper minimize button and, minimized, the compact whisper
/// bar with its attention blink.
/// </summary>
public static class ClassicChatRoomSkin
{
    public const string WindowId = "chat_room";
    public const string PostMeta = "classic_room_post", RosterMeta = "classic_room_row", CellMeta = "classic_room_cell";

    public static Color TheirBubble => new("141310");
    public static Color MineBubble => ClassicDarkFrame.SelectedRow;
    public static Color MineEdge => ClassicDarkFrame.Line;
    public static Color TheirEdge => ClassicSocialStyle.RowRule;
    public static Color NoticeInk => new("ffe24a");
    public static Color Speaking => new("7fd98b");

    public static void Register(PluginUi ui) => ui.FrameWindow(WindowId, frame => new ClassicChatRoomFrame(frame));

    /// <summary>The room's popups (room settings, voice settings, a member's volume) on the Classic menu panel.</summary>
    public static void StylePopup(PopupPanel popup)
    {
        if (popup is EmoticonPicker) { ClassicWhisperSkin.Popup(popup); return; }
        if (popup.HasMeta("classic_popup")) return;
        popup.SetMeta("classic_popup", true);
        var panel = ClassicDarkFrame.InputBox();
        panel.BgColor = new Color(0, 0, 0, .96f);
        panel.ContentMarginLeft = panel.ContentMarginRight = 10;
        panel.ContentMarginTop = panel.ContentMarginBottom = 8;
        popup.AddThemeStyleboxOverride("panel", panel);
        foreach (var node in ClassicSocialStyle.Descendants(popup))
        {
            if (node is HSlider slider) Slider(slider);
            else if (node is ProgressBar meter) Meter(meter);
        }
        ClassicSocialStyle.Apply(popup);
    }

    /// <summary>An icon action in the original item cell; the client keeps its icon and the icon's state tint.</summary>
    public static void Cell(Button button)
    {
        button.SetMeta(CellMeta, true);
        button.SetMeta(ClassicSocialStyle.Styled, true);
        var cell = ClassicSkin.PanelStyle();
        cell.SetContentMarginAll(0);
        StyleBoxTexture Tinted(Color tint) { var box = (StyleBoxTexture)cell.Duplicate(); box.ModulateColor = tint; return box; }
        button.AddThemeStyleboxOverride("normal", cell);
        button.AddThemeStyleboxOverride("hover", Tinted(new Color(1.25f, 1.2f, 1.05f)));
        button.AddThemeStyleboxOverride("pressed", Tinted(new Color(.75f, .75f, .75f)));
        button.AddThemeStyleboxOverride("hover_pressed", Tinted(new Color(.75f, .75f, .75f)));
        button.AddThemeStyleboxOverride("disabled", Tinted(new Color(.55f, .55f, .55f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeConstantOverride("icon_max_width", 14);
        button.CustomMinimumSize = new Vector2(Spec.Cell, Spec.Cell);
        button.FocusMode = Control.FocusModeEnum.None;
    }

    /// <summary>A volume or threshold slider: the dark-frame track with the gold line and a gold fill.</summary>
    public static void Slider(HSlider slider)
    {
        var track = new StyleBoxFlat { BgColor = new Color("171715"), BorderColor = new Color("bfa777") };
        track.SetBorderWidthAll(1);
        track.ContentMarginTop = track.ContentMarginBottom = 2;
        slider.AddThemeStyleboxOverride("slider", track);
        slider.AddThemeStyleboxOverride("grabber_area", new StyleBoxFlat { BgColor = ClassicDarkFrame.Line, ContentMarginTop = 2, ContentMarginBottom = 2 });
        slider.AddThemeStyleboxOverride("grabber_area_highlight", new StyleBoxFlat { BgColor = ClassicDarkFrame.Heading, ContentMarginTop = 2, ContentMarginBottom = 2 });
        slider.FocusMode = Control.FocusModeEnum.None;
        slider.AddThemeIconOverride("grabber", Knob(ClassicDarkFrame.Heading));
        slider.AddThemeIconOverride("grabber_highlight", Knob(ClassicDarkFrame.Value));
        slider.AddThemeIconOverride("grabber_disabled", Knob(ClassicDesign.Muted));
    }

    private static readonly Dictionary<Color, ImageTexture> _knobs = new();

    /// <summary>A small gold slider knob with the dark outline of the Classic buttons, instead of the client's round one.</summary>
    private static ImageTexture Knob(Color fill)
    {
        if (_knobs.TryGetValue(fill, out var cached) && GodotObject.IsInstanceValid(cached)) return cached;
        var image = Image.CreateEmpty(8, 14, false, Image.Format.Rgba8);
        image.Fill(new Color("2b2214"));
        image.FillRect(new Rect2I(1, 1, 6, 12), fill);
        image.FillRect(new Rect2I(1, 1, 6, 1), fill.Lightened(.35f));
        return _knobs[fill] = ImageTexture.CreateFromImage(image);
    }

    /// <summary>A level meter: the dark track with the gold line; the fill keeps the client's tint.</summary>
    public static void Meter(ProgressBar meter)
    {
        var track = new StyleBoxFlat { BgColor = new Color("171715"), BorderColor = ClassicDarkFrame.Line };
        track.SetBorderWidthAll(1);
        meter.AddThemeStyleboxOverride("background", track);
        var tint = meter.GetThemeStylebox("fill") is StyleBoxFlat native ? native.BgColor : ClassicDarkFrame.Heading;
        meter.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = tint });
        meter.CustomMinimumSize = new Vector2(meter.CustomMinimumSize.X, 8);
    }

    /// <summary>
    /// One timeline entry: a post keeps its side (own lines right) and its sender colour on a flat Classic field;
    /// day markers and notices stay centred, notices in the Classic notice yellow.
    /// </summary>
    public static void StylePost(Node post)
    {
        if (post.HasMeta(PostMeta)) return;
        post.SetMeta(PostMeta, true);
        foreach (var panel in ClassicSocialStyle.Descendants(post).OfType<PanelContainer>().ToArray())
        {
            if (panel.GetChildCount() > 0 && panel.GetChild(0) is Label chip)
            {
                bool notice = post is CenterContainer;
                var box = new StyleBoxFlat { BgColor = notice ? Colors.Transparent : new Color(0, 0, 0, .7f), BorderColor = ClassicDarkFrame.Line };
                if (!notice) box.SetBorderWidthAll(1);
                box.ContentMarginLeft = box.ContentMarginRight = 8;
                box.ContentMarginTop = box.ContentMarginBottom = 2;
                panel.AddThemeStyleboxOverride("panel", box);
                panel.SetMeta("room_chip", notice ? "notice" : "day");
                ClassicSocialStyle.Font(chip, true, notice ? Spec.NoticeSize : Spec.SmallSize);
                chip.AddThemeColorOverride("font_color", notice ? NoticeInk : ClassicDesign.Muted);
                continue;
            }
            if (panel.GetParent() is not HBoxContainer row) continue;
            bool mine = row.Alignment == BoxContainer.AlignmentMode.End;
            var bubble = new StyleBoxFlat { BgColor = mine ? MineBubble : TheirBubble, BorderColor = mine ? MineEdge : TheirEdge };
            bubble.SetBorderWidthAll(1);
            bubble.ContentMarginLeft = bubble.ContentMarginRight = 8;
            bubble.ContentMarginTop = 4;
            bubble.ContentMarginBottom = 3;
            panel.AddThemeStyleboxOverride("panel", bubble);
            panel.SetMeta("room_bubble", mine ? "mine" : "theirs");
            foreach (var label in ClassicSocialStyle.Descendants(panel).OfType<Label>())
            {
                // The sender's name is the clickable label (player menu); it keeps the client's colour per sender.
                if (label.MouseFilter == Control.MouseFilterEnum.Stop)
                {
                    ClassicSocialStyle.Font(label, true, Spec.FontSize);
                    label.Name = "room_post_name";
                    continue;
                }
                ClassicSocialStyle.Font(label, false, Spec.SmallSize);
                label.AddThemeColorOverride("font_color", ClassicDesign.Muted);
            }
            foreach (var text in ClassicSocialStyle.Descendants(panel).OfType<RichTextLabel>())
            {
                text.AddThemeFontOverride("normal_font", Plugin.Kit.ChatStrong);
                text.AddThemeFontSizeOverride("normal_font_size", Spec.LineSize);
                text.Name = "room_post_text";
            }
            foreach (var wave in ClassicSocialStyle.Descendants(panel).OfType<VoiceWaveformView>())
            {
                wave.Played = mine ? ClassicDarkFrame.Value : ClassicDarkFrame.Heading;
                wave.Rest = new Color(ClassicDesign.Muted, .45f);
                wave.Dot = ClassicDarkFrame.Heading;
                wave.Name = "room_post_wave";
                wave.QueueRedraw();
            }
            foreach (var play in ClassicSocialStyle.Descendants(panel).OfType<Button>()) Cell(play);
        }
    }
}

/// <summary>
/// Plain Classic frame of the chat room: gold title, the room's door and channel-list actions in item cells,
/// the original whisper minimize button and the round close on the title row, the room 16 px from the sides and
/// the bottom. Minimized it is the compact whisper bar (title, restore, close), which blinks for unread lines.
/// </summary>
public partial class ClassicChatRoomFrame : Container
{
    private readonly WindowFrame? _frame;
    private readonly Control _content = null!, _grip = null!;
    private readonly ClassicFrame _art = null!;
    private readonly Label _title = null!, _compactTitle = null!;
    private readonly Button _close = null!;
    private readonly TextureButton _minimize = null!;
    private readonly LayoutView _compact = null!;
    private readonly ClassicWhisperAttention _attention = null!;
    private readonly List<Button> _actions = new();
    private HudWindow? _window;
    private bool _bound, _watching;

    public ClassicChatRoomFrame() { }

    public ClassicChatRoomFrame(WindowFrame frame)
    {
        _frame = frame;
        Name = "classic_chat_room"; MouseFilter = MouseFilterEnum.Stop; TextureFilter = TextureFilterEnum.Nearest;
        _art = new ClassicFrame { Name = "room_frame", BackgroundAlpha = 1, BackgroundColor = Colors.Black };
        AddChild(_art);
        _grip = new Control { Name = "room_drag", MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.Move };
        AddChild(_grip);
        frame.SetDragHandle(_grip);
        _title = new Label { Name = "room_title", Text = frame.Title, MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        _title.ClipText = true; _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        ClassicSocialStyle.Font(_title, true, Spec.HeadingSize);
        _title.AddThemeColorOverride("font_color", ClassicDesign.FrameTitle);
        AddChild(_title);

        var open = Plugin.Kit.Layout("co_whisper_open_us");
        _minimize = ArtButton(open.Find("btn_hide")!, "room_minimize", "Minimize");
        _minimize.Pressed += () => _window?.SetMinimized(true);
        AddChild(_minimize);
        _close = new Button { Name = "room_close", TooltipText = "Close", FocusMode = FocusModeEnum.None };
        ClassicVendorSkin.Button(_close, Plugin.Kit.Layout("co_questtalk_us").Find("btn_close")!);
        _close.Text = "";
        _close.Pressed += () => _frame?.Close();
        AddChild(_close);

        _content = frame.Content;
        _content.GetParent()?.RemoveChild(_content);
        foreach (var side in new[] { "left", "top", "right", "bottom" }) _content.AddThemeConstantOverride("margin_" + side, 0);
        AddChild(_content);

        // Minimized: the original compact whisper bar with its own restore and close cells.
        var closed = Plugin.Kit.Layout("co_whisper_close_us");
        _compact = new LayoutView(Plugin.Kit, closed) { Name = "room_compact", Visible = false };
        AddChild(_compact);
        _compactTitle = _compact.Get<Label>("exit_id")!;
        _compactTitle.Name = "room_compact_title";
        _compactTitle.Text = frame.Title;
        _compactTitle.AddThemeFontOverride("font", Plugin.Kit.Bold);
        _compactTitle.AddThemeFontSizeOverride("font_size", UiKit.FontSize(closed.Find("exit_id")!));
        _compactTitle.VerticalAlignment = VerticalAlignment.Center;
        _compactTitle.ClipText = true; _compactTitle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _compactTitle.Position = new Vector2(6, 2); _compactTitle.Size = new Vector2(94, 20);
        _compactTitle.AddThemeColorOverride("font_outline_color", Colors.Black);
        _compactTitle.AddThemeConstantOverride("outline_size", 1);
        var bar = (TextureButton)_compact.Get("btn_bar")!;
        bar.Size = new Vector2(101, 15);
        AtlasTexture? Crop(Texture2D? texture) => texture is AtlasTexture atlas
            ? new AtlasTexture { Atlas = atlas.Atlas, Region = new Rect2(atlas.Region.Position, new Vector2(101, 15)), FilterClip = true } : null;
        bar.TextureNormal = Crop(bar.TextureNormal); bar.TextureHover = Crop(bar.TextureHover);
        bar.TexturePressed = Crop(bar.TexturePressed); bar.TextureDisabled = Crop(bar.TextureDisabled);
        bar.MouseDefaultCursorShape = CursorShape.Move;
        bar.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { ButtonIndex: MouseButton.Left } press) return;
            _grip.EmitSignal(Control.SignalName.GuiInput, press);
            bar.AcceptEvent();
        };
        var restore = _compact.Get("btn_open")!;
        restore.TooltipText = "Restore"; restore.Position = new Vector2(104, 3);
        _compact.OnPressed("btn_open", () => _window?.SetMinimized(false));
        var compactClose = ArtButton(open.Find("btn_close")!, "room_compact_close", "Close");
        compactClose.Position = new Vector2(124, 3); compactClose.Size = new Vector2(19, 15);
        compactClose.Pressed += () => _frame?.Close();
        _compact.AddChild(compactClose);
        _attention = new ClassicWhisperAttention(bar, _compactTitle) { Name = "room_attention" };
        AddChild(_attention);

        frame.TitleChanged += title =>
        {
            if (!IsInstanceValid(_title)) return;
            _title.Text = title; _compactTitle.Text = title; _compactTitle.TooltipText = title;
        };
        foreach (var part in GetChildren().OfType<Control>()) part.SizeFlagsHorizontal = part.SizeFlagsVertical = SizeFlags.Fill;
    }

    public Control Content => _content;
    public bool Minimized => _window?.Minimized == true;
    public bool Unread => _attention.HasMeta("unread") && _attention.GetMeta("unread").AsBool();
    public static Vector2 CompactSize => Plugin.Kit.Layout("co_whisper_close_us").SizeVec;
    public IReadOnlyList<Button> HeaderActions => _actions;

    public static ClassicChatRoomFrame? Of(HudWindow window) => window.GetChildren().OfType<ClassicChatRoomFrame>().FirstOrDefault();

    private static TextureButton ArtButton(LayoutNode source, string name, string tooltip)
    {
        Texture2D? State(int tag) => source.Images.FirstOrDefault(i => i.Tag == tag) is { } image ? ClassicChatControls.Atlas(image) : null;
        return new TextureButton
        {
            Name = name, TooltipText = tooltip, FocusMode = FocusModeEnum.None, IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.Scale,
            TextureNormal = State(0), TexturePressed = State(1), TextureHover = State(2), TextureDisabled = State(0),
        };
    }

    public override void _Notification(int what)
    {
        if (what == NotificationParented && _window == null && GetParent() is HudWindow window)
        {
            _window = window;
            window.MinimizedChanged += _ => Refresh();
            window.AttentionStyler = unread => { _attention.SetUnread(unread); _attention.SetMeta("unread", unread); };
            Refresh();
        }
        if (what != NotificationSortChildren || _content == null) return;
        Arrange();
    }

    public override Vector2 _GetMinimumSize()
    {
        if (_content == null) return Vector2.Zero;
        if (Minimized) return CompactSize;
        var content = _content.GetCombinedMinimumSize();
        return new Vector2(Mathf.Ceil(content.X + Spec.Inset * 2), Mathf.Ceil(content.Y + Spec.ContentTop + Spec.Inset));
    }

    private void Refresh()
    {
        bool minimized = Minimized;
        foreach (var part in new Control[] { _art, _grip, _title, _minimize, _close }) part.Visible = !minimized;
        _compact.Visible = minimized;
        if (!minimized) { _attention.SetUnread(false); _attention.SetMeta("unread", false); }
        UpdateMinimumSize();
        QueueSort();
        if (_window != null) Callable.From(() => { if (IsInstanceValid(_window)) _window.ResetSize(); }).CallDeferred();
    }

    private void Arrange()
    {
        var size = Size;
        if (Minimized)
        {
            FitChildInRect(_compact, new Rect2(Vector2.Zero, CompactSize));
            // The client hides its channel-list action itself when the room is minimized; the door goes with it.
            foreach (var action in _actions)
            {
                action.SelfModulate = Colors.Transparent; action.MouseFilter = MouseFilterEnum.Ignore;
                FitChildInRect(action, new Rect2(Vector2.Zero, Vector2.Zero));
            }
            return;
        }
        foreach (var action in _actions) { action.SelfModulate = Colors.White; action.MouseFilter = MouseFilterEnum.Stop; }
        FitChildInRect(_art, new Rect2(Vector2.Zero, size));
        Place(_grip, Spec.Drag(size.X));
        Place(_minimize, Spec.Minimize(size.X));
        Place(_close, Spec.Close(size.X));
        int index = 0;
        for (int i = _actions.Count - 1; i >= 0; i--)
            if (_actions[i].Visible) Place(_actions[i], Spec.HeaderAction(size.X, index++));
        Place(_title, Spec.Title(size.X, index));
        Place(_content, new Rect2(Spec.Inset, Spec.ContentTop, size.X - Spec.Inset * 2, size.Y - Spec.ContentTop - Spec.Inset));
    }

    private void Place(Control control, Rect2 rect)
    {
        FitChildInRect(control, rect);
        control.SetMeta("room_expected_rect", rect);
    }

    /// <summary>
    /// Takes the client's header actions (leave the room, channel list) onto the Classic title row and dresses the
    /// room. Called by the preparer once the client has built the room; the dressing runs again whenever the room
    /// opens, after any generic styling that ran in between.
    /// </summary>
    public void Bind(HudWindow window)
    {
        if (_bound) return;
        _bound = true;
        _window ??= window;
        if (window.Header is { } header)
            foreach (var action in ClassicSocialStyle.Descendants(header).OfType<Button>()
                         .Where(b => b.Icon != null && b.TooltipText is not ("Close" or "Minimize" or "Restore")).ToArray())
            {
                action.GetParent()?.RemoveChild(action);
                AddChild(action);
                ClassicChatRoomSkin.Cell(action);
                action.AddThemeColorOverride("icon_normal_color", ClassicDarkFrame.Heading);
                action.AddThemeColorOverride("icon_hover_color", Colors.White);
                action.SizeFlagsHorizontal = action.SizeFlagsVertical = SizeFlags.Fill;
                action.VisibilityChanged += QueueSort;
                action.Name = "room_action_" + _actions.Count;
                _actions.Add(action);
            }
        NativeWindows.Sync(window, SyncRoster);
        window.VisibilityChanged += () => { if (window.Visible) Callable.From(Dress).CallDeferred(); };
        Callable.From(Dress).CallDeferred();
        Refresh();
    }

    private T? Find<T>(string name) where T : Node =>
        ClassicSocialStyle.Descendants(_content).OfType<T>().FirstOrDefault(n => n.Name == name);

    /// <summary>Restyles the room's fixed controls (idempotent) and starts dressing posts and roster rows as they arrive.</summary>
    public void Dress()
    {
        if (!Plugin.Active || !IsInstanceValid(this) || _window == null || !IsInstanceValid(_window)) return;
        // The generic Classic extender gives every native window its art and theme; the room has its own frame.
        _window.Theme = null;
        foreach (var stray in _window.GetChildren().OfType<ClassicFrame>().ToArray()) { _window.RemoveChild(stray); stray.QueueFree(); }
        // The client sizes the body for its 380 px log; the Classic log and the members column set the width.
        _window.Body.CustomMinimumSize = Vector2.Zero;
        if (Find<HBoxContainer>("room_main") is { } main) main.AddThemeConstantOverride("separation", (int)Spec.ColumnGap);
        if (Find<VBoxContainer>("room_members_column") is { } column)
        {
            column.CustomMinimumSize = new Vector2(Spec.ColumnWidth, 0);
            column.AddThemeConstantOverride("separation", (int)Spec.RowGap);
            if (column.GetChildren().OfType<PanelContainer>().FirstOrDefault() is { } field)
            {
                var box = ClassicDarkFrame.InputBox();
                box.SetContentMarginAll(1);
                field.AddThemeStyleboxOverride("panel", box);
                field.Name = "room_roster_field";
            }
            // The Classic bar is wider than the client's thin one: sort again so the rows end before it.
            foreach (var scroll in ClassicSocialStyle.Descendants(column).OfType<ScrollContainer>()) { ClassicSocialStyle.Scroll(scroll); Reserve(scroll); }
        }
        if (Find<VBoxContainer>("room_roster_rows") is { } rows)
        {
            rows.AddThemeConstantOverride("separation", 0);
            if (rows.GetParent() is VBoxContainer list) list.AddThemeConstantOverride("separation", 0);
            foreach (var row in rows.GetChildren()) StyleRosterRow(row);
            if (!_watching) rows.ChildEnteredTree += StyleRosterRow;
        }
        if (Find<Label>("room_roster_empty") is { } empty) { ClassicSocialStyle.Font(empty, false, Spec.NoticeSize); empty.AddThemeColorOverride("font_color", ClassicDesign.Muted); }
        if (Find<VBoxContainer>("room_leader_box") is { } leader)
        {
            leader.AddThemeConstantOverride("separation", (int)Spec.RowGap);
            foreach (var button in leader.GetChildren().OfType<Button>()) ClassicSocialStyle.Button(button);
        }
        if (Find<ScrollContainer>("room_log_scroll") is { } log)
        {
            log.CustomMinimumSize = new Vector2(Spec.LogWidth, Spec.LogHeight);
            var field = ClassicDarkFrame.InputBox();
            field.SetContentMarginAll(1);
            log.AddThemeStyleboxOverride("panel", field);
            ClassicSocialStyle.ScrollBar(log.GetVScrollBar());
            Reserve(log);
            if (log.GetParent() is VBoxContainer stack) stack.AddThemeConstantOverride("separation", (int)Spec.RowGap);
        }
        if (Find<Button>("room_older") is { } older) ClassicSocialStyle.Button(older);
        if (Find<VBoxContainer>("room_log") is { } posts)
        {
            foreach (var post in posts.GetChildren()) ClassicChatRoomSkin.StylePost(post);
            if (!_watching) posts.ChildEnteredTree += ClassicChatRoomSkin.StylePost;
        }
        foreach (var name in new[] { "room_members_toggle", "room_config", "room_deafen", "room_mic", "room_voice_gear", "room_voice_leave", "room_record", "room_send" })
            if (Find<Button>(name) is { } cell) ClassicChatRoomSkin.Cell(cell);
        if (Find<HBoxContainer>("room_voice_row") is { } voice) voice.AddThemeConstantOverride("separation", 6);
        if (Find<Button>("room_join") is { } join) ClassicSocialStyle.Button(join);
        if (Find<HSlider>("room_volume") is { } volume) { ClassicChatRoomSkin.Slider(volume); volume.CustomMinimumSize = new Vector2(Spec.VolumeWidth, 0); }
        if (Find<HBoxContainer>("room_composer_row") is { } composer) composer.AddThemeConstantOverride("separation", 6);
        if (Find<LineEdit>("room_input") is { } input && !input.HasMeta("classic_room_input"))
        {
            input.SetMeta("classic_room_input", true);
            ClassicSocialStyle.Input(input);
            foreach (var state in new[] { "normal", "focus", "read_only" })
                if (input.GetThemeStylebox(state) is StyleBoxFlat box) box.ContentMarginRight = Spec.EmoticonRoom;
        }
        if (Find<HBoxContainer>("room_record_strip") is { } strip)
        {
            foreach (var label in strip.GetChildren().OfType<Label>()) ClassicSocialStyle.Font(label, true, Spec.FontSize);
            if (Find<Label>("room_record_clock") is { } clock) clock.AddThemeColorOverride("font_color", ClassicDesign.Text);
            foreach (var cancel in strip.GetChildren().OfType<Button>()) ClassicChatRoomSkin.Cell(cancel);
        }
        if (Find<ProgressBar>("room_record_level") is { } level) ClassicChatRoomSkin.Meter(level);
        _watching = true;
        SyncRoster();
        UpdateMinimumSize();
        _window.ResetSize();
    }

    /// <summary>
    /// A scroll container keeps room for its bar by the bar's own minimum width, not its custom one: the Classic
    /// track gets that width as content margins, so rows and posts end before the bar instead of under it.
    /// </summary>
    private static void Reserve(ScrollContainer scroll)
    {
        var bar = scroll.GetVScrollBar();
        float half = Mathf.Ceil(bar.CustomMinimumSize.X / 2);
        foreach (var state in new[] { "scroll", "scroll_focus" })
            if (bar.GetThemeStylebox(state) is StyleBoxFlat track && track.ContentMarginLeft != half)
            {
                var wide = (StyleBoxFlat)track.Duplicate();
                wide.ContentMarginLeft = wide.ContentMarginRight = half;
                bar.AddThemeStyleboxOverride(state, wide);
            }
        scroll.QueueSort();
    }

    private static void StyleRosterRow(Node node)
    {
        if (node is not PanelContainer panel || panel.HasMeta(ClassicChatRoomSkin.RosterMeta)) return;
        if (panel.GetThemeStylebox("panel") is not StyleBoxFlat native) return;
        var box = ClassicSocialStyle.RowBox(false);
        panel.SetMeta(ClassicChatRoomSkin.RosterMeta, native);
        panel.SetMeta("classic_room_box", box);
        panel.AddThemeStyleboxOverride("panel", box);
        panel.CustomMinimumSize = new Vector2(0, Spec.RosterRow);
        foreach (var label in ClassicSocialStyle.Descendants(panel).OfType<Label>())
        {
            // The dot and the leader star are glyphs the client draws in its own font; only the name turns Arial.
            if (label.SizeFlagsHorizontal.HasFlag(Control.SizeFlags.Expand)) { ClassicSocialStyle.Font(label, true, Spec.FontSize); label.Name = "room_roster_name"; }
            else if (label.Text == "★") label.AddThemeColorOverride("font_color", ClassicDarkFrame.Heading);
        }
        panel.Name = "room_roster_row";
    }

    /// <summary>
    /// The client recolours its own row style for the selection and for speaking; the Classic row mirrors that
    /// state: the gold selection line, the speaking green, otherwise the Messenger row rule.
    /// </summary>
    private void SyncRoster()
    {
        if (!_watching || Find<VBoxContainer>("room_roster_rows") is not { } rows) return;
        foreach (var panel in rows.GetChildren().OfType<PanelContainer>())
        {
            if (!panel.HasMeta(ClassicChatRoomSkin.RosterMeta) || panel.GetMeta(ClassicChatRoomSkin.RosterMeta).AsGodotObject() is not StyleBoxFlat native
                || panel.GetMeta("classic_room_box").AsGodotObject() is not StyleBoxFlat box) continue;
            bool selected = native.BgColor == UiTheme.RowHover;
            bool speaking = native.BorderColor == ClassicChatRoomSkin.Speaking;
            box.BgColor = selected ? new Color("26251f") : Colors.Transparent;
            box.BorderColor = speaking ? ClassicChatRoomSkin.Speaking : selected ? ClassicSocialStyle.SelectedEdge : ClassicSocialStyle.RowRule;
            int edge = selected || speaking ? 1 : 0;
            box.BorderWidthLeft = box.BorderWidthTop = box.BorderWidthRight = edge;
            box.BorderWidthBottom = 1;
            panel.SetMeta("room_selected", selected);
            panel.SetMeta("room_speaking", speaking);
        }
    }
}
