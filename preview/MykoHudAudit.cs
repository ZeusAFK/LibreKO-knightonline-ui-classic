using Godot;
using System.Reflection;
using System.Text.Json;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using KnightOnlineUiClassic;
using KnightOnlineUiClassic.Native;
using KnightOnlineUiClassic.Windows;
using L = KnightOnlineUiClassic.Layout.ClassicMykoHudLayout;

// The HUD pieces on optional local art (myko-hud-audit): the status bar's letter plates and clock, the client's
// Events window on the scheduler art and the client's event title chips as notice bars with the GM List bar and the
// content box. Run with KNIGHTONLINE_UI_CLASSIC_LOCAL_ASSETS pointing at an installed assets-local folder, and
// without it: then every piece must keep the bundled Classic design (no clock, the family frame, plaque chips).
public partial class Preview
{
    private async Task CaptureMykoHudAudit(int nation)
    {
        var output = OS.GetEnvironment("LIBREKO_AUDIT_OUTPUT_DIR"); System.IO.Directory.CreateDirectory(output);
        ProjectSettings.LoadResourcePack(ProjectSettings.GlobalizePath("res://../../build/client/source-content/knightonline.pck"), false);
        GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Disabled; GetWindow().Size = new Vector2I(1280, 800);
        AddChild(new ColorRect { Color = new Color("2f332a"), Size = new Vector2(1280, 800), MouseFilter = MouseFilterEnum.Ignore });
        var offline = new Net(); typeof(Net).GetProperty("I")!.SetValue(null, offline);
        Native.Call(offline, "SeedPreviewEnter", new MyInfo { Nation = nation, Name = "Classic Preview" });
        ItemData.EnsureLoaded();
        bool local = ClassicMykoPlates.Available && ClassicMykoScheduler.Available && ClassicNoticeBars.Available;
        string variant = local ? "local" : "classic";
        string prefix = (nation == 1 ? "karus" : "human") + "-" + variant;
        var checks = new List<string>(); var screens = new List<object>(); var bounds = new Dictionary<string, object>();
        void Require(bool condition, string text) { if (!condition) throw new Exception("MYKO_HUD_AUDIT: " + text); checks.Add(text); }
        async Task Frames(int count = 6) { for (int i = 0; i < count; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
        object Rect(Rect2 r) => new { x = r.Position.X, y = r.Position.Y, w = r.Size.X, h = r.Size.Y };
        bool Inside(Rect2 outer, Rect2 inner) => outer.Grow(.5f).Encloses(inner);
        bool Near(Vector2 a, Vector2 b) => (a - b).Abs() is var d && d.X < .51f && d.Y < .51f;
        float TextWidth(Label label) => label.GetThemeFont("font").GetStringSize(label.Text, HorizontalAlignment.Left, -1, label.GetThemeFontSize("font_size")).X;
        async Task Shot(string state, Rect2 crop)
        {
            await Frames();
            using var image = GetViewport().GetTexture().GetImage();
            var r = new Rect2I((Vector2I)crop.Position.Floor(), (Vector2I)crop.Size.Ceil()).Intersection(new Rect2I(Vector2I.Zero, image.GetSize()));
            string file = prefix + "-" + state + ".png";
            using (var part = image.GetRegion(r)) part.SavePng(System.IO.Path.Combine(output, file));
            screens.Add(new { state, file, crop = Rect(crop) });
        }
        async Task Hover(Vector2 at) { GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }); await Frames(3); }
        async Task Click(Vector2 at)
        {
            await Hover(at);
            foreach (bool down in new[] { true, false })
            {
                GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = down, Position = at, GlobalPosition = at });
                await Frames(1);
            }
            await Frames(3);
        }
        async Task Away() => await Hover(new Vector2(1, 799));
        Require(local == (Plugin.Kit.Local.Layout(L.HpMenu.Replace("{nation}", nation == 1 ? "ka" : "el")) != null), variant + ": the local layouts are " + (local ? "installed" : "absent") + " at " + Plugin.Kit.Local.Root);

        // ---- Status bar: letter plates and the clock.
        {
            var status = new StatusHud(); AddChild(status); await Frames(8);
            var letters = Descendants(status).OfType<ClassicStatusShortcut>().Where(s => s.HasBackground).OrderBy(s => s.GetGlobalRect().Position.X).ToArray();
            var clock = Descendants(status).OfType<ClassicStatusClock>().SingleOrDefault();
            Require(letters.Select(s => s.Letter).SequenceEqual(new[] { "Q", "P", "A", "D", "G", "E", "R" }), "the letter row stays Q P A D G E R");
            Require(local == (clock != null), local ? "the original clock follows the letter row" : "no clock without the local art");
            var row = letters.Aggregate(letters[0].GetGlobalRect(), (r, s) => r.Merge(s.GetGlobalRect()));
            if (clock != null)
            {
                var last = letters[^1].GetGlobalRect(); float pitch = letters[1].GetGlobalRect().Position.X - letters[0].GetGlobalRect().Position.X;
                var c = clock.GetGlobalRect();
                Require(Near(c.Position, last.Position + new Vector2(pitch, 0)) && c.Size == last.Size, $"the clock sits one pitch after R with the plate size ({c} after {last})");
                Require(status.GetGlobalRect().Grow(.5f).Encloses(c) && Inside(GetViewportRect(), c), "the clock lies inside the status HUD and the screen");
                Require(clock.GetSignalConnectionList(BaseButton.SignalName.Pressed).Count > 0 && clock.TooltipText == ClassicStatusClock.Title, "the clock opens the event schedule");
                bounds["clock"] = Rect(c);
                row = row.Merge(c);
            }
            foreach (var s in letters) bounds["letter-" + s.Letter] = Rect(s.GetGlobalRect());
            var crop = row.Grow(6);
            await Shot("row-normal", crop);
            var buttons = letters.Cast<BaseButton>().Concat(clock != null ? new BaseButton[] { clock } : Array.Empty<BaseButton>()).ToArray();
            foreach (var b in buttons) { b.EmitSignal(Control.SignalName.MouseEntered); b.QueueRedraw(); }
            await Shot("row-hover", crop);
            foreach (var b in buttons) { b.ToggleMode = true; b.SetPressedNoSignal(true); b.QueueRedraw(); }
            if (clock != null) Require(clock.State == KnightOnlineUiClassic.Layout.ButtonState.Pressed, "the clock draws its pressed art");
            await Shot("row-pressed", crop);
            foreach (var b in buttons) { b.SetPressedNoSignal(false); b.ToggleMode = false; b.EmitSignal(Control.SignalName.MouseExited); b.Disabled = true; b.QueueRedraw(); }
            await Shot("row-disabled", crop);
            status.QueueFree(); await Frames(2);
        }

        // ---- Events window: the scheduler art with the client's rows, or the Classic family frame.
        async Task<(World World, HudWindow Window)> Open(Action<World>? seed = null, string fixture = "BuildEventsUiPreview", params object?[] args)
        {
            var world = new World();
            var window = (HudWindow)DetailCall(world, fixture, args)!;
            seed?.Invoke(world);
            NativeEventSchedule.Bind(window, world);
            AddChild(window); window.Visible = true;
            var frame = ClassicEventFamily.Apply(window.Body);
            await Frames(8);
            window.Position = ((GetViewportRect().Size - window.Size) / 2).Round();
            await Frames(4);
            if (!local) Require(frame != null && window.GetNodeOrNull<ClassicSchedulerPanel>(ClassicMykoScheduler.PanelName) == null, "events: the Classic family frame without the local art");
            return (world, window);
        }
        void Reschedule(World world, int extra)
        {
            var utc = DateTime.UtcNow;
            var slots = Enumerable.Range(0, Math.Max(0, extra)).Select(i => { var at = utc.AddMinutes(20 + 70 * i); return new EventSlot(EventKind.Temple, i % 3 == 0 ? "Border Defence War of the Ardream Plains" : "Event " + (i + 1), EventSchedule.EveryDay, at.Hour, at.Minute, 10 * (i % 8), 83); }).ToList();
            var now = new List<EventNow> { new(EventKind.Temple, "Juraid Mountain", EventPhase.SignUp, 412, 40, 83), new(EventKind.Lottery, "Daily Lottery", EventPhase.Running, 900, 0, 0) };
            typeof(World).GetField("_eventSchedule", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(world, extra < 0 ? null : new EventSchedule(extra == 0 ? new List<EventNow>() : now, slots, utc));
        }
        async Task Schedule(string state, HudWindow window, World world)
        {
            var panel = window.GetNode<ClassicSchedulerPanel>(ClassicMykoScheduler.PanelName);
            panel.Refresh(); await Frames(4);
            var box = window.GetGlobalRect();
            Require(box.Size == new Vector2(306, 385) && panel.GetGlobalRect() == box, state + ": the window is the 306×385 scheduler art " + box);
            Require(window.GetThemeStylebox("panel") is StyleBoxEmpty && window.GetChildren().OfType<MarginContainer>().All(m => !m.Visible), state + ": no client window chrome");
            var schedule = NativeEventSchedule.Schedule(world);
            var rows = schedule == null ? new List<NativeEventSchedule.Row>() : NativeEventSchedule.Rows(schedule, DateTime.UtcNow);
            Require(panel.Shown.Select(r => r.Name).SequenceEqual(rows.Select(r => r.Name)), state + ": the rows are the client's, in its order (" + string.Join(", ", panel.Shown.Select(r => r.Name)) + ")");
            int onPage = Math.Max(0, Math.Min(L.ScheduleRows, rows.Count - panel.PageIndex * L.ScheduleRows));
            var visibleRows = Descendants(panel).OfType<TextureButton>().Where(b => b.Name == "btn_event" && b.IsVisibleInTree()).ToList();
            Require(visibleRows.Count == onPage, $"{state}: {onPage} rows on page {panel.PageIndex + 1} ({visibleRows.Count} shown)");
            foreach (var label in Descendants(panel).OfType<Label>().Where(l => l.IsVisibleInTree() && l.Text.Length > 0))
            {
                Require(Inside(box, label.GetGlobalRect()), state + ": inside the window: '" + label.Text + "'");
                if (label.AutowrapMode == TextServer.AutowrapMode.Off && !label.ClipText)
                    Require(TextWidth(label) <= label.Size.X + 1, state + ": '" + label.Text + "' fits " + label.Size);
                if (label.ClipText && TextWidth(label) > label.Size.X) Require(label.TextOverrunBehavior == TextServer.OverrunBehavior.TrimEllipsis, state + ": long text ends with an ellipsis: " + label.Text);
            }
            var empty = panel.GetNode<Label>(ClassicSchedulerPanel.Empty);
            Require(empty.Visible == (rows.Count == 0), state + ": the empty line shows only without rows");
            var page = (Label)Descendants(panel).OfType<Label>().First(l => l.Name == ClassicSchedulerPanel.Page);
            Require(page.Text == $"{panel.PageIndex + 1} / {panel.PageCount}", state + ": pager " + page.Text);
            Require(Descendants(panel).OfType<Label>().Any(l => l.Text == L.TimeCaption) && Descendants(panel).OfType<Label>().First(l => l.Name == ClassicSchedulerPanel.Time).Text.Length == 5,
                state + ": the header shows the local time");
            bounds["schedule-" + state] = new { window = Rect(box), rows = visibleRows.Select(b => Rect(b.GetGlobalRect())).ToArray() };
            await Shot("schedule-" + state, box.Grow(4));
        }
        if (local)
        {
            var (world, window) = await Open();
            await Schedule("native", window, world);
            Require(new[] { "OfferBifrostJoin", "ToggleLottery" }.All(m => Native.HasMethod(world, m)), "the Join and Open rows reach the client's own actions");
            var panel = window.GetNode<ClassicSchedulerPanel>(ClassicMykoScheduler.PanelName);
            Require(panel.Shown[0].Action == "Join" && panel.Shown.Skip(1).All(r => r.Action == null || r.Action == "Open"), "the temple sign-up row offers Join, as the client's row");
            var firstRow = Descendants(panel).OfType<TextureButton>().First(b => b.Name == "btn_event" && b.IsVisibleInTree());
            Require(firstRow.TooltipText.Contains("Click to join.") && firstRow.TooltipText.Contains("Lv 40-83"), "the row tooltip names the level band and the action: " + firstRow.TooltipText.Replace("\n", " / "));
            var second = Descendants(panel).OfType<TextureButton>().Where(b => b.Name == "btn_event" && b.IsVisibleInTree()).ElementAt(1);
            await Hover(second.GetGlobalRect().GetCenter());
            await Shot("schedule-hover", window.GetGlobalRect().Grow(4));
            second.ButtonPressed = true; second.EmitSignal(BaseButton.SignalName.Pressed); await Away();
            Require(second.ButtonPressed, "a row stays selected");
            await Shot("schedule-selected", window.GetGlobalRect().Grow(4));
            Reschedule(world, 14); panel.Refresh();
            await Schedule("page-1", window, world);
            var next = (TextureButton)Descendants(panel).OfType<TextureButton>().First(b => b.Name == ClassicSchedulerPanel.Next);
            var previous = (TextureButton)Descendants(panel).OfType<TextureButton>().First(b => b.Name == ClassicSchedulerPanel.Previous);
            Require(previous.Disabled && !next.Disabled && panel.PageCount == 2, "page 1 of 2: Next only");
            await Click(next.GetGlobalRect().GetCenter()); await Away();
            Require(panel.PageIndex == 1 && !previous.Disabled && next.Disabled, "Next turns to page 2 by a real click");
            await Schedule("page-2", window, world);
            Reschedule(world, 0); panel.Refresh();
            await Schedule("empty", window, world);
            Reschedule(world, -1); panel.Refresh();
            await Schedule("loading", window, world);
            // Drag through the title strip moves the client's window; close runs the client's close.
            var before = window.Position;
            var grip = Descendants(panel).OfType<Control>().First(c => c.Name == "party_drag");
            Require(grip.GetGlobalRect().Size == L.SchedulerDrag.Size, "the title strip is the drag handle");
            var closeButton = (TextureButton)Descendants(panel).OfType<TextureButton>().First(b => b.Name == ClassicSchedulerPanel.Close);
            var closeRect = closeButton.GetGlobalRect();
            Require(!grip.GetGlobalRect().Intersects(closeRect), "the drag handle leaves the close button free");
            await Click(closeRect.GetCenter());
            Require(!window.Visible, "the close button runs the client's close");
            window.QueueFree(); world.QueueFree(); await Frames(2);
        }
        else
        {
            var (world, window) = await Open();
            await Shot("schedule-classic", window.GetGlobalRect().Grow(4));
            window.QueueFree(); world.QueueFree(); await Frames(2);
        }

        // ---- Top-right group: premium, clan premium, time, the client's event title chips, GM List, content box.
        void Premium(int status, int type, int hours, params PremiumBonus[] bonuses)
        {
            foreach (var (name, value) in new (string, object)[] { ("PremiumAccountStatus", status), ("PremiumType", type), ("PremiumHours", hours),
                ("PremiumBonuses", (IReadOnlyList<PremiumBonus>)bonuses) })
                typeof(Net).GetProperty(name)!.SetValue(Net.I, value);
        }
        var layer = new CanvasLayer { Layer = 70 }; AddChild(layer);
        foreach (bool expanded in new[] { false, true })
        {
            NativeGmList.Clear();
            Premium(0, 0, 0);
            var world = new World();
            var column = (Container)DetailCall(world, "BuildEventTitlesUiPreview", 3, expanded)!;
            column.GetParent()?.RemoveChild(column);
            layer.AddChild(column); column.Visible = true;
            column.Position = new Vector2(1280 - 190 - 169, 40);
            NativeOverlays.PrepareLater(world);
            await Frames(8);
            string tag = expanded ? "expanded" : "collapsed";
            var chips = column.GetChildren().OfType<PanelContainer>().ToList();
            Require(chips.Count == (expanded ? 3 : 1), tag + ": the client's chips (" + chips.Count + ")");
            var stack = layer.GetNodeOrNull<ClassicNoticeStack>(ClassicNoticeBars.StackName);
            if (!local)
            {
                Require(stack == null && chips.All(c => c.GetThemeStylebox("panel") is StyleBoxEmpty && c.GetSignalConnectionList(CanvasItem.SignalName.Draw).Count > 0 && !c.HasMeta("classic_notice_bar")),
                    tag + ": the Classic plaque chips without the local art");
                await Shot("notices-classic-" + tag, column.GetGlobalRect().Grow(6));
                layer.RemoveChild(column); column.QueueFree(); world.Free(); await Frames(2);
                continue;
            }
            Require(stack != null, tag + ": the top-right group follows the column");
            bool inClan = expanded; long clanEnds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (3 * 24 + 2) * 3600 + 120;
            stack!.ClockText = () => "14:33  Clear";
            stack.ClanSource = () => new NativePremium.Clan(inClan, expanded, expanded ? clanEnds : 0);
            stack.Sync(); await Frames(4);
            var gm = stack.GmBar; var box = stack.Box; var premium = stack.PremiumBar; var clan = stack.ClanBar; var time = stack.TimePlate;
            float right = 1280 - L.GroupRight;
            // Order and geometry: premium at the top, clan premium (in a clan), time, notice bars, GM List.
            var p = premium.GetGlobalRect();
            Require(p.Size == L.Bar && Near(p.Position, new Vector2(right - L.Bar.X, L.GroupTop)), $"{tag}: the premium bar tops the group at the right edge ({p})");
            Require(clan.Visible == inClan, $"{tag}: the clan premium bar shows only in a clan");
            float y = p.End.Y + L.BarGap;
            if (inClan) { var c = clan.GetGlobalRect(); Require(c.Size == L.Bar && Near(c.Position, new Vector2(right - L.Bar.X, y)), $"{tag}: the clan premium bar follows ({c})"); y = c.End.Y + L.BarGap; }
            var t = time.GetGlobalRect();
            Require(t.Size == new Vector2(125, 19) && Near(t.Position, new Vector2(right - 125, y)), $"{tag}: the time plate follows, right-aligned ({t})");
            Require(stack.TimeValue.Text == "14:33" && time.TooltipText == "Server time 14:33, Clear", $"{tag}: the time plate shows the client's clock ({stack.TimeValue.Text} / {time.TooltipText})");
            y = t.End.Y + L.BarGap;
            for (int i = 0; i < chips.Count; i++)
            {
                var r = chips[i].GetGlobalRect();
                Require(r.Size == L.Bar && Near(r.Position, new Vector2(right - L.Bar.X, y)), $"{tag}: notice bar {i} is 169×23 in the group ({r}, expected y {y})");
                var caption = Descendants(chips[i]).OfType<Label>().Single();
                Require(Inside(r, caption.GetGlobalRect()) && caption.ClipText && Mathf.Abs(caption.GetGlobalRect().GetCenter().X - r.GetCenter().X) < 1.01f,
                    $"{tag}: caption '{caption.Text}' centred inside its bar ({caption.GetGlobalRect()})");
                bounds[$"{tag}-bar-{i}"] = Rect(r);
                y = r.End.Y + L.BarGap;
            }
            var knob = Descendants(chips[0]).OfType<Button>().SingleOrDefault();
            Require(knob != null && knob.Size == new Vector2(16, 16) && knob.GetThemeStylebox("normal") is StyleBoxTexture, tag + ": the first bar keeps the client's toggle on the original knob");
            var k = knob!.GetGlobalRect(); var first = chips[0].GetGlobalRect();
            Require(Mathf.Abs(k.Position.X - first.Position.X - 152) < 1.01f && Mathf.Abs(k.Position.Y - first.Position.Y - L.KnobTop) < 1.01f, $"{tag}: the knob sits at the original (152, 5) ({k.Position - first.Position})");
            var g = gm.GetGlobalRect();
            Require(g.Size == L.Bar && Near(g.Position, new Vector2(right - L.Bar.X, y)), $"{tag}: the GM List bar follows the last notice bar ({g})");
            Require(!box.Visible, tag + ": the content box starts closed");
            foreach (var (name, control) in new (string, Control)[] { ("premium", premium), ("clan", clan), ("time", time), ("gm", gm) }) if (control.Visible) bounds[$"{tag}-{name}"] = Rect(control.GetGlobalRect());
            Rect2 Group() => new Control[] { premium, clan, time, gm }.Concat(column.GetChildren().OfType<Control>()).Where(c => c.IsVisibleInTree())
                .Aggregate(p, (r, c) => r.Merge(c.GetGlobalRect())).Merge(box.Visible ? box.GetGlobalRect() : p).Grow(6);
            // Premium: none (muted), then active; a click shows the time left and the benefits, again or outside closes.
            Require(stack.PremiumCaption == "No Premium" && ((Label)Descendants(premium).OfType<Label>().Single()).GetThemeColor("font_color") == L.Muted, tag + ": no premium reads 'No Premium' in the muted ink");
            await Shot("group-" + tag, Group());
            await Click(p.GetCenter()); await Away();
            Require(box.Visible && stack.OpenKey == ClassicNoticeBars.PremiumKey && stack.Text.Text.StartsWith("No Premium"), tag + ": the premium bar opens 'No Premium' details");
            await Click(p.GetCenter()); await Away();
            Require(!box.Visible, tag + ": pressing the premium bar again closes the box");
            Premium(1, LibreKO.Domain.PremiumNames.Gold, 52, new PremiumBonus(PremiumBonusKind.Experience, 10), new PremiumBonus(PremiumBonusKind.Drops, 5));
            stack.Sync(); await Frames(3);
            Require(stack.PremiumCaption == "Gold Premium" && ((Label)Descendants(premium).OfType<Label>().Single()).GetThemeColor("font_color") == Colors.White, tag + ": an active premium shows its name in white: " + stack.PremiumCaption);
            await Click(p.GetCenter()); await Away();
            Require(box.Visible && stack.Text.Text == "Gold Premium\n2 days 4 hours left\nEXP +10%\nDrop chance +5%", tag + ": a click shows the time left and the benefits: " + stack.Text.Text.Replace("\n", " / "));
            var b = box.GetGlobalRect();
            Require(b.Size == new Vector2(169, 149) && Near(b.Position, new Vector2(right - 169, g.End.Y + L.ContentGap)), $"{tag}: the 169×149 box opens under the group ({b})");
            await Shot("premium-open-" + tag, Group());
            await Click(new Vector2(300, 600)); await Away();
            Require(!box.Visible && stack.OpenKey == null, tag + ": a click outside closes the box");
            if (inClan)
            {
                await Click(clan.GetGlobalRect().GetCenter()); await Away();
                Require(box.Visible && stack.Text.Text.StartsWith("Clan Premium\n3 days 2 hours left"), "the clan premium bar shows the clan's time left: " + stack.Text.Text.Replace("\n", " / "));
                await Shot("clan-open", Group());
                await Click(clan.GetGlobalRect().GetCenter()); await Away();
            }
            await Hover(chips[0].GetGlobalRect().GetCenter() - new Vector2(30, 0));
            await Shot("group-" + tag + "-hover", Group());
            await Away();
            if (!expanded)
            {
                // The knob is the client's own toggle: it expands the column.
                await Click(k.GetCenter()); await Away();
                Require(Net.I.EventTitles.Expanded && column.GetChildren().OfType<PanelContainer>().Count() == 3, "the + knob expands the notices through the client");
                Require(Descendants(column.GetChildren().OfType<PanelContainer>().First()).OfType<Button>().Single().GetThemeStylebox("normal") is StyleBoxTexture, "the expanded first bar shows the − knob");
                await Frames(4);
                await Shot("group-knob-expanded", Group());
                layer.RemoveChild(column); column.QueueFree(); stack.QueueFree(); world.Free(); await Frames(2);
                continue;
            }
            // A notice: its message in the box under the GM bar; the bar shows its pressed art; again closes it.
            var notice = chips[0]; string title = Descendants(notice).OfType<Label>().Single().Text;
            await Click(notice.GetGlobalRect().GetCenter() - new Vector2(30, 0)); await Away();
            Require(box.Visible && stack.OpenKey == ClassicNoticeBars.KeyOf(title), "a notice bar opens the content box with its notice");
            Require(stack.Text.Text == Net.I.EventTitles.Entries[0].Message, "the box shows the notice's message");
            var clip = stack.Text.GetParent<Control>().GetGlobalRect();
            Require(Inside(box.GetGlobalRect(), clip) && clip.Size.X == 141 && clip.Size.Y <= 139 && clip.Size.Y > 139 - stack.Text.GetLineHeight()
                && Mathf.IsEqualApprox(clip.Size.Y % stack.Text.GetLineHeight(), 0), "the text area is the original text_content rect, cut to whole lines " + clip);
            await Shot("notice-open", Group());
            // A long message scrolls by lines with the art's buttons.
            Net.I.EventTitles.Show(title, string.Join(" ", Enumerable.Repeat("Siege practice tonight at nine, meet at the Moradon gate with full potions and scrolls.", 6)));
            stack.Refresh(); await Frames(3);
            Require(stack.MaxScroll > 0 && stack.Scroll == 0, "a long message can scroll (" + stack.MaxScroll + " lines)");
            var down = Descendants(box).OfType<TextureButton>().First(x => x.Name == "notice_scroll_down");
            await Click(down.GetGlobalRect().GetCenter()); await Click(down.GetGlobalRect().GetCenter()); await Away();
            Require(stack.Scroll == 2 && box.Visible, "the down arrow scrolls by a line (" + stack.Scroll + ")");
            var thumb = Descendants(box).OfType<Control>().First(x => x.Name == "notice_scroll_thumb");
            Require(thumb.Position.Y > 0, "the thumb follows the text");
            await Shot("notice-scrolled", box.GetGlobalRect().Grow(4));
            await Click(notice.GetGlobalRect().GetCenter() - new Vector2(30, 0)); await Away();
            Require(!box.Visible && stack.OpenKey == null, "pressing the open bar again closes the box");
            // GM List: empty for now, filled through NativeGmList.
            await Click(gm.GetGlobalRect().GetCenter()); await Away();
            Require(box.Visible && stack.OpenKey == ClassicNoticeBars.GmKey && stack.Text.Text == L.NoGms, "the GM List bar opens the box with '" + L.NoGms + "'");
            await Shot("gm-empty", Group());
            NativeGmList.Set(new[] { "[GM]Moira", "[GM]Hepa", "[GM]Clepio" }); await Frames(3);
            Require(stack.Text.Text == "[GM]Moira\n[GM]Hepa\n[GM]Clepio", "the open GM list follows NativeGmList");
            await Shot("gm-names", Group());
            // A removed notice closes its open box; with no notice the GM bar follows the time plate.
            await Click(notice.GetGlobalRect().GetCenter() - new Vector2(30, 0)); await Away();
            Net.I.EventTitles.Clear();
            // The preview world does not listen to the change event; the client's own refresh runs as live.
            DetailCall(world, "RefreshEventTitles");
            ((Action?)typeof(Net).GetField("EventTitlesChangedEvent", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Net.I))?.Invoke();
            await Frames(6);
            Require(!box.Visible, "a removed notice closes its box");
            Require(Mathf.Abs(gm.GetGlobalRect().Position.Y - time.GetGlobalRect().End.Y - L.BarGap) < .5f, "without notices the GM List bar follows the time plate " + gm.GetGlobalRect());
            await Shot("group-no-notices", Group());
            NativeGmList.Clear(); Premium(0, 0, 0);
            layer.RemoveChild(column); column.QueueFree(); stack.QueueFree(); world.Free(); await Frames(2);
        }

        var result = new { nation, variant, localRoot = Plugin.Kit.Local.Root, checks, screens, bounds };
        File.WriteAllText(System.IO.Path.Combine(output, prefix + "-myko-hud.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"MYKO_HUD_AUDIT_OK: {variant}, {checks.Count} checks, {screens.Count} screens");
    }
}
