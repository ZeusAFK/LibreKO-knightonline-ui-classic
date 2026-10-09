using Godot;
using System.Reflection;
using System.Text.Json;
using LibreKO.Plugins;
using KnightOnlineUiClassic;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Windows;

// Classic HUD pieces that the other HUD modes do not capture: the target frame,
// message boxes, a long status name and the first-frame chat/Info scroll rails.
public partial class Preview
{
    private async Task CaptureHudQaAudit(int nation)
    {
        var output = OS.GetEnvironment("LIBREKO_AUDIT_OUTPUT_DIR"); System.IO.Directory.CreateDirectory(output);
        string prefix = nation == 1 ? "karus" : "human";
        var window = GetWindow(); window.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
        window.Size = new Vector2I(1120, 660); window.ContentScaleSize = Vector2I.Zero;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(1110, 650), GlobalPosition = new Vector2(1110, 650) });
        AddChild(new ColorRect { Color = new Color("34362b"), Size = new Vector2(1120, 660), MouseFilter = MouseFilterEnum.Ignore });
        AddChild(new ColorRect { Color = new Color("afa995"), Position = new Vector2(0, 0), Size = new Vector2(560, 300), MouseFilter = MouseFilterEnum.Ignore });
        var checks = new List<string>(); var screens = new List<string>(); var bounds = new Dictionary<string, object>();
        var failures = new List<string>();
        // Collect every failed check so one run reports all visible defects.
        void Require(bool value, string text) { if (!value) { failures.Add(text); GD.PushError("HUD_QA_AUDIT: " + text); } else checks.Add(text); }
        async Task Frames(int count = 4) { for (int i = 0; i < count; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
        async Task Shot(string state)
        {
            await Frames();
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(output, prefix + "-" + state + ".png"));
            screens.Add(prefix + "-" + state + ".png");
        }
        object Rect(Rect2 r) => new { x = r.Position.X, y = r.Position.Y, w = r.Size.X, h = r.Size.Y };
        bool Inside(Control outer, Control inner) => outer.GetGlobalRect().Grow(0.01f).Encloses(inner.GetGlobalRect());
        var target = _proxies.First(p => p is IGameTarget);
        var character = _proxies.First(p => p is IGameCharacter);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;

        // Status bar with a long name, chat and Info at their first frame (no resize after construction).
        character.Values = name => name == "get_Name" ? "Extraordinarylongname" : name == "get_Level" ? 83 : null;
        var status = new StatusHud(); AddChild(status);
        var chat = new ChatWindow(); AddChild(chat);
        var info = new LogWindow(); AddChild(info); info.Visible = true;
        var hotbar = new HotkeyBar(); AddChild(hotbar);
        await Frames(6);
        chat.Position = new Vector2(20, 424); info.Position = new Vector2(660, 409);
        hotbar.Position = new Vector2(480, 8);
        var identity = Descendants(status).OfType<Label>().Single(l => l.Name == "Text_Position");
        var zoomIn = Descendants(status).OfType<Control>().Single(c => c.Name == "Btn_ZoomIn");
        Require(identity.GetGlobalRect().End.X <= zoomIn.GetGlobalRect().Position.X - 2, "Status name field ends before the minimap zoom button");
        Require(identity.TextOverrunBehavior == TextServer.OverrunBehavior.TrimEllipsis && identity.ClipText, "Long status names are trimmed with an ellipsis");
        bounds["statusName"] = Rect(identity.GetGlobalRect()); bounds["zoomIn"] = Rect(zoomIn.GetGlobalRect());
        foreach (var (name, control) in new (string, Control)[] { ("chat", chat), ("info", info) })
        {
            var rail = (Control)control.GetType().GetField("_rail", flags)!.GetValue(control)!;
            var arrows = rail.GetChildren().OfType<TextureButton>().ToArray();
            Require(arrows.Length == 2 && arrows[0].Position.Y == 0 && arrows[1].GetRect().End.Y == rail.Size.Y && rail.Size.Y > 60,
                $"{name} scroll arrows sit at both ends of the rail on the first frame (rail {rail.Size.Y})");
            bounds[name + "Rail"] = new { rail = Rect(rail.GetGlobalRect()), up = Rect(arrows[0].GetGlobalRect()), down = Rect(arrows[1].GetGlobalRect()) };
        }
        var keys = hotbar.FindChildren("key_*", "Label", true, false).Cast<Label>().ToArray();
        var ink = keys[0].GetThemeColor("font_color");
        Require(keys.Length == 10 && keys.All(k => k.GetThemeColor("font_color") == ink), "Hotbar numerals share one ink");
        Require(nation == 1 ? ink.Luminance < 0.3f && keys[0].GetThemeConstant("outline_size") == 0 : ink.Luminance > 0.6f,
            nation == 1 ? "Karus hotbar numerals use dark ink on the light rail" : "El Morad hotbar numerals keep light ink on the dark rail");
        var defaultAt = (Vector2)typeof(HotkeyBar).GetMethod("DefaultPosition", flags)!.Invoke(hotbar, null)!;
        var hotbarDefault = new Rect2(defaultAt, hotbar.Size);
        var mailButton = Descendants(status).OfType<ClassicStatusShortcut>().Single(b => b.Name == "btn_mail").GetGlobalRect();
        Require(!hotbarDefault.Intersects(StatusHud.DefaultBounds) && !hotbarDefault.Intersects(mailButton) && GetViewportRect().Encloses(hotbarDefault),
            $"Default vertical hotbar {hotbarDefault} clears the status minimap and shortcuts");
        bounds["hotbarDefault"] = Rect(hotbarDefault); bounds["statusDefault"] = Rect(StatusHud.DefaultBounds);
        hotbar.Position = defaultAt;
        await Shot("status-chat");
        var image = GetViewport().GetTexture().GetImage();
        foreach (var (id, bar) in new[] { ("Text_HP", "Progress_HP"), ("Text_MSP", "Progress_MSP"), ("Text_ExpP", "Progress_ExpP") })
        {
            var text = Descendants(status).OfType<Label>().Single(l => l.Name == id).GetGlobalRect();
            var meter = Descendants(status).OfType<Control>().Single(c => c.Name == bar).GetGlobalRect();
            // Rendered white digit rows inside the label rectangle.
            var rows = Enumerable.Range((int)text.Position.Y - 2, (int)text.Size.Y + 4).Where(y => Enumerable.Range((int)text.Position.X, (int)text.Size.X)
                .Any(x => image.GetPixel(x, y) is var c && c.R > 0.85f && c.G > 0.85f && c.B > 0.85f)).ToArray();
            Require(rows.Length > 0, id + " digits are rendered");
            float digits = (rows.First() + rows.Last() + 1) / 2f, centre = meter.GetCenter().Y;
            bounds[id + "Rows"] = new { top = rows.First(), bottom = rows.Last(), digits, meter = centre };
            // Recorded only: the native UIF places these text rows on the lower edge of each bar.
        }
        status.QueueFree(); chat.QueueFree(); info.QueueFree(); hotbar.QueueFree();
        character.Values = null;

        // Target frame: monster with level, long name at low HP and a player without a level.
        var states = new (string Name, int Level, int Hp, int MaxHp, bool Player)[] {
            ("Death Knight", 72, 6100, 10000, false),
            ("Ancient Blood Seeker of the Abyss", 83, 1500, 10000, false),
            ("Guardian", 0, 10000, 10000, true),
        };
        var frames = new List<TargetFrame>();
        for (int i = 0; i < states.Length; i++)
        {
            var s = states[i];
            target.Values = name => name switch {
                "get_Has" => true, "get_Name" => s.Name, "get_Level" => s.Level, "get_Hp" => s.Hp, "get_MaxHp" => s.MaxHp,
                "get_IsPlayer" => s.Player, "get_Hostile" => !s.Player, _ => null };
            var frame = new TargetFrame(); AddChild(frame); await Frames(1);
            typeof(TargetFrame).GetMethod("Refresh", flags)!.Invoke(frame, null);
            frame.Position = new Vector2(Mathf.Round((1120 - frame.Size.X) / 2), 8 + i * 84);
            frames.Add(frame);
            await Frames(2);
            Require(frame.Visible, $"Target frame {i} is visible for an available target");
            var label = Descendants(frame).OfType<Label>().Single(l => l.Name == "text_target");
            var hp = Descendants(frame).OfType<Control>().Single(c => c.Name == "pro_target");
            Require(Inside(frame, label) && Inside(frame, hp), $"Target frame {i} name and HP bar stay inside the frame");
            var font = label.GetThemeFont("font"); int size = label.GetThemeFontSize("font_size");
            float width = font.GetStringSize(label.Text, HorizontalAlignment.Left, -1, size).X;
            Require(width <= label.Size.X || label.TextOverrunBehavior != TextServer.OverrunBehavior.NoTrimming || label.AutowrapMode != TextServer.AutowrapMode.Off,
                $"Target frame {i} name fits or is trimmed ({width:0} of {label.Size.X:0})");
            Require(!label.GetGlobalRect().Intersects(hp.GetGlobalRect()), $"Target frame {i} name does not overlap the HP bar");
            bounds["target" + i] = new { frame = Rect(frame.GetGlobalRect()), name = Rect(label.GetGlobalRect()), hp = Rect(hp.GetGlobalRect()), text = label.Text, textWidth = width };
        }
        target.Values = null;
        await Shot("target");
        foreach (var frame in frames) frame.QueueFree();

        // Message boxes: OK, OK/Cancel, a long message and a non-dismissable notice.
        var boxes = new (string State, string Title, string Message, string Confirm, string? Cancel, bool Dismissable)[] {
            ("msgbox-ok", "Notice", "You do not have enough gold.", "OK", null, true),
            ("msgbox-okcancel", "Party", "Guardian invites you to join a party. Do you accept?", "Accept", "Decline", true),
            ("msgbox-long", "Warning", "This item will be destroyed permanently and cannot be recovered. All upgrades, stats and bound attributes are lost. Are you sure that you want to continue?", "OK", "Cancel", true),
            ("msgbox-wait", "Connecting", "Please wait while the server answers your request...", "OK", null, false),
        };
        foreach (var b in boxes)
        {
            int confirmed = 0, cancelled = 0, dismissed = 0;
            var request = (DialogRequest)Create(typeof(DialogRequest), b.Title, b.Message, b.Confirm, b.Cancel, b.Dismissable,
                (Action)(() => confirmed++), (Action)(() => cancelled++), (Action)(() => dismissed++));
            var box = new MessageBox(request); AddChild(box);
            await Frames(3);
            var view = box.GetChildren().OfType<LayoutView>().Single();
            Require(Mathf.Abs(view.GetGlobalRect().GetCenter().X - 560) <= 1 && Mathf.Abs(view.GetGlobalRect().GetCenter().Y - 330) <= 1, b.State + " frame is centred in the viewport");
            var message = Descendants(view).OfType<Label>().Single(l => l.Name == "Text_Message");
            var title = Descendants(view).OfType<Label>().Single(l => l.Name == "Text_title");
            Require(Inside(view, message) && Inside(view, title), b.State + " title and message stay inside the frame");
            int lines = message.GetLineCount(); float lineHeight = message.GetLineHeight();
            Require(lines * lineHeight <= message.Size.Y + 0.5f, $"{b.State} message fits its field ({lines} lines x {lineHeight} in {message.Size.Y})");
            var buttons0 = Descendants(view).OfType<BaseButton>().Where(c => c.IsVisibleInTree()).Select(c => c.GetGlobalRect()).ToArray();
            Require(buttons0.All(r => message.GetGlobalRect().End.Y <= r.Position.Y) && title.GetGlobalRect().Position.Y < message.GetGlobalRect().Position.Y,
                b.State + " message stays between the title and the buttons");
            var buttons = Descendants(view).OfType<BaseButton>().Where(c => c.IsVisibleInTree() && (c.Name == "btn_ok" || c.Name == "btn_cancel")).OrderBy(c => c.Position.X).ToArray();
            Require(buttons.Length == (b.Dismissable ? (b.Cancel != null ? 2 : 1) : 0), b.State + " shows the expected buttons");
            foreach (var button in buttons)
            {
                Require(Inside(view, button), b.State + " button inside frame " + button.Name);
                var caption = Descendants(button).OfType<Label>().Where(l => l.Visible).ToArray();
                Require(caption.Length == 1 && Inside(button, caption[0]), b.State + " one caption inside " + button.Name);
                var f = caption[0].GetThemeFont("font"); int fs = caption[0].GetThemeFontSize("font_size");
                Require(f.GetStringSize(caption[0].Text, HorizontalAlignment.Left, -1, fs).X <= caption[0].Size.X, b.State + " caption fits " + caption[0].Text);
            }
            // Content-sized box: the shared rhythm (title band, message lines, buttons) with no empty band.
            float spacing = message.GetThemeConstant("line_spacing");
            Require(message.Position.Y == KnightOnlineUiClassic.Layout.ClassicOverlayLayout.Message.TextTop
                && Mathf.Abs(message.Size.Y - (lines * lineHeight + (lines - 1) * spacing)) < 1f, $"{b.State} message rectangle is its {lines} wrapped lines at the common top ({message.GetRect()})");
            foreach (var button in buttons)
                Require(button.Position.Y == message.GetRect().End.Y + KnightOnlineUiClassic.Layout.ClassicOverlayLayout.Message.ActionGap
                    && view.Size.Y - button.GetRect().End.Y == KnightOnlineUiClassic.Layout.ClassicOverlayLayout.Message.ButtonBottom, $"{b.State} {button.Name} sits the common gap under the message ({button.GetRect()} in {view.Size})");
            if (buttons.Length == 0)
                Require(view.Size.Y - message.GetRect().End.Y == KnightOnlineUiClassic.Layout.ClassicOverlayLayout.Message.TextBottom, $"{b.State} frame closes the common gap under the message ({view.Size})");
            var art = view.GetNode<Control>("message_art");
            Require(art.Size == view.Size && art.Position == Vector2.Zero, $"{b.State} frame art covers the fitted box");
            if (buttons.Length == 1) Require(Mathf.Abs(buttons[0].GetGlobalRect().GetCenter().X - view.GetGlobalRect().GetCenter().X) <= 1, b.State + " single button is centred");
            if (buttons.Length == 2)
            {
                float left = buttons[0].GetGlobalRect().Position.X - view.GetGlobalRect().Position.X;
                float right = view.GetGlobalRect().End.X - buttons[1].GetGlobalRect().End.X;
                Require(Mathf.Abs(left - right) <= 1 && buttons[0].Size == buttons[1].Size && buttons[0].Position.Y == buttons[1].Position.Y, b.State + " button pair is symmetric");
            }
            bounds[b.State] = new { frame = Rect(view.GetGlobalRect()), title = Rect(title.GetGlobalRect()), message = Rect(message.GetGlobalRect()), lines,
                buttons = buttons.Select(x => new { name = x.Name.ToString(), rect = Rect(x.GetGlobalRect()) }).ToArray() };
            await Shot(b.State);
            if (b.Dismissable)
            {
                buttons[0].EmitSignal(BaseButton.SignalName.Pressed);
                if (b.Cancel != null) { buttons[1].EmitSignal(BaseButton.SignalName.Pressed); Require(confirmed == 1 && cancelled == 1, b.State + " buttons fire the native confirm/cancel callbacks"); }
                else Require(dismissed == 1, b.State + " OK fires the native dismiss callback");
            }
            box.QueueFree(); await Frames(1);
        }

        System.IO.File.WriteAllText(System.IO.Path.Combine(output, prefix + "-hud-qa.json"), JsonSerializer.Serialize(new { nation, checks, failures, screens, bounds }, new JsonSerializerOptions { WriteIndented = true }));
        if (failures.Count > 0) throw new Exception($"HUD_QA_AUDIT: {failures.Count} failed checks");
        GD.Print($"HUD_QA_AUDIT_OK: {checks.Count} checks, {screens.Count} screens");
    }
}
