using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Windows;
using KnightOnlineUiClassic.Native;
using System.Text.Json;

public partial class Preview
{
    private async Task CaptureProgressionAudit(int nation)
    {
        var output = OS.GetEnvironment("LIBREKO_AUDIT_OUTPUT_DIR"); System.IO.Directory.CreateDirectory(output);
        ProjectSettings.LoadResourcePack(ProjectSettings.GlobalizePath("res://../../build/client/source-content/knightonline.pck"), false);
        GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Disabled; GetWindow().Size = new Vector2I(1000, 700);
        AddChild(new ColorRect { Color = new Color("252822"), Size = new Vector2(1000, 700), MouseFilter = MouseFilterEnum.Ignore });
        var offline = new Net(); typeof(Net).GetProperty("I")!.SetValue(null, offline);
        Native.Call(offline, "SeedPreviewEnter", new MyInfo { Nation = nation, Name = "Classic Preview" });
        string prefix = nation == 1 ? "karus" : "human";
        var checks = new List<string>(); var screens = new List<object>();
        void Require(bool condition, string text) { if (!condition) throw new Exception("PROGRESSION_AUDIT: " + text); checks.Add(text); }
        async Task Frames(int count = 8) { for (int i = 0; i < count; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
        async Task Capture(HudWindow window, string state)
        {
            await Frames(); window.Position = ((GetViewportRect().Size - window.Size) / 2).Round(); await Frames(3);
            var panel = window.GetChildren().OfType<ClassicProgressionPanel>().Single();
            string file = prefix + "-" + window.Id + "-" + state + ".png";
            GetViewport().GetTexture().GetImage().SavePng(output + "/" + file); screens.Add(new { window = window.Id, state, file, bounds = window.GetGlobalRect().ToString() });
            Require(window.Size == ClassicProgressionLayout.Size(window.Id), state + " fixed Classic footprint " + window.Size);
            Require(GetViewportRect().Encloses(window.GetGlobalRect()), state + " complete window fits viewport");
            var inside = new Rect2(Vector2.Zero, panel.Size);
            foreach (var control in Descendants(panel).OfType<Control>().Where(c => c.IsVisibleInTree() && c.HasMeta("progression_expected_rect")))
            {
                var rect = control.GetMeta("progression_expected_rect").AsRect2();
                Require(control.GetRect() == rect, state + " rendered bounds " + control.Name + " " + control.GetRect());
                var global = new Rect2(control.GlobalPosition - panel.GlobalPosition, control.Size);
                Require(inside.Encloses(global), state + " inside frame " + control.Name);
            }
            Require(panel.Frame.Get("btn_close") is BaseButton { Visible: true } close && GetViewportRect().Encloses(close.GetGlobalRect()), state + " Close ornament reachable");
        }
        async Task Hover(Control control)
        {
            var at = control.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }); await Frames(3);
        }
        void Leave() => GetViewport().PushInput(new InputEventMouseMotion { Position = Vector2.Zero, GlobalPosition = Vector2.Zero });
        (World World, HudWindow Window, ClassicProgressionPanel Panel) Open(string fixture, string view)
        {
            var world = new World(); var window = (HudWindow)DetailCall(world, fixture, view)!;
            AddChild(window);
            var panel = ClassicProgressionSkin.Apply(window.Body)!;
            Require(panel != null, fixture + " " + view + " native controls prepared for the Classic composition");
            return (world, window, panel!);
        }
        T Named<T>(Node root, string name) where T : Node => Descendants(root).OfType<T>().First(n => n.Name == name);
        bool Connected(BaseButton button) => button.GetSignalConnectionList(BaseButton.SignalName.Pressed).Count > 0;
        async Task Done(World world, HudWindow window) { window.QueueFree(); world.QueueFree(); await Frames(2); }

        // Achievements.
        Require(AchievementData.Count > 0, "Achievement definitions are loaded from the client content");
        foreach (string view in new[] { "summary", "list", "claimed", "empty" })
        {
            var (world, window, panel) = Open("BuildAchievementsUiPreview", view);
            await Frames();
            var tabs = Descendants(panel).OfType<Button>().Where(b => b.HasMeta("progression_tab")).ToArray();
            Require(tabs.Length == 6, view + " summary and five category tabs on Classic tab plates");
            Require(tabs.Count(t => t.GetMeta("progression_tab_selected").AsBool()) == 1, view + " exactly one tab selected");
            for (int i = 1; i < tabs.Length; i++) Require(tabs[i].GetGlobalRect().Position.Y == tabs[0].GetGlobalRect().Position.Y && tabs[i].Size == tabs[0].Size, view + " tab row aligned " + tabs[i].Name);
            await Capture(window, view);
            if (view == "summary")
            {
                var page = Named<VBoxContainer>(panel, "ach_summary_page");
                Require(page.Visible && !Named<ScrollContainer>(panel, "ach_scroll").Visible, "Summary page replaces the list natively");
                Require(new Rect2(page.GlobalPosition, page.Size).Encloses(Descendants(page).OfType<Control>().Where(c => c.IsVisibleInTree() && c is PanelContainer).Select(c => c.GetGlobalRect()).Aggregate((a, b) => a.Merge(b))), "Summary sections stay inside the summary page");
                Require(Descendants(page).OfType<ProgressBar>().All(b => b.GetThemeStylebox("fill") is StyleBoxTexture), "Category progress uses the original gauge art");
                var war = tabs.First(t => t.Name == "ach_tab_" + (int)AchievementTab.War);
                await Hover(war); await Capture(window, "tab-hover"); Leave();
                war.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
                Require(!(bool)DetailField(world, "_achOnSummary")! && (AchievementTab)DetailField(world, "_achTab")! == AchievementTab.War, "Category tab invokes the native category selection");
                Require(war.GetMeta("progression_tab_selected").AsBool(), "Selected category keeps the pressed plate after the native refresh");
                Require(Named<ScrollContainer>(panel, "ach_scroll").Visible, "Category list becomes visible");
            }
            if (view == "list")
            {
                var list = Named<VBoxContainer>(panel, "ach_list");
                var rows = list.GetChildren().OfType<PanelContainer>().ToArray();
                Require(rows.Length > 6, "War category lists its achievements");
                var scroll = Named<ScrollContainer>(panel, "ach_scroll");
                Require(scroll.GetVScrollBar().Visible, "Long category scrolls inside its fixed inset");
                foreach (var row in rows.Where(r => r.IsVisibleInTree()))
                    Require(row.Size.X <= scroll.Size.X && row.GlobalPosition.X >= scroll.GlobalPosition.X, "Achievement row contained horizontally " + row.GetMeta("ach_row"));
                var claim = Descendants(list).OfType<Button>().First(b => b.Name == "ach_claim");
                Require(Connected(claim) && claim.Size.X >= 76 && claim.GetThemeStylebox("normal") is StyleBoxTexture, "Claim keeps its native callback on a Classic button");
                Require(rows.First().GetMeta("ach_claimable").AsBool(), "Claimable achievements are listed first, as natively sorted");
                void WholeRowsOnly(string state)
                {
                    var view = scroll.GetGlobalRect().Grow(.5f);
                    var shown = rows.Where(r => r.Modulate.A > 0).ToArray();
                    Require(shown.Length > 0 && shown.All(r => view.Encloses(r.GetGlobalRect())), state + " shows whole achievement rows only");
                    Require(rows.Where(r => r.Modulate.A == 0).All(r => !view.Encloses(r.GetGlobalRect())
                        && Descendants(r).OfType<BaseButton>().All(b => b.MouseFilter == MouseFilterEnum.Ignore)), state + " hides and disables rows cut by the list edge");
                    Require(shown.Any(r => Math.Abs(r.GlobalPosition.Y - scroll.GlobalPosition.Y) < .5f), state + " starts the list at a whole row");
                }
                WholeRowsOnly("list");
                var bar = scroll.GetVScrollBar();
                bar.Value = rows[1].Position.Y - 10; await Frames();
                Require(Math.Abs(bar.Value - rows[1].Position.Y) < .5f, "Scrolling down steps to the next whole row");
                WholeRowsOnly("list scrolled");
                bar.Value = bar.MaxValue; await Frames();
                Require(rows[^1].Modulate.A > 0, "The last achievement row scrolls fully into view");
                WholeRowsOnly("list end");
                bar.Value = 0; await Frames();
                await Hover(claim); await Capture(window, "claim-hover"); Leave();
            }
            if (view == "claimed")
                Require(Named<VBoxContainer>(panel, "ach_list").GetChildren().OfType<PanelContainer>().All(r => !r.GetMeta("ach_claimed").AsBool()), "Hide claimed keeps the native filter");
            if (view == "empty")
            {
                Require(Named<Label>(panel, "ach_summary").Text.Length >= 0, "Empty state renders");
            }
            Named<Control>(panel.Frame, "btn_close").EmitSignal(BaseButton.SignalName.Pressed);
            Require(!window.Visible && !(bool)DetailField(world, "_achShown")!, view + " Close ornament invokes the native close");
            await Done(world, window);
        }

        // Attendance.
        foreach (string view in new[] { "progress", "empty", "complete", "failed" })
        {
            var (world, window, panel) = Open("BuildAttendanceUiPreview", view);
            await Frames();
            var daily = Named<GridContainer>(panel, "attendance_daily");
            var bonus = Named<GridContainer>(panel, "attendance_bonus");
            Require(daily.GetChildren().OfType<VBoxContainer>().Count() == Net.AttendanceDailySlots && bonus.GetChildren().OfType<VBoxContainer>().Count() == Net.AttendanceBonusSlots, view + " every daily and cumulative reward");
            await Frames(2);
            foreach (var grid in new[] { daily, bonus })
                foreach (var socket in Descendants(grid).OfType<Button>().Where(b => b.Name == "attendance_socket"))
                {
                    Require(socket.Size == new Vector2(ClassicProgressionLayout.Cell, ClassicProgressionLayout.Cell), view + " Classic 45-pixel reward cell");
                    Require(grid.GetGlobalRect().Encloses(socket.GetGlobalRect()), view + " reward cell inside its inset");
                }
            foreach (var caption in Descendants(daily).OfType<Label>().Where(l => l.GetParent() is VBoxContainer))
                Require(caption.GetGlobalRect().End.Y <= daily.GetGlobalRect().End.Y, view + " caption contained " + caption.Text);
            var claimable = Descendants(daily).OfType<VBoxContainer>().Concat(Descendants(bonus).OfType<VBoxContainer>()).Where(c => c.GetMeta("attendance_claimable", false).AsBool()).ToArray();
            if (view != "complete")
            {
                Require(claimable.Length > 0 && claimable.All(c => Descendants(c).Any(n => n.Name == "claim_mark")), view + " claimable rewards are marked");
                var socket = Descendants(claimable[0]).OfType<Button>().First();
                Require(Connected(socket), view + " claimable reward keeps the native claim callback");
            }
            else Require(claimable.Length == 0, "Complete board has nothing left to claim");
            if (view == "failed") Require(Named<Label>(panel, "attendance_notice").Visible, "Claim refusal notice stays visible inside the frame");
            await Capture(window, view);
            if (view == "progress")
            {
                await Hover(Descendants(claimable[0]).OfType<Button>().First()); await Capture(window, "hover"); Leave();
            }
            Named<Control>(panel.Frame, "btn_close").EmitSignal(BaseButton.SignalName.Pressed);
            Require(!window.Visible && !(bool)DetailField(world, "_attendanceShown")!, view + " Close ornament invokes the native close");
            await Done(world, window);
        }

        // Rankings.
        foreach (string view in new[] { "full", "short", "empty", "loading" })
        {
            var (world, window, panel) = Open("BuildRankUiPreview", view);
            await Frames();
            var mineTab = Named<Button>(panel, nation == 1 ? "rank_tab_karus" : "rank_tab_elmorad");
            Require(mineTab.ButtonPressed, view + " opens on the player's nation");
            await Capture(window, view);
            var rows = Named<VBoxContainer>(panel, "rank_rows").GetChildren().OfType<PanelContainer>().ToArray();
            if (rows.Length > 0)
            {
                var headers = Enumerable.Range(0, 4).Select(i => Named<Label>(panel, "rank_header_" + i)).ToArray();
                var cells = rows[0].GetChildren().OfType<HBoxContainer>().Single().GetChildren().OfType<Label>().ToArray();
                for (int i = 0; i < 4; i++) Require(Mathf.Abs(cells[i].GetGlobalRect().Position.X - headers[i].GetGlobalRect().Position.X) < .5f, view + " column " + i + " aligns with its heading");
                Require(Mathf.Abs(cells[3].GetGlobalRect().End.X - headers[3].GetGlobalRect().End.X) < .5f, view + " loyalty right edge aligns with its heading");
                Require(rows.All(r => r.Size.Y == ClassicProgressionLayout.Row), view + " uniform 22-pixel rows");
                Require(cells.All(c => c.ClipText), view + " long names and clans are clipped to their columns");
            }
            if (view == "full")
            {
                Require(Named<ScrollContainer>(panel, "rank_scroll").GetVScrollBar().Visible, "Thirty players scroll inside the table");
                var other = Named<Button>(panel, nation == 1 ? "rank_tab_elmorad" : "rank_tab_karus");
                await Hover(other); await Capture(window, "tab-hover"); Leave();
                other.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
                Require((int)DetailField(world, "_rankNation")! == (nation == 1 ? 2 : 1) && other.ButtonPressed && !mineTab.ButtonPressed, "Nation tab invokes the native ladder filter");
                await Capture(window, "other-nation");
                Named<Button>(panel, "rank_refresh").EmitSignal(BaseButton.SignalName.Pressed);
                Require(Named<Label>(panel, "rank_status").Text.StartsWith("Loading"), "Refresh invokes the native request");
            }
            Named<Control>(panel.Frame, "btn_close").EmitSignal(BaseButton.SignalName.Pressed);
            Require(!window.Visible && !(bool)DetailField(world, "_rankShown")!, view + " Close ornament invokes the native close");
            await Done(world, window);
        }

        // Genie.
        foreach (string view in new[] { "configured", "empty", "running", "pending", "claimed" })
        {
            var (world, window, panel) = Open("BuildGenieUiPreview", view);
            var layer = (CanvasLayer)DetailField(world, "_genieLayer")!; world.RemoveChild(layer); AddChild(layer);
            await Frames();
            var slots = Enumerable.Range(0, 24).Select(i => Named<Button>(panel, "genie_slot_" + i)).ToArray();
            Require(slots.All(s => s.Size == new Vector2(36, 36) && s.GetThemeStylebox("normal") is StyleBoxTexture), view + " 24 skill slots on Classic cells");
            await Capture(window, view + "-main");
            var start = Named<Button>(panel, "genie_start");
            if (view == "configured")
            {
                Require(slots.Count(s => s.Icon != null) > 0, "Configured skills show their icons in the cells");
                var list = Named<ItemList>(panel, "genie_monster_list");
                int before = list.ItemCount; list.Select(0); Named<Button>(panel, "genie_monster_remove").EmitSignal(BaseButton.SignalName.Pressed); await Frames(2);
                Require(list.ItemCount == before - 1, "Remove invokes the native monster list edit");
                await Hover(slots[0]); await Capture(window, "slot-hover"); Leave();
                slots[1].EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
                var picker = (PopupMenu)DetailField(world, "_genieSkillPicker")!;
                Require(picker.Visible && (int)DetailField(world, "_genieEditingSlot")! == 1, "Skill cell opens the native skill picker");
                Require(picker.GetThemeStylebox("panel") is StyleBoxFlat { BgColor.A: 1 }, "Skill picker uses the Classic popup material");
                await Capture(window, "skill-picker"); picker.Hide(); await Frames(2);
                var mode = Named<CheckButton>(panel, "genie_mode_0"); bool was = mode.ButtonPressed;
                mode.EmitSignal(BaseButton.SignalName.Pressed); mode.ButtonPressed = !was;
                Require(((CheckButton[])DetailField(world, "_genieModes")!)[0].ButtonPressed == !was, "Mode toggles are the native settings controls");
                Require(Connected(Named<Button>(panel, "genie_spirit")) && Connected(Named<Button>(panel, "genie_save")) && Connected(start), "Start, Save Settings and Spirit of Genie keep native callbacks");
            }
            if (view == "running") Require(start.Text == "Stop" && !start.Disabled, "Running genie offers Stop");
            if (view == "pending") Require(start.Disabled && start.GetThemeStylebox("disabled") is StyleBoxTexture, "Pending start is visibly disabled");
            var misc = Named<Button>(panel, "genie_tab_misc");
            misc.EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Require(panel.GenieMiscShown && ((TabContainer)Named<Control>(window, "genie_tabs")).CurrentTab == 1, view + " Misc tab shows the native Misc page");
            Require(!Named<Control>(panel, "genie_slot_0").IsVisibleInTree() && Named<Control>(panel, "genie_ptcode").IsVisibleInTree(), view + " pages never overlap");
            var claim = Named<Button>(panel, "genie_claim");
            Require(claim.Disabled == (view == "claimed"), view + " daily reward button follows the native status");
            await Capture(window, view + "-misc");
            if (view == "configured")
            {
                Named<Button>(panel, "genie_scroll_0").EmitSignal(BaseButton.SignalName.Pressed); await Frames(3);
                Require(((PopupMenu)DetailField(world, "_genieScrollPicker")!).Visible, "Scroll cell opens the native scroll picker");
                ((PopupMenu)DetailField(world, "_genieScrollPicker")!).Hide();
            }
            Named<Button>(panel, "genie_tab_main").EmitSignal(BaseButton.SignalName.Pressed); await Frames();
            Require(!panel.GenieMiscShown, view + " Main tab returns to the main page");
            Named<Control>(panel.Frame, "btn_close").EmitSignal(BaseButton.SignalName.Pressed);
            Require(!window.Visible && !(bool)DetailField(world, "_genieShown")!, view + " Close ornament invokes the native close");
            layer.QueueFree();
            await Done(world, window);
        }

        System.IO.File.WriteAllText(output + "/" + prefix + "-progression.json",
            JsonSerializer.Serialize(new { nation, checks, screens }, new JsonSerializerOptions { WriteIndented = true }));
        offline.Free(); GD.Print("PROGRESSION_AUDIT_OK " + checks.Count);
    }
}
