using Godot;
using LibreKO;
using LibreKO.Network;
using LibreKO.Plugins;
using System.Reflection;
using System.Text.Json;
using KnightOnlineUiClassic;
using KnightOnlineUiClassic.Windows;
using Spec = KnightOnlineUiClassic.Layout.ClassicOverlayLayout;

public partial class Preview
{
    private async Task CaptureForgottenTempleAudit(int nation)
    {
        string output = OS.GetEnvironment("LIBREKO_AUDIT_OUTPUT_DIR");
        if (output.Length == 0)
            output = ProjectSettings.GlobalizePath("res://../../research/forgotten-temple-integration/events");
        System.IO.Directory.CreateDirectory(output);
        GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
        GetWindow().Size = new Vector2I(1280, 800);
        AddChild(new ColorRect { Color = new Color("252822"), Size = new Vector2(1280, 800), MouseFilter = MouseFilterEnum.Ignore });
        var net = new Net();
        typeof(Net).GetProperty("I")!.SetValue(null, net);
        var world = new World();
        DetailCall(world, "BuildBifrostUi");
        DetailCall(world, "BuildInZoneLeaveUi");
        foreach (var layer in world.GetChildren().OfType<CanvasLayer>().ToArray()) layer.Reparent(this);
        PluginHost.Ui.ReplaceDialogs(request => new KnightOnlineUiClassic.Windows.MessageBox(request));
        NativeOverlays.Prepare(world);
        var checks = new List<string>();
        var screenshots = new List<object>();
        void Require(bool valid, string message)
        {
            if (!valid) throw new Exception(message);
            checks.Add(message);
        }
        async Task Frames()
        {
            for (int i = 0; i < 10; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
        async Task Capture(string state)
        {
            await Frames();
            string file = (nation == 1 ? "karus" : "human") + "-" + state + ".png";
            GetViewport().GetTexture().GetImage().SavePng(output + "/" + file);
            var bounds = Descendants(this).OfType<Control>().Where(c => c.IsVisibleInTree())
                .Select(c => new { name = c.Name.ToString(), type = c.GetType().Name, x = c.GlobalPosition.X, y = c.GlobalPosition.Y, w = c.Size.X, h = c.Size.Y }).ToArray();
            screenshots.Add(new { file, state, bounds });
        }
        var viewport = GetViewportRect();
        bool Whole(Rect2 r) => r.Position == r.Position.Round() && r.Size == r.Size.Round();
        bool Fits(Label label)
        {
            var font = label.GetThemeFont("font"); int size = label.GetThemeFontSize("font_size");
            if (label.AutowrapMode == TextServer.AutowrapMode.Off)
                return font.GetStringSize(label.Text, HorizontalAlignment.Left, -1, size).X <= label.Size.X + 0.5f;
            int lines = label.GetLineCount();
            return lines * font.GetHeight(size) + (lines - 1) * label.GetThemeConstant("line_spacing") <= label.Size.Y + 0.5f;
        }
        void Expected(Control control, Control parent, string what)
        {
            var expected = control.GetMeta(ClassicWorldOverlays.ExpectedRect).AsRect2();
            var actual = new Rect2(control.GlobalPosition - parent.GlobalPosition, control.Size);
            Require(actual == expected && Whole(control.GetGlobalRect()), $"{what}: rendered bounds {actual} match the declared {expected}");
        }
        void Art(Button button, string text, string what)
        {
            var caption = button.GetNodeOrNull<Label>("caption");
            Require(caption != null && caption.Text == text && button.Text == text && Fits(caption), $"{what}: the native '{text}' on the art caption");
            Require(button.GetThemeStylebox("normal") is StyleBoxTexture && button.GetThemeStylebox("hover") is StyleBoxTexture
                && button.GetThemeStylebox("pressed") is StyleBoxTexture, $"{what}: original button art in every state");
        }
        var zone = typeof(World).GetField("_zone", BindingFlags.Instance | BindingFlags.NonPublic)!;
        void SetZone(int value) => zone.SetValue(world, Convert.ChangeType(value, zone.FieldType));
        var banner = (Control)DetailField(world, "_inZoneLeaveBanner")!;
        var title = (Label)DetailField(world, "_inZoneLeaveTitleLbl")!;
        DetailCall(world, "OnBifrostTime", 600, TempleEventType.ForgottenTemple);
        var join = (Control)DetailField(world, "_joinModal")!;
        var joinTitle = (Label)DetailField(world, "_joinModalTitle")!;
        Require(join.Visible && joinTitle.Text.Contains("FORGOTTEN TEMPLE"), "FT registration uses its event title and existing join UI");
        var countdown = (PanelContainer)DetailField(world, "_bifrostBanner")!;
        var joinButton = (Button)DetailField(world, "_joinModalBtn")!;
        var closeButton = (Button)DetailField(world, "_joinModalCloseBtn")!;
        var timerLabel = (Label)DetailField(world, "_joinModalTimer")!;
        var statusLabel = (Label)DetailField(world, "_joinModalStatus")!;
        var bar = (ProgressBar)DetailField(world, "_joinModalBar")!;
        var countdownTimer = (Label)DetailField(world, "_bifrostTimerLbl")!;
        var countdownBar = (ProgressBar)DetailField(world, "_bifrostBar")!;
        async Task Registration(string state, bool urgent, bool registered)
        {
            await Frames();
            countdown.Position = new Vector2(Mathf.Round((viewport.Size.X - countdown.Size.X) / 2), 24);
            await Frames();
            var panel = join.GetNode<Control>("classic_registration");
            Require(!join.GetChildren().OfType<CenterContainer>().Single().Visible, $"{state}: the flat native panel is hidden");
            Require(panel.Size == Spec.Registration.Size && panel.GlobalPosition == ((viewport.Size - panel.Size) / 2).Round(), $"{state}: centred message box {panel.GetGlobalRect()}");
            foreach (var label in new[] { joinTitle, timerLabel, statusLabel }) { Expected(label, panel, $"{state} '{label.Text}'"); Require(Fits(label), $"{state}: '{label.Text}' fits"); }
            Expected(bar, panel, state + " bar"); Expected(joinButton, panel, state + " join"); Expected(closeButton, panel, state + " close");
            Require(joinButton.Size == Spec.Message.Button && closeButton.Size == Spec.Message.Button, $"{state}: 97 x 29 message buttons");
            Art(joinButton, registered ? "Cancel" : "Join", state + " join"); Art(closeButton, "Close", state + " close");
            Require(timerLabel.GetThemeColor("font_color") == (urgent ? ClassicWorldOverlays.UrgentOnMessage : ClassicWorldOverlays.MessageText), $"{state}: {(urgent ? "urgent" : "calm")} countdown ink");
            Require(((StyleBoxTexture)bar.GetThemeStylebox("fill")).RegionRect.Position.Y == (urgent ? 462 : 389), $"{state}: {(urgent ? "red" : "orange")} original bar fill");
            Require(statusLabel.GetThemeColor("font_color") == (registered ? ClassicEventPanel.Ready : ClassicDesign.Text), $"{state}: {(registered ? "registered" : "open")} status ink");
            Require(countdown.Visible && countdown.Size == Spec.Countdown.Size && viewport.Encloses(countdown.GetGlobalRect()), $"{state}: countdown plaque {countdown.Size}");
            var surface = countdown.GetNode<Control>("classic_countdown");
            foreach (var control in new Control[] { (Label)DetailField(world, "_bifrostTitleLbl")!, countdownTimer, countdownBar })
            {
                Expected(control, surface, $"{state} countdown {control.Name}");
                if (control is Label label) Require(Fits(label), $"{state}: countdown '{label.Text}' fits");
            }
            Require(countdownTimer.GetThemeColor("font_color") == (urgent ? ClassicWorldOverlays.UrgentOnPlaque : ClassicWorldOverlays.PlaqueInk), $"{state}: countdown plaque ink");
            await Capture(state);
        }
        await Registration("ft-registration", false, false);
        joinButton.EmitSignal(BaseButton.SignalName.Pressed);
        Require((bool)DetailField(world, "_isEventRegistered")! && joinButton.Text == "Cancel", "Join runs the native registration toggle");
        DetailCall(world, "OnBifrostJoinResult", LibreKO.Domain.TempleJoin.Joined, 55);
        Require(((Button)DetailField(world, "_joinModalBtn")!).Text == "Cancel", "Joining FT updates registration state");
        await Registration("ft-registered", false, true);
        DetailCall(world, "OnBifrostTime", 20, TempleEventType.ForgottenTemple);
        await Registration("ft-registration-urgent", true, true);
        closeButton.EmitSignal(BaseButton.SignalName.Pressed);
        Require(!join.Visible && countdown.Visible, "Close runs the native close and keeps the countdown plaque");
        await Capture("ft-countdown");
        DetailCall(world, "EndBifrostEvent");
        SetZone(55);
        DetailCall(world, "RefreshInZoneLeaveUi");
        await Frames();
        banner.Position = new Vector2(480, 24);
        Require(banner.Visible && title.Text == "Forgotten Temple", "FT leave banner appears after zone readiness refresh");
        Require(GetViewportRect().Encloses(banner.GetGlobalRect()), "FT leave banner fits the viewport");
        var leaveButton = (Button)DetailField(world, "_inZoneLeaveBtn")!;
        var leaveSurface = banner.GetNode<Control>("classic_leave");
        Require(banner.Size == Spec.Leave.Size && banner.GetThemeStylebox("panel") is StyleBoxEmpty, $"FT leave plaque {banner.Size} replaces the flat panel");
        Expected(title, leaveSurface, "leave title"); Expected(leaveButton, leaveSurface, "leave action");
        Require(leaveButton.Size == Spec.Plaque.Action, "Leave uses the 55 x 15 chat tab art");
        Art(leaveButton, "Leave", "leave action");
        Require(title.GetThemeColor("font_color") == ClassicWorldOverlays.PlaqueInk && title.GetThemeFont("font") == Plugin.Kit.Bold, "Leave plaque caption uses the plaque ink and Arial Bold");
        await Capture("ft-leave-banner");
        foreach (int eventZone in new[] { 55, 84, 85, 86, 87 })
        {
            SetZone(eventZone);
            DetailCall(world, "RefreshInZoneLeaveUi");
            Require(banner.Visible, $"Zone {eventZone} retains an event leave banner");
            await Frames();
            Require(Fits(title) && banner.Size == Spec.Leave.Size, $"Zone {eventZone} name '{title.Text}' fits the leave plaque");
            DetailCall(world, "OnInZoneLeavePressed");
            var notice = NativeEvents.LeavePrompt(world)!;
            notice.Reparent(this);
            var dialog = Descendants(notice).OfType<Control>().First(c => c.GetType().FullName == "KnightOnlineUiClassic.Windows.MessageBox");
            Require(dialog != null, $"Zone {eventZone} uses the Classic confirmation");
            DetailCall(world, "OnInZoneLeavePressed");
            Require(ReferenceEquals(notice, NativeEvents.LeavePrompt(world)), $"Zone {eventZone} prevents duplicate confirmations");
            if (eventZone == 55) await Capture("ft-leave-confirmation");
            dialog!._Input(new InputEventKey { Pressed = true, Keycode = Key.Escape });
            await Frames();
            Require(NativeEvents.LeavePrompt(world) == null && banner.Visible, $"Zone {eventZone} Escape cancels without hiding the banner");
        }
        SetZone(55);
        DetailCall(world, "OnInZoneLeavePressed");
        var stale = NativeEvents.LeavePrompt(world)!;
        stale.Reparent(this);
        SetZone(86);
        DetailCall(world, "RefreshInZoneLeaveUi");
        await Frames();
        Require(NativeEvents.LeavePrompt(world) == null && banner.Visible, "Moving between event zones cancels the old confirmation");
        SetZone(21);
        DetailCall(world, "RefreshInZoneLeaveUi");
        DetailCall(world, "OnInZoneLeavePressed");
        Require(NativeEvents.LeavePrompt(world) == null && !banner.Visible, "Moradon has no stale leave prompt or banner");
        await Capture("outside-event");
        System.IO.File.WriteAllText(output + "/" + (nation == 1 ? "karus" : "human") + "-verification.json",
            JsonSerializer.Serialize(new { checks, screenshots, liveTransactions = false }, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print("FORGOTTEN_TEMPLE_AUDIT_OK " + checks.Count);
        foreach (var layer in GetChildren().OfType<CanvasLayer>().ToArray()) layer.Free();
        world.Free();
        net.Free();
        await Frames();
    }
}
