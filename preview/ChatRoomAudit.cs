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
    /// restore, settings validation, close) run through the Classic controls.
    /// </summary>
    private async Task CaptureChatRoomAudit(int nation)
    {
        var output = OS.GetEnvironment("LIBREKO_AUDIT_OUTPUT_DIR"); System.IO.Directory.CreateDirectory(output);
        GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Disabled; GetWindow().Size = new Vector2I(1100, 640);
        GetViewport().GuiEmbedSubwindows = true;
        AddChild(new ColorRect { Color = new Color("252822"), Size = new Vector2(1100, 640), MouseFilter = MouseFilterEnum.Ignore });
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
        // Chat categories (3277df5): chat room and Noah Knights lines reach the Classic chat (the old 1023 mask hid
        // them); a room line carries its zone; Noah Knights lines keep the ink of the sender's nation.
        {
            var type = typeof(World).Assembly.GetType("LibreKO.ChatSystem")!;
            var system = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object?[] { null }, null)!;
            var entryType = type.GetNestedType("ChatEntry", BindingFlags.NonPublic)!;
            var store = type.GetField("_store", Flags)!.GetValue(system)!;
            long seq = 900000 + nation * 100;
            void Add(byte channel, string name, int from, string text, string place = "")
            {
                object entry = entryType.GetMethod("Player", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { channel, name, from, false, text })!;
                entryType.GetProperty("Seq")!.SetValue(entry, ++seq);
                entryType.GetProperty("Place")!.SetValue(entry, place);
                store.GetType().GetMethod("AddLast", new[] { entryType })!.Invoke(store, new[] { entry });
            }
            Add(ChatType.General, "Canbo", nation, "general line");
            Add(ChatType.ChatRoom, "Borin", nation, "room line", "Moradon");
            Add(ChatType.NoahKnights, "Rikka", Nations.ElMorad, "noah line");
            Add(ChatType.NoahKnights, "Kaito", Nations.Karus, "noah karus");
            var all = NativeChat.ReadHistory(system, ChatWindow.AllCategories, false, "")!;
            Require(ChatWindow.AllCategories == (int)ChatCategory.All && all.Count == 4, "categories: the full mask shows room and Noah Knights lines (" + all.Count + ")");
            Require(all.Any(l => l.Contains("room line") && l.Contains("[lb]room[rb]") && l.Contains("[lb] Moradon [rb]")), "categories: a room line carries the room tag and its [ Zone ]");
            Require(all.Any(l => l.Contains("noah line") && l.Contains("ffb6c1")) && all.Any(l => l.Contains("noah karus") && l.Contains("87cefa")), "categories: Noah Knights lines keep the nation ink");
            Require(NativeChat.ReadHistory(system, (int)(ChatCategory.ChatRoom | ChatCategory.NoahKnights), false, "")!.Count == 3, "categories: the two new categories filter on their own");
            Require(NativeChat.ReadHistory(system, 1023, false, "")!.Count == 1, "categories: the pre-3277 mask (1023) would hide them");

            // The Noah Knights tab: shown only for Noah Knights, who lose the Clan and Alliance tabs.
            var chat = new ChatWindow(); AddChild(chat); await Frames(3);
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
