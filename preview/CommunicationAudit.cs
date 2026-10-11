using Godot;
using LibreKO;
using LibreKO.Network;
using KnightOnlineUiClassic.Windows;
using System.Text.Json;

public partial class Preview
{
    private async Task CaptureCommunicationAudit(int nation)
    {
        var output = OS.GetEnvironment("LIBREKO_AUDIT_OUTPUT_DIR"); System.IO.Directory.CreateDirectory(output);
        GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Disabled; GetWindow().Size = new Vector2I(1000, 700);
        AddChild(new ColorRect { Color = new Color("252822"), Size = new Vector2(1000, 700), MouseFilter = MouseFilterEnum.Ignore });
        var offline = new Net(); typeof(Net).GetProperty("I")!.SetValue(null, offline);
        var checks = new List<string>(); var screens = new List<object>();
        void Require(bool value, string text) { if (!value) throw new Exception("COMMUNICATION_AUDIT: " + text); checks.Add(text); }
        async Task Frames(int count = 8) { for (int i = 0; i < count; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
        async Task KeyInput(Key key)
        {
            GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
            await Frames(1);
            GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false }); await Frames();
        }
        async Task Click(Control control)
        {
            var point = control.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point, GlobalPosition = point });
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point }); await Frames();
        }
        async Task Capture(HudWindow window, string state)
        {
            await Frames(); window.Position = ((GetViewportRect().Size - window.Size) / 2).Round(); await Frames(2);
            var panel = window.GetChildren().OfType<ClassicCommunicationPanel>().Single();
            Require(window.Size == panel.CustomMinimumSize, state + " fixed frame size");
            Require(GetViewportRect().Encloses(window.GetGlobalRect()), state + " complete window fits viewport");
            foreach (var control in Descendants(panel).OfType<Control>().Where(c => c.IsVisibleInTree() && c.HasMeta("communication_expected_rect")))
            {
                Require(control.GetRect() == control.GetMeta("communication_expected_rect").AsRect2(), state + " actual control bounds " + control.Name + " " + control.GetRect());
                Require(control.GetParent() is Control parent && new Rect2(Vector2.Zero, parent.Size).Encloses(control.GetRect()), state + " control stays inside its composition " + control.Name);
            }
            foreach (var text in Descendants(panel).OfType<Label>().Where(l => l.IsVisibleInTree() && l.Name == "communication_body"))
            {
                Require(text.Size.Y >= 15 && text.Size.X >= 260, state + " native message has readable actual bounds " + text.Size);
                Require(!text.ClipText && text.AutowrapMode == TextServer.AutowrapMode.WordSmart, state + " wrapped message contributes its real text height");
            }
            foreach (var edit in Descendants(panel).OfType<LineEdit>()) Require(edit.TextDirection == Control.TextDirection.Ltr, state + " LTR input " + edit.Name);
            string file = (nation == 1 ? "karus" : "human") + "-" + state + ".png";
            GetViewport().GetTexture().GetImage().SavePng(output + "/" + file); screens.Add(new { state, file, bounds = window.GetGlobalRect().ToString() });
        }
        // TODO(port-2619): the "chatrooms" pass (room list, log cap, join/leave states) drove the chat-room
        // window that upstream cd63f98 replaced with the nation chat rooms ("nation_chat"); restore an
        // equivalent pass against the nation chat windows when the Classic skin is retargeted.
        foreach (string id in new[] { "messenger" })
        {
            var world = new World(); var layer = (CanvasLayer)DetailCall(world, "BuildCommunicationUiPreview", id)!; AddChild(layer);
            var window = layer.GetChildren().OfType<HudWindow>().Single(); var panel = ClassicCommunicationSkin.Apply(window.Body)!;
            await Frames(); window.Position = new Vector2(50, 30); await Frames(2);
            var grip = Descendants(panel).OfType<Control>().Single(c => c.Name == "party_drag"); var start = window.Position;
            grip.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, GlobalPosition = start + new Vector2(30, 15) });
            window.Layout._Input(new InputEventMouseMotion { GlobalPosition = start + new Vector2(63, 36) });
            window.Layout._Input(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
            Require(window.Position == start + new Vector2(33, 21), id + " header forwards native dragging");
            await Capture(window, id);
            if (id == "messenger")
            {
                var rows = (VBoxContainer)DetailField(world, "_msgrList")!; var to = (LineEdit)DetailField(world, "_msgrToInput")!;
                var message = (LineEdit)DetailField(world, "_msgrTextInput")!;
                Require(to.MaxLength == 20 && message.MaxLength == 128, "Messenger retains native input limits");
                Require(rows.GetChildren().OfType<PanelContainer>().Count() == 3, "Messenger preserves online and offline rows");
                var action = Descendants(rows).OfType<Button>().First(); await Click(action);
                Require(to.Text == "Rikka" && message.HasFocus(), "Whisper click selects native recipient and focuses message");
                to.GrabFocus(); await KeyInput(Key.Enter); Require(message.HasFocus(), "Recipient Enter advances to message without sending");
                message.Text = "Ready for the hunt."; await Capture(window, "whisper-ready"); await KeyInput(Key.Enter);
                Require(message.Text == "" && to.Text == "Rikka", "Message Enter follows native send and clears only message");
                message.Text = "  "; DetailCall(world, "DoMessengerSend"); Require(message.Text == "  ", "Blank message does not send");
                to.Text = ""; message.Text = "Keep draft"; DetailCall(world, "DoMessengerSend"); Require(message.Text == "Keep draft", "Missing recipient does not send");
                var many = new List<MessengerBuddy>(); for (int i = 0; i < 24; i++) many.Add(new MessengerBuddy { CharId = i + 1, Name = "LongCharacterName1299", Online = i % 2 == 0 });
                DetailCall(world, "OnMessengerList", many); await Frames();
                Require(rows.GetChildCount() == 24, "Refresh immediately replaces old buddy children");
                Require(Descendants(panel).OfType<ScrollContainer>().Single().GetVScrollBar().Visible, "Long roster scrolls inside fixed viewport");
                await Capture(window, "messenger-full");
                DetailCall(world, "OnMessengerList", new List<MessengerBuddy>()); Require(rows.GetChildCount() == 1 && rows.GetChild(0) is Label { Text: "No one else online." }, "Empty buddy response has immediate empty state");
                await Capture(window, "messenger-empty"); to.GrabFocus(); await KeyInput(Key.Escape);
                Require(!window.Visible && !(bool)DetailField(world, "_msgrShown")!, "Focused recipient Escape follows native Messenger close");
                DetailCall(world, "MessengerDispose");
            }
            layer.Free(); world.Free();
        }
        System.IO.File.WriteAllText(output + "/" + (nation == 1 ? "karus" : "human") + "-communication.json", JsonSerializer.Serialize(new { nation, checks, screens }, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print("Classic communication audit: " + checks.Count + " checks / " + screens.Count + " screens"); offline.Free();
    }
}
