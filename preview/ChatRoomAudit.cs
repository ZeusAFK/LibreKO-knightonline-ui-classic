using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Plugins;
using KnightOnlineUiClassic;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Native;
using KnightOnlineUiClassic.Windows;
using System.Reflection;
using System.Text.Json;
using Spec = KnightOnlineUiClassic.Layout.ClassicChatRoomLayout;

public partial class Preview
{
    /// <summary>
    /// chat-room-audit: the Classic chat room (3277df5) for one nation, on the client's own preview builder in
    /// every state it ships (public channel, private room as member and as leader in a call, voice notes, older
    /// history, recording, voice settings, room settings, minimized). The frame, the members column, the log, the
    /// voice row and the composer are checked at their actual rendered bounds against the declared layout; posts
    /// keep their side and sender colour; the native actions (send, members toggle, Escape minimise, blink,
    /// restore, settings validation, close) run through the Classic controls. Since ff78eef rooms are open to both
    /// nations and their lines stay in the room window: the room list shows a room of the other nation, the Classic
    /// Enter joins it, the server's replies open its room window, and the Classic chat settings no longer offer
    /// "Chatroom".
    /// </summary>
    private async Task CaptureChatRoomAudit(int nation)
    {
        var output = OS.GetEnvironment("LIBREKO_AUDIT_OUTPUT_DIR"); System.IO.Directory.CreateDirectory(output);
        GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Disabled; GetWindow().Size = new Vector2I(1100, 640);
        GetViewport().GuiEmbedSubwindows = true;
        AddChild(new ColorRect { Color = new Color("252822"), Size = new Vector2(1100, 640), MouseFilter = MouseFilterEnum.Ignore });
        // The installed content: item data (links, tooltips, text table) and the emoticon sheet.
        if (!ProjectSettings.LoadResourcePack(ProjectSettings.GlobalizePath("res://../../build/client/source-content/knightonline.pck"), false)) throw new Exception("Missing existing content pack");
        var offline = new Net(); typeof(Net).GetProperty("I")!.SetValue(null, offline);
        ItemData.EnsureLoaded();
        var voice = VoiceChat.Ensure(this);
        ClassicChatRoomSkin.Register(PluginHost.Ui);
        var checks = new List<string>(); var screens = new List<object>();
        string prefix = nation == 1 ? "karus" : "human";
        void Require(bool value, string text) { if (!value) throw new Exception("CHAT_ROOM_AUDIT: " + text); checks.Add(text); }
        async Task Frames(int count = 6) { for (int i = 0; i < count; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
        static bool Inside(Rect2 outer, Rect2 inner) => outer.Grow(1).Encloses(inner);
        var senders = new Dictionary<string, int> { ["Borin"] = 9003, ["Rikka"] = 9001, ["Arwen"] = 9004, ["Kaito"] = 9005 };
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        T Named<T>(Node root, string name) where T : Node => Descendants(root).OfType<T>().Single(n => n.Name == name);

        async Task Capture(HudWindow window, string state)
        {
            await Frames(4); window.Position = new Vector2(40, 40); await Frames(3);
            string file = prefix + "-" + state + ".png";
            GetViewport().GetTexture().GetImage().SavePng(output + "/" + file);
            var frame = ClassicChatRoomFrame.Of(window)!;
            Require(window.Size == frame.Size, state + ": the Classic frame fills the window " + frame.Size);
            Require(GetViewportRect().Encloses(window.GetGlobalRect()), state + ": complete window inside the viewport " + window.GetGlobalRect());
            var origin = frame.GetGlobalRect().Position;
            Rect2 Local(Control c) => new(c.GetGlobalRect().Position - origin, c.GetGlobalRect().Size);
            if (window.Minimized)
            {
                Require(frame.Size == ClassicChatRoomFrame.CompactSize, state + ": minimized room is the compact whisper bar " + frame.Size);
                var compact = Named<LayoutView>(frame, "room_compact");
                Require(compact.IsVisibleInTree() && !frame.Content.IsVisibleInTree(), state + ": only the compact bar shows");
                foreach (var button in Descendants(compact).OfType<BaseButton>().Where(b => b.IsVisibleInTree()))
                    Require(Inside(new Rect2(Vector2.Zero, frame.Size), Local(button)), state + ": compact control inside the bar " + button.Name);
                Require(Named<Label>(frame, "room_compact_title").Text == Named<Label>(frame, "room_title").Text, state + ": the compact bar carries the room title");
                screens.Add(new { state, file, bounds = window.GetGlobalRect().ToString() });
                return;
            }
            var content = frame.Content;
            var column = Named<Control>(content, "room_members_column");
            var voiceRow = Named<Control>(content, "room_voice_row");
            bool members = column.IsVisibleInTree(), call = voiceRow.IsVisibleInTree();
            Require(frame.Size == Spec.Size(members, call), state + ": frame follows the declared layout " + frame.Size + " / " + Spec.Size(members, call)
                + " (" + string.Join(", ", Descendants(content).OfType<Control>().Where(c => c.IsVisibleInTree() && c.GetCombinedMinimumSize().X > 300).Select(c => c.Name + " " + c.GetType().Name + " " + c.GetCombinedMinimumSize() + " custom " + c.CustomMinimumSize)) + ")");
            Require(Local(Named<Control>(content, "room_log_scroll")) == Spec.Log(members), state + ": actual log bounds " + Local(Named<Control>(content, "room_log_scroll")));
            Require(Local(Named<Control>(content, "room_composer_row")) == Spec.Composer(members, call), state + ": actual composer bounds " + Local(Named<Control>(content, "room_composer_row")));
            if (call) Require(Local(voiceRow) == Spec.Voice(members), state + ": actual voice row bounds " + Local(voiceRow));
            if (members)
            {
                Require(Local(column) == Spec.Column(call), state + ": actual members column bounds " + Local(column));
                Require(Local(Named<Control>(content, "room_roster_field")) == Spec.Roster(call), state + ": actual roster field bounds " + Local(Named<Control>(content, "room_roster_field")));
                Require(Local(Named<Control>(content, "room_leader_box")) == Spec.Leader(call), state + ": actual leader actions bounds " + Local(Named<Control>(content, "room_leader_box")));
            }
            foreach (var part in Descendants(frame).OfType<Control>().Where(c => c.IsVisibleInTree() && c.HasMeta("room_expected_rect")))
                Require(part.GetRect() == part.GetMeta("room_expected_rect").AsRect2(), state + ": actual bounds of " + part.Name + " " + part.GetRect());
            var title = Named<Label>(frame, "room_title");
            foreach (var header in frame.HeaderActions.Where(a => a.IsVisibleInTree()).Cast<Control>().Append(Named<Control>(frame, "room_minimize")).Append(Named<Control>(frame, "room_close")))
            {
                Require(!title.GetRect().Intersects(header.GetRect()), state + ": the title leaves room for " + header.Name);
                Require(Mathf.Abs(header.GetRect().GetCenter().Y - title.GetRect().GetCenter().Y) <= 1, state + ": " + header.Name + " centred on the title row");
            }
            var area = content.GetGlobalRect();
            Require(Inside(frame.GetGlobalRect().Grow(-ClassicFrame.PlainEdge), area), state + ": content inside the frame line");
            var scrolls = Descendants(content).OfType<ScrollContainer>().ToArray();
            foreach (var control in Descendants(content).OfType<Control>().Where(c => c.IsVisibleInTree() && !scrolls.Any(s => s != c && s.IsAncestorOf(c))))
            {
                var rect = control.GetGlobalRect();
                if (rect.Size.X > 0 && rect.Size.Y > 0) Require(Inside(area, rect), state + ": " + control.GetType().Name + " " + control.Name + " inside the content " + rect);
            }
            foreach (var button in Descendants(frame).OfType<Button>().Where(b => b.IsVisibleInTree() && b.Text.Length > 0 && b is not CheckBox and not CheckButton))
            {
                var font = button.GetThemeFont("font"); int size = button.GetThemeFontSize("font_size");
                Require(font == Plugin.Kit.Bold && button.Size.Y >= ClassicSocialLayout.ActionHeight, state + ": Classic action " + button.Text + " " + button.Size);
                Require(font.GetStringSize(button.Text, HorizontalAlignment.Left, -1, size).X <= button.Size.X - 2, state + ": caption fits " + button.Text);
            }
            foreach (var cell in Descendants(frame).OfType<Button>().Where(b => b.IsVisibleInTree() && b.HasMeta(ClassicChatRoomSkin.CellMeta) && !scrolls.Any(s => s.IsAncestorOf(b))))
                Require(cell.Size == new Vector2(Spec.Cell, Spec.Cell) && cell.GetThemeStylebox("normal") is StyleBoxTexture, state + ": icon action in the Classic cell " + cell.Name + " " + cell.Size);
            var input = Named<LineEdit>(content, "room_input");
            if (input.IsVisibleInTree()) Require(input.Size.Y == Spec.RowHeight && input.GetThemeStylebox("normal") is StyleBoxFlat { BorderColor: var line } && line == ClassicDarkFrame.Line, state + ": Classic composer field");
            if (input.IsVisibleInTree())
            {
                var face = Named<TextureButton>(input, "room_emoticons");
                Require(face.TextureNormal != null && face.Size == new Vector2(Spec.EmoticonSize, Spec.EmoticonSize) && input.GetGlobalRect().Grow(-3).Encloses(face.GetGlobalRect())
                    && Mathf.Abs(face.GetGlobalRect().GetCenter().Y - input.GetGlobalRect().GetCenter().Y) <= 0.5f, state + ": the emoticon button sits inside the composer field " + face.GetGlobalRect());
            }
            var log = Named<ScrollContainer>(content, "room_log_scroll").GetGlobalRect();
            foreach (var bubble in Descendants(content).OfType<PanelContainer>().Where(p => p.HasMeta("room_bubble")))
            {
                bool mine = bubble.GetMeta("room_bubble").AsString() == "mine";
                var rect = bubble.GetGlobalRect();
                Require(mine ? rect.End.X >= log.End.X - 26 : rect.Position.X <= log.Position.X + 12, state + ": " + (mine ? "own post on the right " : "post on the left ") + rect);
                Require(bubble.GetThemeStylebox("panel") is StyleBoxFlat { CornerRadiusTopLeft: 0 } box && box.BgColor == (mine ? ClassicChatRoomSkin.MineBubble : ClassicChatRoomSkin.TheirBubble), state + ": Classic post field");
                foreach (var name in Descendants(bubble).OfType<Label>().Where(l => l.Name == "room_post_name"))
                    if (senders.TryGetValue(name.Text, out int id))
                        Require(name.GetThemeColor("font_color") == new Color(RoomNameColours.Of(id)) && name.GetThemeFont("font") == Plugin.Kit.Bold, state + ": sender colour kept for " + name.Text);
            }
            foreach (var chip in Descendants(content).OfType<PanelContainer>().Where(p => p.HasMeta("room_chip") && p.IsVisibleInTree()))
                Require(Mathf.Abs(chip.GetGlobalRect().GetCenter().X - log.GetCenter().X) <= 8, state + ": " + chip.GetMeta("room_chip") + " marker centred " + chip.GetGlobalRect());
            screens.Add(new { state, file, bounds = window.GetGlobalRect().ToString() });
        }

        async Task<(World World, HudWindow Window)> Open(string page)
        {
            var world = new World();
            var window = (HudWindow)DetailCall(world, "BuildChatRoomUiPreview", page)!;
            var layer = new CanvasLayer { Name = "room_" + page };
            AddChild(layer); layer.AddChild(window); window.Visible = true;
            NativeChatRoom.Prepare(window, world);
            await Frames(8);
            Require(window.HasMeta(NativeChatRoom.ControlsMeta) && ClassicChatRoomFrame.Of(window) != null, page + ": the room is prepared and in the Classic frame");
            return (world, window);
        }
        void Done((World World, HudWindow Window) opened)
        {
            DetailCall(opened.World, "ChatRoomDispose");
            opened.Window.GetParent().QueueFree(); opened.World.QueueFree();
        }
        void Press(BaseButton button) => button.EmitSignal(BaseButton.SignalName.Pressed);

        // Public channel: no voice, no members column; the two header actions sit on the title row.
        {
            var opened = await Open("public"); var (world, window) = opened; var frame = ClassicChatRoomFrame.Of(window)!;
            Require(frame.HeaderActions.Count == 2 && frame.HeaderActions.All(a => a.HasMeta(ClassicChatRoomSkin.CellMeta)), "public: leave and channel-list actions in Classic cells");
            Require(frame.HeaderActions.Select(a => a.TooltipText).Order().SequenceEqual(new[] { "Channel list", "Leave the room" }), "public: the client's header actions keep their meaning");
            Require(Named<Label>(frame, "room_title").Text == NationChatRules.Title("General", 212, NationChatRules.PublicCapacity), "public: the title is the client's 'Name (n/max)'");
            await Capture(window, "public");
            Press(Named<Button>(frame, "room_close")); await Frames(2);
            Require(!window.Visible && !offline.ChatRoomPanelShown, "public: the Classic close runs the client's close");
            Done(opened);
        }
        // Private room as a member: members column, voice row, disabled leader actions; send, members toggle, Escape, blink, restore.
        {
            var opened = await Open("text"); var (world, window) = opened; var frame = ClassicChatRoomFrame.Of(window)!;
            await Capture(window, "text");
            foreach (var name in new[] { "room_leader_handover", "room_leader_kick" })
            {
                var action = Named<Button>(frame, name);
                Require(action.Disabled && action.GetThemeColor("font_disabled_color") == ClassicSocialStyle.ButtonDisabledInk, "text: leader action " + action.Text + " disabled for a member, Classic disabled ink");
            }
            Require(Named<Button>(frame, "room_config").Disabled, "text: room settings disabled for a member");
            var input = Named<LineEdit>(frame, "room_input");
            input.Text = "on my way"; Press(Named<Button>(frame, "room_send")); await Frames(2);
            Require(input.Text == "", "text: Send runs the client's send (the composer clears)");
            Press(Named<Button>(frame, "room_members_toggle")); await Frames(6);
            Require(!Named<Control>(frame, "room_members_column").IsVisibleInTree() && frame.Size == Spec.Size(false, true), "text: the members toggle hides the column and the frame narrows " + frame.Size);
            await Capture(window, "text-members-hidden");
            Press(Named<Button>(frame, "room_members_toggle")); await Frames(6);
            Require(frame.Size == Spec.Size(true, true), "text: the members toggle shows the column again");
            // Escape: the client's own escape stack minimizes an expanded room.
            DetailCall(world, "BuildEscapeStack");
            var stack = (System.Collections.IList)typeof(World).GetField("_escapeStack", Flags)!.GetValue(world)!;
            foreach (var entry in stack)
            {
                var (isOpen, close) = ((Func<bool>, Action))entry!;
                bool open; try { open = isOpen(); } catch (NullReferenceException) { continue; }
                if (open) { close(); break; }
            }
            await Frames(6);
            Require(window.Minimized && frame.Size == ClassicChatRoomFrame.CompactSize, "text: Escape minimizes the room to the compact bar");
            await Capture(window, "escape-minimized");
            DetailCall(world, "StartChatRoomBlink"); await Frames(4);
            Require(frame.Unread && window.Modulate == Colors.White, "text: a new line blinks the compact bar (the client's fade is taken over)");
            await Capture(window, "minimized-unread");
            Press(Descendants(frame).OfType<BaseButton>().Single(b => b.Name == "btn_open")); await Frames(6);
            Require(!window.Minimized && !frame.Unread && frame.Size == Spec.Size(true, true), "text: Restore expands the room and clears the blink");
            Done(opened);
        }
        // Item links and emoticons in posts (installed item data and emoticon sheet): the grade-coloured link opens
        // the client's item tooltip; emoticons sit inline on the text line inside the Classic post field.
        {
            var opened = await Open("text"); var (world, window) = opened; var frame = ClassicChatRoomFrame.Of(window)!;
            DetailCall(world, "BuildItemTooltip");
            var tipLayer = (CanvasLayer)DetailField(world, "_itemTipLayer")!; world.RemoveChild(tipLayer); AddChild(tipLayer);
            var texts = Descendants(frame).OfType<RichTextLabel>().Where(r => r.Name == "room_post_text").ToArray();
            Require(texts.Length > 0 && texts.All(r => r.GetThemeFont("normal_font") == Plugin.Kit.ChatStrong && r.GetThemeFontSize("normal_font_size") == Spec.LineSize), "links: post text in the Classic chat font");
            const int linked = 156210008;
            Require(ItemData.Get(linked) != null, "links: the installed item data knows the linked item " + linked);
            var link = texts.SingleOrDefault(r => r.Text.Contains("[url=i:" + linked + "]"));
            Require(link != null && link.Text.Contains("[color=#" + ItemGrade.Tint(linked).ToHtml(false) + "][lb]" + ItemData.DisplayName(linked) + "[rb]"),
                "links: the item link shows the item name in its grade colour " + ItemGrade.Tint(linked).ToHtml(false));
            var faces = texts.Where(r => r.Text.Contains("[img") && r.Text.Contains(Emoticons.SheetPath)).ToArray();
            Require(ResourceLoader.Exists(Emoticons.SheetPath) && faces.Length >= 2, "links: emoticons render from the installed sheet (" + faces.Length + " posts)");
            // Scroll the linked post into view and check that the link and the emoticons stay inside their fields.
            var logScroll = Named<ScrollContainer>(frame, "room_log_scroll");
            foreach (var rich in faces.Append(link!))
            {
                var bubble = rich.GetParent<Control>();
                while (bubble is not PanelContainer) bubble = bubble.GetParent<Control>();
                var inner = bubble.GetGlobalRect().Grow(-1);
                Require(inner.Encloses(rich.GetGlobalRect()), "links: post text inside its Classic field " + rich.GetGlobalRect() + " / " + bubble.GetGlobalRect());
                Require(rich.GetContentHeight() <= rich.Size.Y + 0.5f && rich.Size.Y >= Spec.LineSize, "links: no clipped line in a post with links or emoticons (" + rich.GetContentHeight() + " / " + rich.Size.Y + ")");
            }
            logScroll.EnsureControlVisible(link!); await Frames(4);
            link!.EmitSignal(RichTextLabel.SignalName.MetaClicked, "i:" + linked); await Frames(4);
            var tip = (Control)DetailField(world, "_itemTipPanel")!;
            Require(world.ItemTooltipVisible && Descendants(tip).OfType<Label>().Any(l => l.Text.Contains(ItemData.DisplayName(linked).Trim())), "links: clicking the link opens the client's tooltip for the item");
            window.Position = new Vector2(40, 40); await Frames(2);
            tip.Position = new Vector2(window.GetGlobalRect().End.X + 12, 40); await Frames(3);
            string tipFile = prefix + "-links-tooltip.png";
            GetViewport().GetTexture().GetImage().SavePng(output + "/" + tipFile); screens.Add(new { state = "links-tooltip", file = tipFile, bounds = window.GetGlobalRect().ToString() });
            link.EmitSignal(RichTextLabel.SignalName.MetaHoverEnded, "i:" + linked); await Frames(2);
            Require(!world.ItemTooltipVisible, "links: leaving the link hides the tooltip");
            logScroll.ScrollVertical = 0; await Frames(3);
            await Capture(window, "links-top");
            tipLayer.QueueFree();
            Done(opened);
        }
        // Leader in a call: in-call controls, selected and speaking roster rows, enabled leader actions.
        {
            var opened = await Open("voice"); var (world, window) = opened; var frame = ClassicChatRoomFrame.Of(window)!;
            await Capture(window, "voice");
            foreach (var name in new[] { "room_deafen", "room_volume", "room_mic", "room_voice_gear", "room_voice_leave" })
                Require(Named<Control>(frame, name).IsVisibleInTree(), "voice: in-call control " + name + " shown");
            Require(!Named<Control>(frame, "room_join").IsVisibleInTree(), "voice: Join voice hidden in a call");
            Require(Named<HSlider>(frame, "room_volume").GetThemeStylebox("slider") is StyleBoxFlat, "voice: the volume slider uses the Classic track");
            var rows = Named<VBoxContainer>(frame, "room_roster_rows").GetChildren().OfType<PanelContainer>().Where(r => !r.IsQueuedForDeletion()).ToArray();
            Require(rows.Length > 0 && rows.All(r => r.HasMeta(ClassicChatRoomSkin.RosterMeta) && r.Size.Y == Spec.RosterRow), "voice: Classic roster rows of " + Spec.RosterRow + " px");
            var selected = rows.Where(r => r.GetMeta("room_selected", false).AsBool()).ToArray();
            Require(selected.Length == 1 && Descendants(selected[0]).OfType<Label>().Any(l => l.Text == "Borin")
                && ((StyleBoxFlat)selected[0].GetThemeStylebox("panel")).BorderColor == ClassicSocialStyle.SelectedEdge, "voice: the selected member carries the gold selection line");
            Require(rows.Any(r => r.GetMeta("room_speaking", false).AsBool() && ((StyleBoxFlat)r.GetThemeStylebox("panel")).BorderColor == ClassicChatRoomSkin.Speaking), "voice: a speaking member is outlined in the speaking green");
            int inCall = rows.TakeWhile(r => Descendants(r).OfType<Label>().First().Modulate.A > 0).Count();
            Require(inCall > 1 && rows.Skip(inCall).All(r => Descendants(r).OfType<Label>().First().Modulate.A == 0), "voice: the members in the call are listed on top (" + inCall + ")");
            foreach (var name in new[] { "room_leader_handover", "room_leader_kick" }) Require(!Named<Button>(frame, name).Disabled, "voice: leader action enabled for the leader with a member picked");
            Require(!Named<Button>(frame, "room_config").Disabled, "voice: room settings enabled for the leader");
            Done(opened);
        }
        // Voice notes: waveform with the playhead, a download in progress.
        {
            var opened = await Open("notes"); var (world, window) = opened; var frame = ClassicChatRoomFrame.Of(window)!;
            await Frames(30);
            var waves = Descendants(frame).OfType<VoiceWaveformView>().ToArray();
            Require(waves.Length >= 3 && waves.All(w => w.Dot == ClassicDarkFrame.Heading), "notes: voice notes keep their waveform, Classic gold playhead");
            Require(waves.Any(w => Mathf.IsEqualApprox(w.Progress, 0.38f)), "notes: the playing note shows its progress");
            Require(Descendants(frame).OfType<NoteDownloadRing>().Any(r => r.Visible), "notes: a downloading note shows its ring");
            await Capture(window, "notes");
            Done(opened);
        }
        // History on demand: the Classic button, then a post arriving later is dressed as well.
        {
            var opened = await Open("older"); var (world, window) = opened; var frame = ClassicChatRoomFrame.Of(window)!;
            var older = Named<Button>(frame, "room_older");
            Require(older.IsVisibleInTree() && older.GetSignalConnectionList(BaseButton.SignalName.Pressed).Count > 0, "older: 'Load older messages' keeps the client's request");
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout); await Frames(4);
            var posts = Named<VBoxContainer>(frame, "room_log").GetChildren().Where(n => !n.IsQueuedForDeletion()).ToArray();
            Require(posts.Length > 0 && posts.All(p => p.HasMeta(ClassicChatRoomSkin.PostMeta)), "older: every post, also the later voice note, is dressed Classic (" + posts.Length + ")");
            await Capture(window, "older");
            Done(opened);
        }
        // Recording a voice note.
        {
            var opened = await Open("recording"); var (world, window) = opened; var frame = ClassicChatRoomFrame.Of(window)!;
            Require(Named<Control>(frame, "room_record_strip").IsVisibleInTree() && !Named<Control>(frame, "room_input").IsVisibleInTree(), "recording: the record strip replaces the composer");
            Require(Named<ProgressBar>(frame, "room_record_level").GetThemeStylebox("background") is StyleBoxFlat { BorderColor: var edge } && edge == ClassicDarkFrame.Line, "recording: the level meter on the Classic track");
            await Capture(window, "recording");
            Done(opened);
        }
        // Popups: voice settings and room settings on the Classic menu panel; room settings validation.
        foreach (var (page, field) in new[] { ("settings", "_roomVoiceSettings"), ("config", "_roomConfig") })
        {
            var opened = await Open(page); var (world, window) = opened;
            var popup = (PopupPanel)typeof(World).GetField(field, Flags)!.GetValue(world)!;
            await Frames(4);
            window.Position = new Vector2(40, 40); await Frames(2);
            // An embedded popup can lose its focus to the next run's window; show it again beside the room.
            if (!popup.Visible) popup.Popup();
            popup.Position = new Vector2I((int)window.GetGlobalRect().End.X + 12, 120);
            await Frames(4);
            Require(popup.Visible, page + ": the client's popup is shown");
            Require(popup.HasMeta("classic_popup") && popup.GetThemeStylebox("panel") is StyleBoxFlat { BorderColor: var line } && line == ClassicDarkFrame.Line, page + ": popup on the Classic menu panel");
            Require(GetViewportRect().Encloses(new Rect2(popup.Position, popup.Size)), page + ": popup inside the viewport");
            foreach (var label in Descendants(popup).OfType<Label>().Where(l => l.Visible && l.Text.Length > 0))
                Require(label.GetThemeFont("font") == Plugin.Kit.Bold || label.GetThemeFont("font") == Plugin.Kit.Regular, page + ": Arial label " + label.Text);
            foreach (var slider in Descendants(popup).OfType<HSlider>()) Require(slider.GetThemeStylebox("slider") is StyleBoxFlat, page + ": Classic slider");
            await Capture(window, page);
            if (page == "config")
            {
                ((LineEdit)typeof(World).GetField("_roomConfigName", Flags)!.GetValue(world)!).Text = "";
                Press(Descendants(popup).OfType<Button>().Single(b => b.Text == "Save")); await Frames(4);
                var error = (Label)typeof(World).GetField("_roomConfigError", Flags)!.GetValue(world)!;
                Require(error.Visible && error.Text.Length > 0, "config: Save runs the client's validation: " + error.Text);
                await Capture(window, "config-error");
            }
            popup.Hide();
            Done(opened);
        }
        // Minimized from the start (the client's preview state), then restored.
        {
            var opened = await Open("minimized"); var (world, window) = opened; var frame = ClassicChatRoomFrame.Of(window)!;
            await Capture(window, "minimized");
            Press(Descendants(frame).OfType<BaseButton>().Single(b => b.Name == "btn_open")); await Frames(6);
            Require(!window.Minimized && frame.Size == Spec.Size(true, true), "minimized: Restore brings the room back");
            Press(Named<TextureButton>(frame, "room_minimize")); await Frames(6);
            Require(window.Minimized, "minimized: the Classic minimize button minimizes the room");
            Done(opened);
        }
        // Chat rooms of both nations (ff78eef dropped the client's nation check): the room list shows a room of the
        // other nation with its nation mark like any other room, the Classic Enter joins it (a locked one asks for
        // its password), and the room window opens for it. Its lines stay in the room window and never reach the chat.
        {
            ClassicSocialSkin.Register(PluginHost.Ui);
            int own = nation == 1 ? Nations.Karus : Nations.ElMorad, other = own == Nations.Karus ? Nations.ElMorad : Nations.Karus;
            void Handle(string handler, Packet packet) => typeof(Net).GetMethod(handler, Flags)!.Invoke(offline, new object[] { packet });
            string Pending() => (((string, bool, int, int))typeof(Net).GetField("_pendingRoom", Flags)!.GetValue(offline)!).Item1;
            void Click(Control row) => row.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
            string Mark(int of) => "res://assets/ui/nation_chat/" + (of == Nations.Karus ? "karus.png" : "elmorad.png");

            var world = new World();
            var list = (HudWindow)DetailCall(world, "BuildNationChatUiPreview", "private")!;
            var me = offline.LastEnter; me.Name = "Tester10"; me.Nation = own;
            typeof(Net).GetMethod("SeedPreviewEnter", Flags)!.Invoke(offline, new object[] { me });
            typeof(World).GetField("_myId", Flags)!.SetValue(world, 9006);
            // The client's fixture rooms plus two rooms of the other nation: an open one and a locked one.
            var open = new NationRoom(1005, "Border traders", false, other, 3, 20);
            var locked = new NationRoom(1006, "Ronark Land watch", true, other, 5, 10);
            var rooms = new List<NationRoom>((List<NationRoom>)DetailField(world, "_ncRooms")!) { open, locked };
            Require(rooms.Any(r => r.Nation == own) && rooms.Count(r => r.Nation == other) >= 2, "nations: the list holds rooms of both nations");
            offline.NationChat.Left(); DetailCall(world, "RefreshNationButtons");
            DetailCall(world, "OnNationRooms", rooms);
            var listLayer = new CanvasLayer { Name = "nation_rooms" }; AddChild(listLayer); listLayer.AddChild(list); list.Visible = true;
            NativeSocial.Prepare(list, world);
            var password = (HudWindow)DetailCall(world, "DetachPreviewControl", (HudWindow)DetailField(world, "_ncPassword")!)!;
            listLayer.AddChild(password);
            await Frames(8);
            var rows = (VBoxContainer)DetailField(world, "_ncPrivateRows")!;
            PanelContainer Row(NationRoom room) => rows.GetChildren().OfType<PanelContainer>().Where(r => !r.IsQueuedForDeletion())
                .Single(r => Descendants(r).OfType<Label>().Any(l => l.Text == NationChatRules.Row(room.Name, room.Members, room.MaxMembers)));
            Click(Row(open)); await Frames(6);
            Require((int)DetailField(world, "_ncSelectedRoom")! == open.Id, "nations: a click selects the other nation's room natively");
            list.Position = ((GetViewportRect().Size - list.Size) / 2).Round(); await Frames(4);
            Require(list.GetChildren().OfType<ClassicSocialFrame>().Count() == 1 && GetViewportRect().Encloses(list.GetGlobalRect()), "nations: the room list in the Classic social frame inside the viewport " + list.GetGlobalRect());
            string listFile = prefix + "-nation-rooms-both.png";
            var shot = GetViewport().GetTexture().GetImage(); shot.SavePng(output + "/" + listFile);
            screens.Add(new { state = "nation-rooms-both", file = listFile, bounds = list.GetGlobalRect().ToString() });
            int Lit(Rect2 rect)
            {
                int lit = 0;
                for (int y = (int)rect.Position.Y; y < (int)rect.End.Y; y++)
                    for (int x = (int)rect.Position.X; x < (int)rect.End.X; x++)
                        if (shot.GetPixel(x, y) is var c && Mathf.Max(c.R, Mathf.Max(c.G, c.B)) > 0.3f) lit++;
                return lit;
            }
            var scrollRect = ((Control)rows.GetParent()).GetGlobalRect();
            var plainInks = new HashSet<Color>();
            foreach (var room in rooms)
            {
                var row = Row(room); var rect = row.GetGlobalRect();
                var icons = Descendants(row).OfType<TextureRect>().ToArray();
                var label = Descendants(row).OfType<Label>().Single();
                string who = room.Name + (room.Nation == own ? " (own nation)" : " (other nation)");
                Require(row.HasMeta(ClassicSocialStyle.Styled) && row.GetMeta("social_row", false).AsBool() && scrollRect.Grow(1).Encloses(rect), who + ": a Classic list row inside the list " + rect);
                Require(icons.Length == 2 && icons[1].Texture?.ResourcePath == Mark(room.Nation), who + ": the row carries its nation's mark " + icons.ElementAtOrDefault(1)?.Texture?.ResourcePath);
                Require(room.Locked == (icons[0].Texture != null), who + ": the lock mark follows the room");
                foreach (var icon in icons.Where(i => i.Texture != null))
                {
                    var at = icon.GetGlobalRect();
                    Require(rect.Encloses(at) && Mathf.Abs(at.GetCenter().Y - rect.GetCenter().Y) <= 1 && at.End.X <= label.GetGlobalRect().Position.X, who + ": mark inside the row, centred, before the name " + at);
                    Require(Lit(at) >= 12, who + ": the mark renders (" + Lit(at) + " lit px)");
                }
                Require(label.GetThemeFont("font") == Plugin.Kit.Bold, who + ": Arial Bold row text");
                bool selected = room.Id == open.Id;
                Require(row.GetMeta("social_selected", false).AsBool() == selected, who + ": selection state " + selected);
                if (selected)
                    Require(row.GetThemeStylebox("panel") is StyleBoxFlat { BorderColor: var edge } && edge == ClassicSocialStyle.SelectedEdge && label.GetThemeColor("font_color") == ClassicDarkFrame.Heading, who + ": the selected room carries the gold selection line and heading ink");
                else plainInks.Add(label.GetThemeColor("font_color"));
            }
            Require(plainInks.Count == 1 && plainInks.Single() == ClassicDesign.Text, "nations: rooms of both nations share the Classic row ink (" + string.Join(", ", plainInks) + ")");
            Texture2D? MarkOf(NationRoom room) => Descendants(Row(room)).OfType<TextureRect>().ElementAt(1).Texture;
            Require(MarkOf(open) != MarkOf(rooms.First(r => r.Nation == own)), "nations: own and other nation rooms carry different marks");

            // Enter on the other nation's rooms: the client accepts both without a notice; a locked one asks for its password.
            var join = (Button)DetailField(world, "_ncPriJoin")!;
            var log = (TabbedLog)DetailField(world, "_combatLog")!;
            Require(join.IsVisibleInTree() && join.Text == "Enter" && !join.Disabled, "nations: the Classic Enter shows outside a room");
            Click(Row(locked)); await Frames(4);
            int notices = log.Lines(LogTab.All).Count;
            Press(join); await Frames(4);
            Require(password.Visible && password.GetChildren().OfType<ClassicSocialFrame>().Count() == 1 && Pending() == locked.Name, "nations: a locked room of the other nation asks for its password in the Classic frame");
            password.Visible = false;
            Click(Row(open)); await Frames(4);
            Press(join); await Frames(4);
            Require(Pending() == open.Name && !password.Visible && log.Lines(LogTab.All).Count == notices, "nations: Enter joins the other nation's room without a notice (" + string.Join(" | ", log.Lines(LogTab.All).Skip(notices)) + ")");

            // The server's enter reply opens the room window; members and a line follow.
            DetailCall(world, "ChatRoomInit");
            var entered = new Packet(GameOpcodes.GS_NATION_CHAT);
            entered.WriteByte(NationChatReplies.NationRooms); entered.WriteByte(NationChatReplies.PrivateEnter); entered.WriteByte(0); entered.WriteUShort((ushort)open.Id);
            Handle("HandleNationChat", entered); await Frames(4);
            var roomWindow = (HudWindow)DetailField(world, "_roomWindow")!;
            Require(offline.NationChat.InPrivate && offline.NationChat.RoomId == open.Id && roomWindow.Visible, "nations: the enter reply opens the other nation's room");
            DetailCall(world, "DetachPreviewControl", roomWindow);
            var roomLayer = new CanvasLayer { Name = "room_other_nation" }; AddChild(roomLayer); roomLayer.AddChild(roomWindow);
            NativeChatRoom.Prepare(roomWindow, world);
            await Frames(8);
            var frame = ClassicChatRoomFrame.Of(roomWindow)!;
            Require(roomWindow.HasMeta(NativeChatRoom.ControlsMeta) && ClassicChatRoomFrame.Of(roomWindow) != null, "nations: the room window is in the Classic frame");
            var people = new[] { (9101, "Valka"), (9102, "Orrin"), (9103, "Sefa"), (9006, "Tester10") };
            var roster = new Packet(GameOpcodes.GS_NATION_CHAT);
            roster.WriteByte(NationChatReplies.NationRooms); roster.WriteByte(NationChatReplies.Members); roster.WriteByte(0);
            roster.WriteUShort((ushort)open.Id); roster.WriteString(open.Name); roster.WriteInt(9101); roster.WriteUShort((ushort)open.MaxMembers); roster.WriteUShort((ushort)people.Length);
            foreach (var (id, name) in people) { roster.WriteInt(id); roster.WriteString(name); }
            Handle("HandleNationChat", roster);
            int chatLines = 0, roomLines = 0;
            Action<ChatLine> chatLine = _ => chatLines++; Action<RoomChatLine> roomLine = _ => roomLines++;
            offline.ChatEvent += chatLine; offline.RoomChatEvent += roomLine;
            var said = new Packet(GameOpcodes.GS_CHAT);
            said.WriteByte(ChatType.ChatRoom); said.WriteByte(Net.RoomChatMessage); said.WriteInt(9101); said.WriteString("Valka"); said.WriteString("Welcome to both nations"); said.WriteUShort(21);
            Handle("HandleChat", said); await Frames(8);
            offline.ChatEvent -= chatLine; offline.RoomChatEvent -= roomLine;
            Require(roomLines == 1 && chatLines == 0, "nations: a room line goes to the room window only, never to the chat (room " + roomLines + ", chat " + chatLines + ")");
            var posts = Named<VBoxContainer>(frame, "room_log").GetChildren().Where(n => !n.IsQueuedForDeletion()).ToArray();
            Require(posts.Any(p => p.HasMeta(ClassicChatRoomSkin.PostMeta) && Descendants(p).OfType<Label>().Any(l => l.Name == "room_post_name" && l.Text == "Valka"
                && l.GetThemeColor("font_color") == new Color(RoomNameColours.Of(9101)))), "nations: the line is a Classic post with the sender's colour");
            var listed = Named<VBoxContainer>(frame, "room_roster_rows").GetChildren().OfType<PanelContainer>().Where(r => !r.IsQueuedForDeletion())
                .SelectMany(r => Descendants(r).OfType<Label>().Select(l => l.Text)).ToArray();
            Require(people.All(p => listed.Contains(p.Item2)), "nations: the members column lists the room's members: " + string.Join(", ", listed));
            Require(Named<Label>(frame, "room_title").Text == NationChatRules.Title(open.Name, open.Members + 1, open.MaxMembers), "nations: the room title is the other nation's room: " + Named<Label>(frame, "room_title").Text);
            listLayer.Visible = false;
            await Capture(roomWindow, "other-nation-room");
            DetailCall(world, "ChatRoomDispose"); DetailCall(world, "NationChatDispose");
            offline.NationChat.Left(); offline.ChatRoomPanelShown = false;
            listLayer.QueueFree(); roomLayer.QueueFree(); world.QueueFree(); await Frames(2);
        }
        // Chat categories: Noah Knights lines reach the Classic chat (the old 1023 mask hid them) and keep the ink of
        // the sender's nation. Chat room lines stay in the room window since ff78eef, so the client no longer offers
        // "Chatroom" as a filter and neither do the Classic chat settings.
        {
            var type = typeof(World).Assembly.GetType("LibreKO.ChatSystem")!;
            var system = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object?[] { null }, null)!;
            var entryType = type.GetNestedType("ChatEntry", BindingFlags.NonPublic)!;
            var store = type.GetField("_store", Flags)!.GetValue(system)!;
            long seq = 900000 + nation * 100;
            void Add(byte channel, string name, int from, string text)
            {
                object entry = entryType.GetMethod("Player", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { channel, name, from, false, text })!;
                entryType.GetProperty("Seq")!.SetValue(entry, ++seq);
                store.GetType().GetMethod("AddLast", new[] { entryType })!.Invoke(store, new[] { entry });
            }
            Add(ChatType.General, "Canbo", nation, "general line");
            Add(ChatType.NoahKnights, "Rikka", Nations.ElMorad, "noah line");
            Add(ChatType.NoahKnights, "Kaito", Nations.Karus, "noah karus");
            var all = NativeChat.ReadHistory(system, ChatWindow.AllCategories, false, "")!;
            Require(ChatWindow.AllCategories == (int)ChatCategory.All && all.Count == 3, "categories: the full mask shows Noah Knights lines (" + all.Count + ")");
            Require(all.Any(l => l.Contains("noah line") && l.Contains("ffb6c1")) && all.Any(l => l.Contains("noah karus") && l.Contains("87cefa")), "categories: Noah Knights lines keep the nation ink");
            Require(NativeChat.ReadHistory(system, (int)ChatCategory.NoahKnights, false, "")!.Count == 2, "categories: the Noah Knights category filters on its own");
            Require(NativeChat.ReadHistory(system, 1023, false, "")!.Count == 1, "categories: the pre-3277 mask (1023) would hide them");
            Require(!entryType.GetProperties(Flags).Any(p => p.Name == "Place"), "categories: chat entries carry no zone any more (ff78eef), the Classic chat reads none");
            // The Classic chat sends a line unchanged when it starts with a prefix the client knows; ff78eef dropped '\'.
            string prefixes = (string)typeof(ChatWindow).GetField("PrefixChars", BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;
            const byte Unprefixed = 255;
            var clientPrefixes = Enumerable.Range(33, 94).Select(c => (char)c).Where(c => ChatPrefixes.ChannelFor(c + "text", Unprefixed) != Unprefixed).ToArray();
            Require(prefixes.Order().SequenceEqual(clientPrefixes.Order()) && !prefixes.Contains('\\'), "prefixes: the Classic chat knows the client's prefixes, without the chat room's '\\': " + prefixes + " / " + new string(clientPrefixes));

            // The Classic chat settings list the client's categories, without "Chatroom", and close up under them.
            var chat = new ChatWindow(); AddChild(chat); await Frames(3);
            chat.OpenSettings(); await Frames(3);
            var panel = (ClassicChatPanel)typeof(ChatWindow).GetField("_settingsPanel", Flags)!.GetValue(chat)!;
            panel.Position = new Vector2(Mathf.Round((GetViewportRect().Size.X - panel.Size.X) / 2), 8); await Frames(3);
            string[] captions = ChatCategories.Each.Where(c => c != ChatCategory.Whisper).Select(ChatCategories.Caption).ToArray();
            var buttons = panel.Body.GetChildren().OfType<Button>().Where(b => b.Visible).ToArray();
            var toggles = buttons.Take(captions.Length).ToArray();
            Require(!ChatCategories.Each.Contains(ChatCategory.ChatRoom) && ChatWindow.SettingsCategories.Length == captions.Length, "settings: the client offers no chat room filter (" + captions.Length + " categories)");
            Require(toggles.Select(b => b.Text[..b.Text.LastIndexOf(':')]).SequenceEqual(captions) && !buttons.Any(b => b.Text.StartsWith(ChatCategories.Caption(ChatCategory.ChatRoom))),
                "settings: the Classic options list the client's categories without Chatroom: " + string.Join(", ", toggles.Select(b => b.Text)));
            var bodyRect = panel.Body.GetGlobalRect();
            // Rendered bounds: every button keeps the declared 156 x 24 (captioned ones used to keep 31 px and overlap the next row).
            foreach (var button in buttons)
                Require(bodyRect.Encloses(button.GetGlobalRect()) && button.Size == new Vector2(156, 24) && buttons.All(o => o == button || !o.GetGlobalRect().Intersects(button.GetGlobalRect())), "settings: " + button.Text + " is 156 x 24 inside the body without overlap " + button.GetGlobalRect());
            foreach (var text in panel.Body.GetChildren().OfType<Label>())
                Require(bodyRect.Encloses(text.GetGlobalRect()) && !buttons.Any(b => b.GetGlobalRect().Intersects(text.GetGlobalRect())), "settings: line '" + text.Text + "' inside the body clear of the buttons " + text.GetGlobalRect());
            float lastRow = toggles.Max(b => b.GetGlobalRect().End.Y);
            var time = buttons.Single(b => b.Text.StartsWith("Time:"));
            Require(time.GetGlobalRect().Position.Y - lastRow == 12 && Mathf.IsEqualApprox(time.Position.Y, ChatWindow.SettingsOptionsTop(captions.Length)), "settings: the options follow the last category row with the section gap, no empty row " + time.GetGlobalRect());
            var help = panel.Body.GetChildren().OfType<Label>().Single(l => l.Text.StartsWith("History:"));
            Require(Mathf.IsEqualApprox(help.GetGlobalRect().End.Y, bodyRect.End.Y) && GetViewportRect().Encloses(panel.GetGlobalRect()), "settings: the panel ends under the help line, inside the viewport " + panel.GetGlobalRect());
            string settingsFile = prefix + "-chat-settings.png";
            GetViewport().GetTexture().GetImage().SavePng(output + "/" + settingsFile); screens.Add(new { state = "chat-settings", file = settingsFile, bounds = panel.GetGlobalRect().ToString() });
            panel.Visible = false; await Frames(2);

            // The Noah Knights tab: shown only for Noah Knights, who lose the Clan and Alliance tabs.
            chat.Position = new Vector2(20, GetViewportRect().Size.Y - chat.Size.Y - 20);
            var tabs = (List<Panel>)typeof(ChatWindow).GetField("_tabs", Flags)!.GetValue(chat)!;
            string Caption(Panel tab) => Descendants(tab).OfType<Button>().First().Text is { Length: > 0 } text ? text : Descendants(tab).OfType<Label>().First().Text;
            string[] Shown() => tabs.Where(t => t.Visible).Select(Caption).ToArray();
            Require(Shown().SequenceEqual(new[] { "All", "Private", "Shout", "Party", "Clan", "Nation", "Alliance" }), "tabs: no Noah tab outside the Noah Knights: " + string.Join(",", Shown()));
            typeof(ChatWindow).GetField("_noahKnight", Flags)!.SetValue(chat, true);
            typeof(ChatWindow).GetMethod("Arrange", Flags)!.Invoke(chat, null); await Frames(3);
            Require(Shown().SequenceEqual(new[] { "All", "Private", "Shout", "Party", "Noah", "Nation" }), "tabs: Noah Knights get the Noah tab instead of Clan and Alliance: " + string.Join(",", Shown()));
            var shown = tabs.Where(t => t.Visible).ToArray();
            Require(shown[0].Position.X == 64 && shown[^1].GetRect().End.X == 620 && shown.Zip(shown.Skip(1)).All(p => p.First.GetRect().End.X == p.Second.Position.X), "tabs: the six tabs fill the original strip without gaps");
            string file = prefix + "-chat-noah-tabs.png";
            GetViewport().GetTexture().GetImage().SavePng(output + "/" + file); screens.Add(new { state = "chat-noah-tabs", file, bounds = chat.GetGlobalRect().ToString() });
            chat.QueueFree(); await Frames(2);
        }
        System.IO.File.WriteAllText(output + "/" + prefix + "-chat-room.json", JsonSerializer.Serialize(new { nation, checks, screens }, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print("CHAT_ROOM_AUDIT_OK " + prefix + " " + checks.Count + " checks / " + screens.Count + " screens");
        await Frames(); voice.QueueFree(); await Frames(2); offline.Free();
    }
}
