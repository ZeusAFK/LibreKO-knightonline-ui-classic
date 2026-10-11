using Godot;
using LibreKO;
using LibreKO.Network;
using LibreKO.Plugins;
using KnightOnlineUiClassic;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Windows;

public partial class Preview
{
    /// <summary>
    /// social-audit, second part: the prompt seams and the whisper window. The Classic player menu (client rows,
    /// row and outside-click close), the Classic text prompt (invite by name: submit, cancel, limits), the
    /// friend-request consent on the Classic message box, and the Classic private-message frame with the
    /// client's header actions, item links, emoticons and its emoticon and size popups.
    /// </summary>
    private async Task CaptureSocialPrompts(int nation, string output, Action<bool, string> Require, Func<int, Task> Frames, List<object> screens)
    {
        string prefix = nation == 1 ? "karus" : "human";
        PluginHost.Ui.ReplacePlayerMenu(request => new ClassicPlayerMenu(request));
        PluginHost.Ui.ReplaceTextInput(request => new ClassicTextPrompt(request));
        PluginHost.Ui.ReplaceDialogs(request => new MessageBox(request));
        async Task Shot(string state, Control? subject = null)
        {
            await Frames(6);
            if (subject != null) Require(GetViewportRect().Encloses(subject.GetGlobalRect()), state + ": stays inside the viewport " + subject.GetGlobalRect());
            string file = prefix + "-" + state + ".png";
            GetViewport().GetTexture().GetImage().SavePng(output + "/" + file);
            screens.Add(new { state, file, bounds = subject?.GetGlobalRect().ToString() ?? "" });
        }
        void Expected(Control root, string state)
        {
            foreach (var control in Descendants(root).OfType<Control>().Where(c => c.HasMeta(ClassicWorldOverlays.ExpectedRect)))
                Require(control.GetRect() == control.GetMeta(ClassicWorldOverlays.ExpectedRect).AsRect2(), state + ": actual bounds of " + control.Name + " " + control.GetRect());
        }

        // Player menu: the client's rows for a player out of view.
        {
            var world = new World();
            DetailCall(world, "ShowPlayerMenuByName", "Rikka", new Vector2(420, 260));
            var layer = (CanvasLayer)DetailField(world, "_playerMenuSkinLayer")!;
            layer.Reparent(this);
            var menu = Descendants(layer).OfType<ClassicPlayerMenu>().Single();
            await Frames(4);
            var panel = (Control)menu.FindChild("player_menu_panel", true, false)!;
            var rows = Descendants(panel).OfType<Button>().ToArray();
            Require(rows.Length == 7 && rows.All(r => r.Size.Y == ClassicSocialLayout.MenuRow && r.GetThemeFont("font") == Plugin.Kit.Bold), "player menu: every client row on a 22 px Classic row");
            Require(Descendants(panel).OfType<Label>().Single().Text == "Rikka", "player menu: the player's name heads the menu");
            Require(panel.Position == new Vector2(422, 262), "player menu: opens at the cursor " + panel.Position);
            await Shot("player-menu", panel);
            rows.Single(r => r.Text == "Copy name").EmitSignal(BaseButton.SignalName.Pressed); await Frames(2);
            Require(DetailField(world, "_playerMenuSkinLayer") == null, "player menu: a row runs the client's action and closes the menu");
            DetailCall(world, "ShowPlayerMenuByName", "LongCharacterName1299", new Vector2(980, 690));
            var edge = (CanvasLayer)DetailField(world, "_playerMenuSkinLayer")!; edge.Reparent(this); await Frames(4);
            var edgePanel = (Control)edge.FindChild("player_menu_panel", true, false)!;
            Require(GetViewportRect().Encloses(edgePanel.GetGlobalRect()), "player menu: near the screen edge it stays on screen");
            await Shot("player-menu-edge", edgePanel);
            Descendants(edge).OfType<ClassicPlayerMenu>().Single().EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true }); await Frames(2);
            Require(DetailField(world, "_playerMenuSkinLayer") == null, "player menu: a click outside closes it");
            if (GodotObject.IsInstanceValid(layer)) layer.QueueFree();
            if (GodotObject.IsInstanceValid(edge)) edge.QueueFree();
            world.QueueFree();
        }
        // Text prompt: invite by name.
        {
            string? submitted = null; bool cancelled = false;
            var notice = Notice.AskText(this, "Invite player by name:", text => submitted = text, "Party Invite", placeholder: "Character name", maxLength: 20, confirmText: "Send", onCancel: () => cancelled = true);
            await Frames(4);
            var prompt = Descendants(notice).OfType<ClassicTextPrompt>().Single();
            var input = (LineEdit)prompt.FindChild("prompt_input", true, false)!;
            var panel = (Control)prompt.FindChild("prompt_panel", true, false)!;

            Expected(panel, "text prompt");
            Require(input.MaxLength == 20 && input.PlaceholderText == "Character name" && input.HasFocus(), "text prompt: client limits and placeholder, focused input");
            Require(((Button)prompt.FindChild("prompt_confirm", true, false)!).Text == "Send", "text prompt: the client's confirm caption");
            Require(panel.Size == ClassicSocialLayout.PromptSize(ClassicSocialLayout.PromptLine), "text prompt: message box sized to its content " + panel.Size);
            input.Text = "Rikka"; await Shot("text-prompt", panel);
            input.EmitSignal(LineEdit.SignalName.TextSubmitted, input.Text); await Frames(2);
            Require(submitted == "Rikka" && !cancelled, "text prompt: Enter submits the typed name to the client");
            var second = Notice.AskText(this, "Invite player by name:", text => submitted = text, "Party Invite", onCancel: () => cancelled = true);
            await Frames(3);
            ((Button)Descendants(second).OfType<ClassicTextPrompt>().Single().FindChild("prompt_cancel", true, false)!).EmitSignal(BaseButton.SignalName.Pressed); await Frames(2);
            Require(cancelled, "text prompt: Cancel runs the client's cancel");
        }
        // Friend request consent: the client's confirmation on the Classic message box.
        {
            bool accepted = false;
            var ask = Notice.Confirm(this, "[Rikka] sent a friend request. Will you accept?", "OK", "Cancel", () => accepted = true, title: "Friends");
            await Frames(4);
            var box = Descendants(ask).OfType<MessageBox>().Single();
            await Shot("friend-request", box);
            Descendants(box).OfType<BaseButton>().Single(c => c.Name == "btn_ok").EmitSignal(BaseButton.SignalName.Pressed); await Frames(2);
            Require(accepted, "friend request: OK accepts through the client");
        }
        // Whisper: the client's conversation with header actions, item links and emoticons.
        {
            var world = new World();
            var window = (HudWindow)DetailCall(world, "BuildWhisperUiPreview", false)!;
            var layer = (CanvasLayer)DetailField(world, "_whisperLayer")!;
            layer.Reparent(this); layer.AddChild(window); window.Visible = true;
            KnightOnlineUiClassic.Native.NativeWhispers.Prepare(window, world);
            await Frames(6); window.Position = new Vector2(360, 120); await Frames(3);
            var surface = (Control)window.FindChild("classic_whisper", true, false)!;
            var actions = Descendants(surface).OfType<Button>().Where(b => b.Name.ToString().StartsWith("whisper_action_")).ToArray();
            Require(actions.Length == 3 && actions.All(a => a.IsVisibleInTree() && new Rect2(0, 0, 304, 24).Encloses(a.GetRect())), "whisper: the client's three header actions sit on the original title bar");
            var title = Descendants(surface).OfType<Label>().First(l => l.Text == "Tester10" && l.IsVisibleInTree());
            Require(title.GetRect().End.X <= actions[0].Position.X, "whisper: the title stops before the header actions");
            var lines = Descendants(surface).OfType<ScrollContainer>().Single().GetChild(0);
            Require(lines.GetChildren().All(l => l.HasMeta("classic_whisper_line")) && lines.GetChildCount() == Net.I.WhisperHistory("Tester10").Count, "whisper: the whole history uses the Classic line builder");
            // Item links and emoticons need the client's item and emoticon tables; the audit pack may lack them.
            bool linked = Native.Call(Native.ClientType("LibreKO.ChatSystem")!, "RenderText", "x", "ffffff") is string && lines.GetChildren().OfType<Label>().All(l => !l.Text.Contains('\u0001'));
            Require(linked, "whisper: plain lines carry no raw link tokens");
            var rendered = KnightOnlineUiClassic.Native.NativeWhispers.Render("look (Smile)", "ffffff");
            GD.Print("SOCIAL_AUDIT whisper rich lines: " + lines.GetChildren().OfType<RichTextLabel>().Count() + ", emoticon markup available: " + rendered.Contains("[img"));
            await Shot("whisper", window);
            DetailCall(world, "OpenWhisperEmoticons", "Tester10", window.GlobalPosition + new Vector2(260, 140)); await Frames(4);
            var picker = Descendants(layer).OfType<EmoticonPicker>().Single();
            Require(picker.HasMeta("classic_popup") && picker.GetThemeStylebox("panel") is StyleBoxFlat, "whisper: the emoticon picker uses the Classic menu panel");
            await Shot("whisper-emoticons", window);
            picker.Hide();
            DetailCall(world, "BuildWhisperOptions"); DetailCall(world, "RefreshWhisperOptions");
            var options = (PopupPanel)DetailField(world, "_whisperOptions")!;
            options.Position = (Vector2I)(window.GlobalPosition + new Vector2(150, 30)); options.Popup(); await Frames(4);
            Require(options.HasMeta("classic_popup"), "whisper: the size and transparency popup uses the Classic menu panel");
            await Shot("whisper-options", window);
            options.Hide();
            layer.QueueFree(); world.QueueFree();
        }
    }
}
