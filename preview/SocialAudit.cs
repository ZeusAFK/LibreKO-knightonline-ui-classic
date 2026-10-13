using Godot;
using LibreKO;
using LibreKO.Network;
using LibreKO.Plugins;
using KnightOnlineUiClassic;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Windows;
using System.Text.Json;

public partial class Preview
{
    /// <summary>
    /// social-audit: the Classic social frame on every new social window (nation chat, clan and join-request
    /// boards, leader transfer, commander) for one nation. Each window is captured in its main
    /// states; the frame, title, close button and content are checked at their actual rendered bounds, every
    /// visible control stays inside the content, native inks are mapped to the Classic palette, and the main
    /// native actions run through the restyled controls.
    /// </summary>
    private async Task CaptureSocialAudit(int nation)
    {
        var output = OS.GetEnvironment("LIBREKO_AUDIT_OUTPUT_DIR"); System.IO.Directory.CreateDirectory(output);
        GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Disabled; GetWindow().Size = new Vector2I(1000, 700);
        AddChild(new ColorRect { Color = new Color("252822"), Size = new Vector2(1000, 700), MouseFilter = MouseFilterEnum.Ignore });
        var offline = new Net(); typeof(Net).GetProperty("I")!.SetValue(null, offline);
        ClassicSocialSkin.Register(PluginHost.Ui);
        var checks = new List<string>(); var screens = new List<object>();
        string prefix = nation == 1 ? "karus" : "human";
        void Require(bool value, string text) { if (!value) throw new Exception("SOCIAL_AUDIT: " + text); checks.Add(text); }
        async Task Frames(int count = 6) { for (int i = 0; i < count; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
        // Flow containers place fractional column widths; a sub-pixel overhang is not a visible defect.
        static bool Inside(Rect2 outer, Rect2 inner) => outer.Grow(1).Encloses(inner);
        var nativeInks = new[] { UiTheme.Gold, UiTheme.GoldBright, UiTheme.GoldVivid, UiTheme.TextHi, UiTheme.TextLo, UiTheme.TextDim };

        async Task Capture(HudWindow window, string state)
        {
            await Frames(); window.Position = ((GetViewportRect().Size - window.Size) / 2).Round(); await Frames(3);
            string file = prefix + "-" + state + ".png";
            GetViewport().GetTexture().GetImage().SavePng(output + "/" + file);
            var frame = window.GetChildren().OfType<ClassicSocialFrame>().Single();
            Require(window.Size == frame.Size, state + ": the Classic frame fills the window " + frame.Size);
            Require(GetViewportRect().Encloses(window.GetGlobalRect()), state + ": complete window fits the viewport " + window.GetGlobalRect());
            Require(frame.Size.X >= ClassicSocialLayout.MinimumWidth, state + ": frame keeps its minimum width");
            foreach (var part in Descendants(frame).OfType<Control>().Where(c => c.HasMeta("social_expected_rect")))
                Require(part.GetRect() == part.GetMeta("social_expected_rect").AsRect2(), state + ": actual bounds of " + part.Name + " " + part.GetRect());
            var content = frame.Content; var area = content.GetGlobalRect();
            Require(Inside(frame.GetGlobalRect().Grow(-ClassicFrame.PlainEdge), area), state + ": content stays inside the frame line");
            foreach (var control in Descendants(content).OfType<Control>().Where(c => c.IsVisibleInTree()))
            {
                if (Descendants(content).OfType<ScrollContainer>().Any(s => s != control && s.IsAncestorOf(control))) continue;
                var rect = control.GetGlobalRect();
                if (rect.Size.X <= 0 || rect.Size.Y <= 0) continue;
                Require(Inside(area, rect), state + ": " + control.GetType().Name + " " + control.Name + " stays inside the content " + rect);
            }
            foreach (var button in Descendants(content).OfType<Button>().Where(b => b.IsVisibleInTree() && b.Text.Length > 0 && b is not CheckBox and not CheckButton))
            {
                Require(button.Size.Y >= ClassicSocialLayout.ActionHeight, state + ": Classic button height " + button.Text + " " + button.Size);
                var font = button.GetThemeFont("font"); int size = button.GetThemeFontSize("font_size");
                Require(font == Plugin.Kit.Bold, state + ": Arial Bold caption " + button.Text);
                float text = font.GetStringSize(button.Text, HorizontalAlignment.Left, -1, size).X;
                Require(text <= button.Size.X - 2, state + ": caption fits its button " + button.Text + " " + text + "/" + button.Size.X);
            }
            foreach (var label in Descendants(content).OfType<Label>().Where(l => l.IsVisibleInTree() && l.Text.Length > 0))
            {
                var font = label.GetThemeFont("font");
                Require(font == Plugin.Kit.Bold || font == Plugin.Kit.Regular, state + ": Arial label " + label.Text);
                Require(!nativeInks.Contains(label.GetThemeColor("font_color")), state + ": Classic ink on " + label.Text);
            }
            foreach (var edit in Descendants(content).OfType<LineEdit>().Where(e => e.IsVisibleInTree()))
                Require(edit.Size.Y >= ClassicSocialLayout.InputHeight && edit.GetThemeStylebox("normal") is StyleBoxFlat, state + ": Classic edit field " + edit.Name);
            screens.Add(new { state, file, bounds = window.GetGlobalRect().ToString() });
        }

        async Task<(World World, HudWindow Window)> Open(string view)
        {
            var world = new World();
            var window = (HudWindow)DetailCall(world, "BuildSocialUiPreview", view)!;
            var layer = new CanvasLayer { Name = "social_" + view };
            AddChild(layer); layer.AddChild(window); window.Visible = true;
            KnightOnlineUiClassic.Native.NativeSocial.Prepare(window, world);
            await Frames();
            Require(window.GetChildren().OfType<ClassicSocialFrame>().Count() == 1, view + ": native window uses the Classic social frame");
            return (world, window);
        }
        void Done((World World, HudWindow Window) opened)
        {
            var layer = opened.Window.GetParent();
            layer.QueueFree(); opened.World.QueueFree();
        }
        Button ButtonOf(Control root, string text) => Descendants(root).OfType<Button>().Single(b => b.Text == text && b.IsVisibleInTree());
        void Click(Control row) => row.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });

        // Nation chat: public channels with the joined channel, then a selection.
        {
            var opened = await Open("nation-public"); var (world, window) = opened;
            var rows = (VBoxContainer)DetailField(world, "_ncPublicRows")!;
            Require(Descendants(window).OfType<Button>().Single(b => b.Name == "nation_refresh").Text == "Refresh", "nation chat: refresh carries a word, not a glyph");
            await Capture(window, "nation-public");
            Require(rows.GetChildren().OfType<PanelContainer>().Count(r => r.GetMeta("social_selected", false).AsBool()) == 1, "nation chat: one selected channel row");
            Click(rows.GetChildren().OfType<PanelContainer>().First());
            await Frames();
            Require((int)DetailField(world, "_ncSelectedChannel")! == 1, "nation chat: a row click selects the channel natively");
            var live = rows.GetChildren().OfType<PanelContainer>().Where(r => !r.IsQueuedForDeletion()).ToArray();
            Require(live[0].GetMeta("social_selected", false).AsBool() && live[0].HasMeta(ClassicSocialStyle.Styled), "nation chat: the rebuilt selected row is styled Classic");
            Require(ButtonOf(window, "Leave").Visible && !Descendants(window).OfType<Button>().Any(b => b.Text == "Enter" && b.IsVisibleInTree()), "nation chat: joined channel shows Leave");
            await Capture(window, "nation-public-selected");
            Descendants(window).OfType<Button>().Single(b => b.Name == "social_close").EmitSignal(BaseButton.SignalName.Pressed);
            Require(!window.Visible && !(bool)DetailField(world, "_nationChatShown")!, "nation chat: the Classic close button runs the native close");
            Done(opened);
        }
        // Nation chat: private rooms as the owner, then after leaving (disabled owner actions).
        {
            var opened = await Open("nation-private"); var (world, window) = opened;
            await Capture(window, "nation-private");
            // 3277df5: "Open" shows the room of the joined channel or room, beside Leave in the Classic button row.
            var open = Descendants(window).OfType<Button>().Single(b => b.Name == "nation_private_open");
            var leave = ButtonOf(window, "Leave");
            Require(open.IsVisibleInTree() && open.Text == "Open" && open.GetThemeFont("font") == Plugin.Kit.Bold && open.Size.Y >= ClassicSocialLayout.ActionHeight, "nation chat: Open is a Classic action in an own room");
            Require(open.GetGlobalRect().Position.Y == leave.GetGlobalRect().Position.Y && open.GetGlobalRect().Position.X >= leave.GetGlobalRect().End.X, "nation chat: Open sits beside Leave");
            DetailCall(world, "ChatRoomInit");
            open.EmitSignal(BaseButton.SignalName.Pressed); await Frames(2);
            Require(((HudWindow)DetailField(world, "_roomWindow")!).Visible, "nation chat: Open shows the chat room natively");
            DetailCall(world, "ChatRoomDispose");
            Require(!ButtonOf(window, "Change options").Disabled && !ButtonOf(window, "Number of admissions").Disabled, "nation chat: owner actions enabled in an own room");
            offline.NationChat.Left(); DetailCall(world, "RefreshNationButtons"); await Frames();
            Require(ButtonOf(window, "Change options").Disabled && ButtonOf(window, "Number of admissions").Disabled, "nation chat: owner actions disabled outside a room");
            Require(!Descendants(window).OfType<Button>().Single(b => b.Name == "nation_private_open").IsVisibleInTree(), "nation chat: Open hidden outside a room");
            Require(ButtonOf(window, "Change options").GetThemeColor("font_disabled_color") == ClassicSocialStyle.ButtonDisabledInk, "nation chat: disabled caption keeps the Classic disabled ink");
            await Capture(window, "nation-private-left");
            Done(opened);
        }
        // Members: the room title and the client's summary line sit directly in the body (no header row since 3277df5).
        // fa3cfc3 removed the members window (members live in the room window) and the separate search window.
        bool membersWindow = typeof(World).GetField("_ncMembers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) != null;
        if (membersWindow)
        {
            var opened = await Open("nation-members"); var (world, window) = opened;
            KnightOnlineUiClassic.Native.NativeSocial.Prepare(window, world);
            var title = Descendants(window).OfType<Label>().Single(l => l.Name == "nation_members_title");
            var count = Descendants(window).OfType<Label>().Single(l => l.Name == "nation_members_count");
            var rows = Descendants(window).OfType<VBoxContainer>().Single(l => l.Name == "nation_member_rows");
            Require(title.GetParent() == window.Body && count.GetParent() == window.Body, "members: title and summary are body lines");
            var summary = typeof(LibreKO.Domain.NationChatRules).GetMethod("Summary")?.Invoke(null, [false, offline.NationChat.RoomPrivate, 4, 30]) as string;
            Require(count.Text == summary, "members: the summary is the client's text: " + count.Text);
            await Capture(window, "nation-members");
            Require(title.GetGlobalRect().End.Y <= count.GetGlobalRect().Position.Y && count.GetGlobalRect().End.Y <= rows.GetGlobalRect().Position.Y, "members: title, summary and list stack without overlap");
            Require(title.GetThemeFont("font") == Plugin.Kit.Bold && title.GetThemeColor("font_color") == ClassicDarkFrame.Heading, "members: the room title in the Classic heading");
            Done(opened);
        }
        foreach (var view in membersWindow ? new[] { "nation-create", "nation-password", "nation-search" } : new[] { "nation-create", "nation-password" })
        {
            var opened = await Open(view);
            await Capture(opened.Window, view);
            Done(opened);
        }
        // Commander: order bar in both directions.
        {
            var opened = await Open("commander"); var (world, window) = opened;
            var flip = Descendants(window).OfType<Button>().Single(b => b.Name == "commander_flip");
            Require(flip.Text == "Rotate", "commander: the direction toggle carries a word");
            await Capture(window, "commander");
            var slots = Descendants(window).OfType<Button>().Where(b => b.Text.Length == 0 && b.GetChildren().OfType<TextureRect>().Any()).ToArray();
            Require(slots.Length > 0 && slots.All(s => s.GetThemeStylebox("normal") is StyleBoxTexture), "commander: order slots sit in the Classic cell");
            GD.Print("SOCIAL_AUDIT commander icons with art: " + slots.Count(s => s.GetChildren().OfType<TextureRect>().Any(t => t.Texture != null)) + "/" + slots.Length);
            flip.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Require(DetailField(world, "_commanderSlots") is VBoxContainer, "commander: Rotate runs the native flip");
            await Capture(window, "commander-vertical");
            Done(opened);
        }
        foreach (var view in new[] { "clan-board", "clan-message", "clan-join-list", "wish-board", "wish-message", "wish-requests" })
        {
            var opened = await Open(view);
            await Capture(opened.Window, view);
            Done(opened);
        }
        // Leader transfer: the preselected heir enables Confirm; another pick moves the selection.
        {
            var opened = await Open("clanhandover"); var (world, window) = opened;
            var choices = (Button[])DetailField(world, "_handoverChoices")!;
            Require(choices.Count(c => c.Visible && c.ButtonPressed) == 1 && !ButtonOf(window, "Confirm").Disabled, "leader transfer: preselected heir enables Confirm");
            await Capture(window, "clanhandover");
            choices[0].ButtonPressed = true; await Frames();
            Require(choices[0].ButtonPressed && !choices[1].ButtonPressed, "leader transfer: picking another heir moves the selection natively");
            Require(choices[0].GetThemeStylebox("pressed") is StyleBoxFlat picked && picked.BorderColor == ClassicSocialStyle.SelectedEdge, "leader transfer: the picked heir carries the gold selection line");
            await Capture(window, "clanhandover-picked");
            Done(opened);
        }
        await CaptureSocialPrompts(nation, output, Require, Frames, screens);
        System.IO.File.WriteAllText(output + "/" + prefix + "-social.json", JsonSerializer.Serialize(new { nation, checks, screens }, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print("SOCIAL_AUDIT_OK " + prefix + " " + checks.Count + " checks / " + screens.Count + " screens");
        await Frames(); offline.Free();
    }
}
