using Godot;
using LibreKO;
using LibreKO.Network;
using KnightOnlineUiClassic;
using KnightOnlineUiClassic.Windows;
using System.Text.Json;
using Spec = KnightOnlineUiClassic.Layout.ClassicEventFamilyLayout;

// The event, ranking and war windows the client builds from its own rows (event schedule, daily and board
// rankings, Border Defense War result and ranking, Chaos Dungeon, Monster Stone, Wanted, siege war, Knight
// Royale). Each is built by the client's preview builder (or a fixture), given the Classic family skin and
// captured for both nations; the audit checks the frame, the actual bounds of every visible control, the
// type and materials, the close button and that the native buttons keep their callbacks.
public partial class Preview
{
    private async Task CaptureEventWindowsAudit(int nation)
    {
        var output = OS.GetEnvironment("LIBREKO_AUDIT_OUTPUT_DIR"); System.IO.Directory.CreateDirectory(output);
        ProjectSettings.LoadResourcePack(ProjectSettings.GlobalizePath("res://../../build/client/source-content/knightonline.pck"), false);
        GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Disabled; GetWindow().Size = new Vector2I(1280, 800);
        AddChild(new ColorRect { Color = new Color("252822"), Size = new Vector2(1280, 800), MouseFilter = MouseFilterEnum.Ignore });
        var offline = new Net(); typeof(Net).GetProperty("I")!.SetValue(null, offline);
        Native.Call(offline, "SeedPreviewEnter", new MyInfo { Nation = nation, Name = "Classic Preview" });
        string prefix = nation == 1 ? "karus" : "human";
        var checks = new List<string>(); var screens = new List<object>();
        void Require(bool condition, string text) { if (!condition) throw new Exception("EVENT_WINDOWS_AUDIT: " + text); checks.Add(text); }
        async Task Frames(int count = 8) { for (int i = 0; i < count; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
        var viewport = GetViewportRect();
        bool Inside(Rect2 outer, Rect2 inner) => outer.Grow(.5f).Encloses(inner);
        float TextWidth(Control control, string text) => control.GetThemeFont("font").GetStringSize(text, HorizontalAlignment.Left, -1, control.GetThemeFontSize("font_size")).X;
        bool Connected(BaseButton button) => button.GetSignalConnectionList(BaseButton.SignalName.Pressed).Count > 0;
        var palette = new[] { ClassicDesign.FrameTitle, ClassicEventPanel.Caption, ClassicEventPanel.Ink, ClassicEventPanel.Muted, ClassicEventPanel.Ready,
            ClassicEventPanel.Refused, new Color("ffe094"), new Color("9cd8f0"), new Color("efd9b4"), ClassicDesign.Muted };

        async Task<(World World, HudWindow Window)> Open(string fixture, params object?[] args)
        {
            var world = new World();
            var window = (HudWindow)DetailCall(world, fixture, args)!;
            AddChild(window);
            window.Visible = true;
            var frame = ClassicEventFamily.Apply(window.Body);
            Require(frame != null && !frame.Ornate, window.Id + " gets the plain Classic frame");
            await Frames();
            return (world, window);
        }

        async Task Capture(HudWindow window, string state)
        {
            await Frames(); window.Position = ((viewport.Size - window.Size) / 2).Round(); await Frames(3);
            string tag = window.Id + " " + state;
            var box = window.GetGlobalRect();
            Require(Inside(viewport, box), tag + " complete window fits the viewport " + box);
            var frame = window.GetNode<ClassicFrame>("classic_frame");
            Require(frame.Size == window.Size && frame.Position == Vector2.Zero, tag + " frame covers the window");
            Require(window.GetThemeStylebox("panel") is StyleBoxEmpty, tag + " no modern window panel");
            // Everything visible sits inside the frame's straight line, with the even content margin of the family.
            var inner = box.Grow(-(ClassicFrame.PlainEdge - 2));
            foreach (var control in Descendants(window).OfType<Control>().Where(c => c.IsVisibleInTree() && c != window && c != frame && c.Size.X > 0 && c.Size.Y > 0
                && c is Label or Button or ItemSlotView or LineEdit or ProgressBar or TextureRect))
                Require(Inside(inner, control.GetGlobalRect()), tag + " inside the frame: " + control.GetType().Name + " '" + (control as Label)?.Text + (control as Button)?.Text + "' " + control.GetGlobalRect());
            foreach (var label in Descendants(window.Body).OfType<Label>().Where(l => l.IsVisibleInTree() && l.Text.Length > 0))
            {
                Require(label.GetThemeFont("font") == Plugin.Kit.Bold, tag + " Arial Bold: '" + label.Text + "'");
                var ink = label.GetThemeColor("font_color");
                Require(palette.Any(c => c.IsEqualApprox(new Color(ink, 1))) || label.GetMeta("classic_native_ink", false).AsBool(), tag + " Classic ink " + ink.ToHtml() + ": '" + label.Text + "'");
                if (label.AutowrapMode == TextServer.AutowrapMode.Off && !label.ClipText)
                    Require(TextWidth(label, label.Text) <= label.Size.X + 1, tag + " text fits: '" + label.Text + "' " + TextWidth(label, label.Text) + " in " + label.Size.X);
                if (label.AutowrapMode != TextServer.AutowrapMode.Off)
                    Require(label.GetLineCount() * label.GetLineHeight() <= label.Size.Y + 2, tag + " wrapped text fits: '" + label.Text[..Math.Min(24, label.Text.Length)] + "'");
            }
            foreach (var button in Descendants(window.Body).OfType<Button>().Where(b => b.IsVisibleInTree() && b.Text.Length > 0 && b is not CheckBox and not CheckButton))
            {
                Require(button.GetThemeStylebox("normal") is StyleBoxTexture && button.GetThemeStylebox("hover") is StyleBoxTexture
                    && button.GetThemeStylebox("disabled") is StyleBoxTexture, tag + " Classic button art: " + button.Text);
                Require(button.Size.Y >= Spec.Button && TextWidth(button, button.Text) <= button.Size.X - 4, tag + " button fits its caption: " + button.Text + " " + button.Size);
            }
            foreach (var check in Descendants(window.Body).OfType<Button>().Where(b => b.IsVisibleInTree() && b.Text.Length > 0 && b is CheckBox or CheckButton))
                Require(check.GetThemeFont("font") == Plugin.Kit.Bold && check.GetThemeColor("font_color") == ClassicDarkFrame.Value,
                    tag + " check caption in Arial Bold and the light ink of both nations: " + check.Text + " " + check.GetThemeFont("font")?.ResourcePath + " " + check.GetThemeColor("font_color").ToHtml());
            foreach (var panel in Descendants(window.Body).OfType<PanelContainer>().Where(p => p.IsVisibleInTree() && p is not ItemSlotView))
                Require(panel.GetThemeStylebox("panel") is not StyleBoxFlat flat || flat.BgColor.A == 0 || flat.BgColor.IsEqualApprox(ClassicDarkFrame.SelectedRow),
                    tag + " no modern panel fill");
            foreach (var cell in Descendants(window.Body).OfType<ItemSlotView>().Where(c => c.IsVisibleInTree()))
                Require(cell.GetThemeStylebox("panel") is StyleBoxTexture, tag + " item cell on the inventory cell art");
            if (window.Header?.GetChildren().OfType<HBoxContainer>().FirstOrDefault() is { } bar)
            {
                var title = bar.GetChildren().OfType<Label>().Last();
                Require(title.IsVisibleInTree() && title.GetThemeFont("font") == Plugin.Kit.Bold && title.GetThemeColor("font_color") == ClassicDesign.FrameTitle, tag + " gold Arial Bold title");
                Require(Mathf.Abs(title.GetGlobalRect().Position.X - box.Position.X - Spec.Margin) < 1, tag + " title on the content edge");
                if (bar.GetChildren().OfType<Button>().FirstOrDefault(b => b.TooltipText == "Close") is { Visible: true } close)
                {
                    Require(close.Size == Spec.Close && close.GetThemeStylebox("normal") is StyleBoxTexture, tag + " original close knob");
                    Require(Mathf.Abs(box.End.X - close.GetGlobalRect().End.X - Spec.Margin) < 1, tag + " close knob 14 pixels from the right edge");
                }
            }
            string file = prefix + "-" + window.Id + "-" + state + ".png";
            GetViewport().GetTexture().GetImage().SavePng(output + "/" + file);
            screens.Add(new { window = window.Id, state, file, bounds = box.ToString() });
        }

        async Task Close(World world, HudWindow window)
        {
            var close = window.Header?.GetChildren().OfType<HBoxContainer>().First().GetChildren().OfType<Button>().FirstOrDefault(b => b.TooltipText == "Close");
            if (close is { Visible: true })
            {
                close.EmitSignal(BaseButton.SignalName.Pressed);
                Require(!window.Visible, window.Id + " Close runs the native close");
            }
            window.QueueFree(); world.QueueFree(); await Frames(2);
        }

        async Task Hover(Control control, HudWindow window, string state)
        {
            var at = control.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }); await Frames(3);
            await Capture(window, state);
            GetViewport().PushInput(new InputEventMouseMotion { Position = Vector2.One, GlobalPosition = Vector2.One }); await Frames(2);
        }

        // Event schedule.
        {
            var (world, window) = await Open("BuildEventsUiPreview");
            var buttons = Descendants(window.Body).OfType<Button>().Where(b => b.Text is "Join" or "Open").ToArray();
            Require(buttons.Length == 1 && buttons.All(Connected), "events: the sign-up row keeps its native Join action");
            await Capture(window, "schedule");
            await Hover(buttons[0], window, "join-hover");
            await Close(world, window);
            (world, window) = await Open("BuildEventsScheduleUiPreview", "loading");
            await Capture(window, "loading");
            await Close(world, window);
        }
        // Daily rank and the rank board.
        foreach (var view in new[] { "daily-rank", "daily-rank-clan" })
        {
            var (world, window) = await Open("BuildDailyRankBoardsUiPreview", view);
            Require(Native.Get<OptionButton>(world, "_drPicker")!.Text.Length > 0, "daily_rank: the board picker shows the chosen board");
            await Capture(window, view == "daily-rank" ? "players" : "clans");
            await Close(world, window);
        }
        {
            var (world, window) = await Open("BuildRankBoardUiPreview");
            await Capture(window, "filled");
            await Close(world, window);
        }
        // Border Defense War result and ranking.
        {
            var (world, window) = await Open("BuildWarResultUiPreview");
            await Capture(window, "finished");
            await Close(world, window);
            (world, window) = await Open("BuildWarRankingUiPreview");
            await Capture(window, "filled");
            await Close(world, window);
        }
        // Chaos Dungeon.
        foreach (bool final in new[] { false, true })
        {
            var (world, window) = await Open("BuildChaosLadderUiPreview", final);
            await Capture(window, final ? "final" : "running");
            await Close(world, window);
        }
        {
            var (world, window) = await Open("BuildChaosSkillPageUiPreview");
            await Capture(window, "page");
            await Close(world, window);
            (world, window) = await Open("BuildChaosGuideUiPreview");
            await Capture(window, "guide");
            await Close(world, window);
        }
        // Monster Stone.
        foreach (var view in new[] { "short", "long" })
        {
            var (world, window) = await Open("BuildNestUiPreview", view);
            var actions = Descendants(window.Body).OfType<Button>().Where(b => b.Text.Length > 0).ToArray();
            Require(actions.Length == 3 && actions.All(Connected), "nestindun: Enter, Receive EXP and Cancel keep their native actions");
            await Capture(window, view);
            var labels = Descendants(window.Body).OfType<Label>().Where(l => l.IsVisibleInTree()).ToArray();
            var description = labels.First(l => l.AutowrapMode != TextServer.AutowrapMode.Off);
            var question = labels.First(l => l.GetGlobalRect().Position.Y > description.GetGlobalRect().Position.Y);
            float band = question.GetGlobalRect().Position.Y - description.GetGlobalRect().End.Y;
            Require(band <= 12, $"nestindun {view}: the question follows the description without an empty band ({band:0} px)");
            await Close(world, window);
        }
        // Wanted.
        foreach (var (mode, view) in new[] { (1, "invite"), (3, "invite-mini"), (2, "list"), (4, "list-mini") })
        {
            var (world, window) = await Open("BuildVanguardUiPreview", mode);
            var actions = Descendants(window.Body).OfType<Button>().Where(b => b.Text.Length > 0 && b is not CheckBox).ToArray();
            if (view == "list") Require(actions.Length == 0 && Descendants(window.Body).OfType<CheckBox>().Single().GetSignalConnectionList(BaseButton.SignalName.Toggled).Count > 0,
                window.Id + ": the Wanted list keeps its native hide option");
            else Require(actions.Length > 0 && actions.All(Connected), window.Id + ": Accept, Reject and Detail keep their native actions");
            await Capture(window, view);
            await Close(world, window);
        }
        // Castle siege war and Knight Royale.
        foreach (var view in new[] { "unheld", "held" })
        {
            var (world, window) = view == "held" ? await Open("BuildSiegeWarUiPreview") : await Open("BuildSiegeWarStateUiPreview", view);
            await Capture(window, view);
            await Close(world, window);
        }
        {
            var (world, window) = await Open("BuildRoyaleScoreUiPreview");
            await Capture(window, "running");
            await Close(world, window);
        }

        System.IO.File.WriteAllText(output + "/" + prefix + "-event-windows.json",
            JsonSerializer.Serialize(new { nation, checks, screens }, new JsonSerializerOptions { WriteIndented = true }));
        offline.Free(); GD.Print("EVENT_WINDOWS_AUDIT_OK " + checks.Count);
    }
}
