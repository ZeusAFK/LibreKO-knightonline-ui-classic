using Godot;
using LibreKO;
using LibreKO.Network;
using KnightOnlineUiClassic;
using KnightOnlineUiClassic.Windows;
using System.Reflection;
using System.Text.Json;
using Spec = KnightOnlineUiClassic.Layout.ClassicOverlayLayout;

// Native world overlays without a window id: confirmation prompts, the death notice, broadcast
// banners, the zone chip, war score strips, the player menu and the loot window. Each overlay is
// captured once in its native look, then prepared and skinned and captured in its main states.
public partial class Preview
{
    private async Task CaptureWorldOverlaysAudit(int nation)
    {
        string output = OS.GetEnvironment("LIBREKO_AUDIT_OUTPUT_DIR");
        if (output.Length == 0) output = ProjectSettings.GlobalizePath("res://../../research/world-overlays");
        System.IO.Directory.CreateDirectory(output);
        string prefix = nation == 1 ? "karus" : "human";
        var window = GetWindow(); window.ContentScaleMode = Window.ContentScaleModeEnum.Disabled; window.Size = new Vector2I(1280, 800);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(1275, 795), GlobalPosition = new Vector2(1275, 795) });
        // A dark ground with a light band: banners and plaques must read over both.
        AddChild(new ColorRect { Color = new Color("2f332a"), Size = new Vector2(1280, 800), MouseFilter = MouseFilterEnum.Ignore });
        AddChild(new ColorRect { Color = new Color("b2ac98"), Position = new Vector2(0, 120), Size = new Vector2(1280, 130), MouseFilter = MouseFilterEnum.Ignore });
        var net = new Net(); typeof(Net).GetProperty("I")!.SetValue(null, net);
        foreach (string pack in new[] { "build/client/LibreKO.pck", "build/client/source-content/knightonline.pck" })
            if (!ProjectSettings.LoadResourcePack(ProjectSettings.GlobalizePath("res://../../" + pack), false)) throw new Exception("Missing world overlay audit pack: " + pack);
        LibreKO.Domain.ItemData.EnsureLoaded();
        var catalog = LibreKO.Domain.ItemData.All().Where(i => i.Slot < 100 && !string.IsNullOrWhiteSpace(i.Name)).OrderBy(i => i.Id).ToArray();
        int stackable = catalog.Where(i => i.Countable > 0).OrderByDescending(i => i.Name.Contains("Potion")).First().Id;
        var singles = catalog.Where(i => i.Countable == 0).DistinctBy(i => i.Icon).Select(i => i.Id).Take(3).ToList();
        singles.Add(catalog.Where(i => i.Countable == 0).OrderByDescending(i => i.Name.Length).First().Id);
        var world = new World();
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        T Field<T>(string name) => (T)typeof(World).GetField(name, Flags)!.GetValue(world)!;
        void SetField(string name, object? value) { var f = typeof(World).GetField(name, Flags)!; f.SetValue(world, value == null ? null : Convert.ChangeType(value, f.FieldType)); }
        void Call(string method, params object?[] args) => DetailCall(world, method, args);
        Call("BuildDeathDialog"); Call("TownRecallInit"); Call("BuildChallengePrompt"); Call("BuildPartyPanel");
        Call("BuildUpgradeNoticeBanner"); Call("BuildShoutBanner"); Call("BuildSeasonalBanner"); Call("BuildBattleEventUi");
        Call("BuildClanBattleBanner"); Call("BuildZoneAbilityChip");
        SetField("_bdwHudLayer", new CanvasLayer { Layer = 95 }); world.AddChild(Field<CanvasLayer>("_bdwHudLayer")); Call("BuildBdwScoreBanner");
        Call("PlayerMenuInit"); Call("BuildLootWindow"); Call("BuildLootPrompt");
        foreach (var layer in world.GetChildren().OfType<CanvasLayer>().ToArray()) layer.Reparent(this);
        Field<PopupMenu>("_playerMenu").Reparent(this);

        var checks = new List<string>(); var failures = new List<string>(); var screens = new List<object>();
        void Require(bool valid, string text) { if (valid) checks.Add(text); else { failures.Add(text); GD.PushError("WORLD_OVERLAYS_AUDIT: " + text); } }
        async Task Frames(int count = 6) { for (int i = 0; i < count; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
        object R(Rect2 r) => new { x = r.Position.X, y = r.Position.Y, w = r.Size.X, h = r.Size.Y };
        async Task Shot(string state, params (string Name, Rect2 Rect)[] bounds)
        {
            await Frames();
            string file = prefix + "-" + state + ".png";
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(output, file));
            screens.Add(new { file, state, bounds = bounds.ToDictionary(b => b.Name, b => R(b.Rect)) });
        }
        Rect2 Global(Control c) => c.GetGlobalRect();
        Rect2 Win(Window w) => new(w.Position, w.Size);
        bool Inside(Rect2 outer, Rect2 inner) => outer.Grow(0.01f).Encloses(inner);
        bool Whole(Rect2 r) => r.Position == r.Position.Round() && r.Size == r.Size.Round();
        var viewport = GetViewportRect();
        // A label's text fits its rectangle: one line within the width, wrapped lines within the height.
        bool Fits(Label label)
        {
            var font = label.GetThemeFont("font"); int size = label.GetThemeFontSize("font_size");
            if (label.AutowrapMode == TextServer.AutowrapMode.Off)
                return font.GetStringSize(label.Text, HorizontalAlignment.Left, -1, size).X <= label.Size.X + 0.5f;
            int lines = label.GetLineCount();
            return lines * font.GetHeight(size) + (lines - 1) * label.GetThemeConstant("line_spacing") <= label.Size.Y + 0.5f;
        }
        string Measure(Label label) => label.GetThemeFont("font").GetStringSize(label.Text, HorizontalAlignment.Left, -1, label.GetThemeFontSize("font_size")) + " lines " + label.GetLineCount();
        void Expected(Control control, Control parent, string what)
        {
            var expected = control.GetMeta(ClassicWorldOverlays.ExpectedRect).AsRect2();
            var actual = new Rect2(control.GlobalPosition - parent.GlobalPosition, control.Size);
            Require(actual == expected, $"{what}: rendered bounds {actual} match the declared {expected}");
            Require(Whole(control.GetGlobalRect()), $"{what}: whole-pixel geometry");
        }
        void ButtonArt(Button button, string what)
        {
            var caption = button.GetNodeOrNull<Label>("caption");
            Require(caption != null && caption.Text == button.Text && caption.IsVisibleInTree(), $"{what}: the native text '{button.Text}' shows on the art caption");
            Require(button.GetThemeStylebox("normal") is StyleBoxTexture && button.GetThemeStylebox("hover") is StyleBoxTexture
                && button.GetThemeStylebox("pressed") is StyleBoxTexture && button.GetThemeStylebox("disabled") is StyleBoxTexture, $"{what}: original art in every state");
            if (caption != null) Require(Fits(caption), $"{what}: caption fits");
        }

        // ---- Native look (before) ---------------------------------------------------------------------
        var invite = Field<ConfirmationDialog>("_inviteAskDialog");
        var duel = Field<ConfirmationDialog>("_challengeAskDialog");
        var recall = Field<ConfirmationDialog>("_townRecallDialog");
        var deathLayer = Field<CanvasLayer>("_deathLayer");
        var deathPanel = Field<PanelContainer>("_deathPanel");
        var upgradeLayer = Field<CanvasLayer>("_upgradeNoticeLayer"); var upgradeLabel = Field<Label>("_upgradeNoticeLabel");
        var shoutLayer = Field<CanvasLayer>("_shoutLayer"); var shoutLabel = Field<Label>("_shoutLabel");
        var seasonalLayer = Field<CanvasLayer>("_seasonalLayer");
        var battleBanner = Field<PanelContainer>("_battleeventBanner"); var battleBannerLabel = Field<Label>("_battleeventBannerLbl");
        var battleResult = Field<PanelContainer>("_battleeventResult"); var battleResultLabel = Field<Label>("_battleeventResultLbl");
        var clanBattle = Field<PanelContainer>("_clanBattlePanel"); var clanBattleLabel = Field<Label>("_clanBattleLabel");
        var chip = Field<PanelContainer>("_zoneabilityChip"); var chipLabel = Field<Label>("_zoneabilityTypeLbl");
        var board = Field<WarScoreStrip>("_battleeventBoard"); var bdw = Field<WarScoreStrip>("_bdwScoreBanner");
        var menu = Field<PopupMenu>("_playerMenu"); var partyMenu = Field<PopupMenu>("_partyCtxMenu");
        var lootLayer = Field<CanvasLayer>("_lootLayer"); var lootPanel = Field<PanelContainer>("_lootPanel");
        var lootRows = Field<VBoxContainer>("_lootListBox"); var lootPrompt = Field<Label>("_lootPrompt");

        void ShowBanners()
        {
            upgradeLabel.Text = "Guardian has successfully upgraded Iron Bow(+7)!"; upgradeLayer.Visible = true;
            battleBannerLabel.Text = "The Battle Zone is now OPEN!  (Napies Gorge)"; battleBanner.Visible = true;
            clanBattleLabel.Text = "The clan battle has begun!"; clanBattle.Visible = true;
            chipLabel.Text = "PK Zone"; chipLabel.AddThemeColorOverride("font_color", UiTheme.Bad); chip.Visible = true;
            board.Centre = "12:34"; board.SetScores(1250, 980); board.Visible = true;
            bdw.Centre = "Border Defense War"; bdw.SetScores(14, 22); bdw.SetStatus("Altar held by El Morad", UiTheme.Good); bdw.Visible = true;
            lootPrompt.Visible = true;
        }
        void PlaceBanners()
        {
            battleBanner.Position = new Vector2(Mathf.Round((viewport.Size.X - battleBanner.Size.X) / 2), 268);
            clanBattle.Position = new Vector2(Mathf.Round((viewport.Size.X - clanBattle.Size.X) / 2), 316);
            board.Position = new Vector2(Mathf.Round((viewport.Size.X - board.Size.X) / 2), 380);
            bdw.Position = new Vector2(Mathf.Round((viewport.Size.X - bdw.Size.X) / 2), 460);
            lootPrompt.Position = new Vector2(520, 560);
        }
        void HideBanners()
        {
            foreach (var layer in new[] { upgradeLayer, shoutLayer, seasonalLayer }) layer.Visible = false;
            foreach (var c in new Control[] { battleBanner, battleResult, clanBattle, chip, board, bdw, lootPrompt }) c.Visible = false;
        }
        invite.DialogText = "Guardian invites you to a party.\nJoin?"; invite.PopupCentered();
        await Shot("native-prompt"); invite.Hide();
        Call("OpenInvitePrompt"); Field<LineEdit>("_inviteNameInput").Text = "Longestcharactername";
        await Shot("native-invite-name"); Field<PanelContainer>("_inviteNamePanel").Visible = false;
        Call("OnDeathExpLoss", 1_234_567L); Call("ShowDeathDialog");
        await Shot("native-death"); Call("HideDeathDialog");
        ShowBanners(); await Frames(); PlaceBanners();
        await Shot("native-banners"); HideBanners();
        menu.Clear(); foreach (var item in new[] { "Request a party", "Report", "User trade", "Whisper", "Add friend", "User Information", "Duel", "Equipment View", "Copy name" }) menu.AddItem(item);
        menu.ResetSize(); menu.Position = new Vector2I(560, 260); menu.Popup();
        await Shot("native-menu"); menu.Hide();
        var gold = new LootEntry(Net.GoldItemId, 1520);
        SetField("_openBundleId", 7);
        Call("OnLootContents", 7, new List<LootEntry> { gold, new(singles[0], 1), new(stackable, 25) });
        lootPanel.Position = new Vector2(560, 260);
        await Shot("native-loot"); Call("CloseLoot");

        // ---- Classic ----------------------------------------------------------------------------------
        NativeOverlays.Prepare(world);
        await Frames(2);
        foreach (var (node, what) in new (Node, string)[] { (invite, "party invite"), (duel, "duel challenge"), (recall, "town recall"), (deathPanel, "death notice"),
            (upgradeLabel.GetParent().GetParent(), "upgrade banner"), (battleBanner, "battle banner"), (clanBattle, "clan battle banner"), (chip, "zone chip"),
            (board, "battle score"), (bdw, "border war score"), (menu, "player menu"), (partyMenu, "party menu"), (lootPanel, "loot window"), (lootPrompt, "loot prompt") })
            Require(node.HasMeta("classic_overlay"), $"{what} has the Classic skin");

        async Task Prompt(ConfirmationDialog dialog, string state, string title, bool decline)
        {
            await Frames();
            var art = dialog.GetNode<Control>("classic_prompt/message_art");
            var heading = dialog.GetNode<Label>("classic_prompt/prompt_title");
            var rect = Win(dialog);
            Require(dialog.Visible && dialog.Borderless && rect.Size.X == Spec.Message.Width && rect.Size.Y >= Spec.Message.Height, $"{state}: borderless message box {rect.Size}");
            Require(Inside(viewport, rect), $"{state}: inside the viewport");
            Require(art.Size == new Vector2(Spec.Message.Width, rect.Size.Y) && art.Position == Vector2.Zero, $"{state}: message art covers the box");
            Require(heading.Text == title && heading.Position == Spec.Message.Title.Position && Fits(heading), $"{state}: native title '{title}' in the title band");
            var label = dialog.GetLabel();
            var text = new Rect2(label.Position, label.Size);
            Require(Inside(new Rect2(Spec.Message.Text.Position, new Vector2(Spec.Message.Text.Size.X, rect.Size.Y - Spec.Message.Text.Position.Y - Spec.Message.ButtonBottom - Spec.Message.Button.Y)), text),
                $"{state}: message {text} inside the message band");
            Require(Fits(label), $"{state}: message wraps inside its band");
            if (rect.Size.Y == Spec.Message.Height) Require(text == Spec.Message.Text, $"{state}: message band {text} is the original {Spec.Message.Text}");
            var ok = dialog.GetOkButton(); var cancel = dialog.GetCancelButton();
            // Controls of an embedded window report global rectangles in window space.
            var okRect = ok.GetGlobalRect(); var cancelRect = cancel.GetGlobalRect();
            Require(okRect.Size == Spec.Message.Button && cancelRect.Size == Spec.Message.Button, $"{state}: buttons use the 97 x 29 art ({okRect.Size}, {cancelRect.Size})");
            Require(okRect == Spec.Message.Left(rect.Size.Y) && cancelRect == Spec.Message.Right(rect.Size.Y), $"{state}: buttons at the original two-button places ({okRect}, {cancelRect})");
            ButtonArt(ok, state + " confirm"); ButtonArt(cancel, state + " cancel");
            Require(ok.HasFocus() && ok.GetThemeStylebox("focus") is StyleBoxEmpty, $"{state}: the confirm button keeps the native keyboard focus without a focus frame");
            await Shot(state, ("box", rect), ("ok", okRect), ("cancel", cancelRect), ("message", text));
            bool confirmed = false, cancelled = false;
            dialog.Confirmed += () => confirmed = true; dialog.Canceled += () => cancelled = true;
            (decline ? cancel : ok).EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
            Require(!dialog.Visible && (decline ? cancelled : confirmed), $"{state}: the native {(decline ? "cancel" : "confirm")} callback runs and the prompt closes");
            if (dialog.Visible) dialog.Hide();
        }
        SetField("_invitePending", true);
        invite.DialogText = "Guardian invites you to a party.\nJoin?"; invite.PopupCentered();
        await Prompt(invite, "party-invite", "Party Invite", false);
        Require(!Field<bool>("_invitePending"), "party invite: Join answers the native invite");
        SetField("_challengeRequestPending", true);
        duel.DialogText = "Extraordinarylongname challenges you to a duel.\nAccept and warp to the arena?"; duel.PopupCentered();
        await Prompt(duel, "duel-challenge", "Duel challenge", true);
        Require(!Field<bool>("_challengeRequestPending"), "duel challenge: closing declines the native request");
        recall.PopupCentered();
        await Prompt(recall, "town-recall", "Town Recall", true);

        // Invite by name from the party window: the message box with its edit field; Cancel and Send stay native.
        var namePanel = Field<PanelContainer>("_inviteNamePanel"); var nameInput = Field<LineEdit>("_inviteNameInput");
        Require(namePanel.HasMeta("classic_overlay"), "invite by name has the Classic skin");
        Call("OpenInvitePrompt"); nameInput.Text = "Longestcharactername";
        await Frames();
        var nameBox = namePanel.GetNode<Control>("classic_invite_name");
        Require(namePanel.Visible && nameBox.Size == Spec.NamePrompt.Size && Global(nameBox).Position == ((viewport.Size - Spec.NamePrompt.Size) / 2).Round(), $"invite by name: centred message box {Global(nameBox)}");
        foreach (var control in nameBox.GetChildren().OfType<Control>().Where(c => c.HasMeta(ClassicWorldOverlays.ExpectedRect)))
        {
            Expected(control, nameBox, "invite by name " + control.Name);
            if (control is Label label) Require(Fits(label), $"invite by name: '{label.Text}' fits");
            if (control is Button button) ButtonArt(button, "invite by name " + button.Text);
        }
        Require(nameInput.GetThemeFont("font").GetStringSize(nameInput.Text, HorizontalAlignment.Left, -1, nameInput.GetThemeFontSize("font_size")).X <= nameInput.Size.X - 12,
            "invite by name: a name at the native 20-character limit fits the edit field");
        await Shot("party-invite-name", ("box", Global(nameBox)), ("input", Global(nameInput)));
        nameBox.GetChildren().OfType<Button>().Single(b => b.Text == "Cancel").EmitSignal(BaseButton.SignalName.Pressed);
        Require(!namePanel.Visible, "invite by name: Cancel runs the native close");
        Call("OpenInvitePrompt"); nameInput.Text = "Guardian";
        nameBox.GetChildren().OfType<Button>().Single(b => b.Text == "Send").EmitSignal(BaseButton.SignalName.Pressed);
        Require(!namePanel.Visible, "invite by name: Send runs the native invite");

        // Death notice: experience loss, the pending (disabled) state and the native OK.
        Call("OnDeathExpLoss", 1_234_567_890L); Call("ShowDeathDialog");
        await Frames();
        var death = deathPanel.GetNode<Control>("classic_death");
        var deathLabels = death.GetChildren().OfType<Label>().Where(l => l.HasMeta(ClassicWorldOverlays.ExpectedRect)).ToArray();
        var deathOk = Descendants(death).OfType<Button>().Single();
        async Task DeathState(string state)
        {
            await Frames();
            var centre = (viewport.Size - Spec.Death.Size) / 2;
            Require(death.Size == Spec.Death.Size && Math.Abs(Global(death).Position.X - centre.X) <= 1 && Math.Abs(Global(death).Position.Y - centre.Y) <= 1 && Whole(Global(death)),
                $"{state}: centred original message box on whole pixels {Global(death)}");
            foreach (var label in deathLabels) { Expected(label, death, $"{state} '{label.Text}'"); Require(Fits(label), $"{state}: '{label.Text}' fits ({Measure(label)} in {label.Size})"); }
            Expected(deathOk, death, state + " OK"); ButtonArt(deathOk, state + " OK");
            await Shot(state, ("box", Global(death)), ("ok", Global(deathOk)));
        }
        Require(deathLabels.Length == 2, "death notice: loss and hint labels placed");
        await DeathState("death");
        Call("MarkRespawnPending");
        await Frames(2);
        Require(deathOk.Disabled && deathOk.GetNode<Label>("caption").GetThemeColor("font_color") != Colors.White, "death notice: the pending OK is disabled and greyed");
        await DeathState("death-pending");
        Call("ArmRespawnButton", "Press OK to teleport back to the re-spawn point.");
        SetField("_selfDead", false);
        deathOk.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        Require(!deathLayer.Visible, "death notice: OK runs the native respawn callback");

        // Banners, chip and score strips over light and dark ground.
        ShowBanners(); await Frames(); PlaceBanners();
        await Frames();
        foreach (var label in new[] { upgradeLabel, battleBannerLabel, clanBattleLabel, lootPrompt })
            Require(label.GetThemeFont("font") == Plugin.Kit.Bold && label.GetThemeConstant("outline_size") > 0 && Fits(label), $"banner '{label.Text}': outlined Arial Bold that fits");
        foreach (var plate in new PanelContainer[] { battleBanner, clanBattle })
            Require(plate.GetThemeStylebox("panel") is StyleBoxEmpty, $"banner {plate.Name}: no flat panel");
        Require(chipLabel.GetThemeColor("font_color") == ClassicWorldOverlays.PlaqueInk && chip.Size.Y == Spec.Plaque.Height && Inside(viewport, Global(chip)),
            $"zone chip: plaque height {chip.Size.Y} (label {chipLabel.GetCombinedMinimumSize()}) and plaque ink over the native tint");
        var chipText = chipLabel.GetThemeFont("font").GetStringSize(chipLabel.Text, HorizontalAlignment.Left, -1, chipLabel.GetThemeFontSize("font_size")).X;
        float left = Global(chipLabel).Position.X + (chipLabel.Size.X - chipText) / 2 - Global(chip).Position.X, right = Global(chip).End.X - (Global(chipLabel).Position.X + (chipLabel.Size.X + chipText) / 2);
        Require(Math.Abs(left - right) <= 1, $"zone chip: name centred on the plaque ({left:0.#} / {right:0.#})");
        foreach (var strip in new[] { board, bdw })
        {
            Require(Inside(viewport, Global(strip)), $"{strip.Name}: inside the viewport");
            foreach (var label in Descendants(strip).OfType<Label>().Where(l => l.IsVisibleInTree()))
                Require(Inside(Global(strip), Global(label)) && Fits(label) && label.GetThemeFont("font") == Plugin.Kit.Bold, $"{strip.Name}: '{label.Text}' inside the plaque");
            var bars = Descendants(strip).OfType<ColorRect>().ToArray();
            Require(bars.All(b => b.Color.A == 0 && b.Size.Y == Spec.Plaque.BarHeight), $"{strip.Name}: original bar art at {Spec.Plaque.BarHeight} px");
        }
        await Shot("banners", ("upgrade", Global(upgradeLabel)), ("battle", Global(battleBanner)), ("clan", Global(clanBattle)), ("chip", Global(chip)),
            ("score", Global(board)), ("bdw", Global(bdw)), ("lootPrompt", Global(lootPrompt)));
        HideBanners();
        shoutLabel.Text = "[Shout] Extraordinarylongname: selling Iron Bow(+7), whisper me in Moradon!"; shoutLayer.Visible = true;
        battleResultLabel.Text = "VICTORY!"; battleResult.Visible = true;
        await Frames();
        Require(Fits(shoutLabel) && Inside(viewport, Global(shoutLabel)), "shout banner: long line fits the viewport");
        await Shot("banners-result", ("shout", Global(shoutLabel)), ("result", Global(battleResult)));
        HideBanners();

        // Player menu.
        menu.Popup(); menu.SetFocusedItem(3); await Frames();
        Require(menu.GetFocusedItem() == 3 && menu.GetThemeStylebox("hover") is StyleBoxFlat { BgColor.A: > 0.5f }, "player menu: the hovered row shows the Classic selection");
        Require(menu.GetThemeFont("font") == Plugin.Kit.Regular && menu.GetThemeStylebox("panel") is StyleBoxFlat && Inside(viewport, Win(menu)), $"player menu: Classic type and panel inside the viewport {Win(menu)}");
        await Shot("menu", ("menu", Win(menu)));
        menu.Hide();

        // Loot: gold and two items, then a full grid with a long name; a cell click takes through the native row.
        SetField("_openBundleId", 7);
        Call("OnLootContents", 7, new List<LootEntry> { gold, new(singles[0], 1), new(stackable, 25) });
        lootPanel.Position = new Vector2(560, 260);
        await Frames();
        var loot = lootPanel.GetNode<Control>("classic_loot");
        async Task LootState(string state, int filled)
        {
            await Frames();
            Require(lootPanel.Size == Spec.Loot.Size && loot.Size == Spec.Loot.Size && Inside(viewport, Global(loot)), $"{state}: original grid size {lootPanel.Size}");
            foreach (var name in new[] { "loot_header", "loot_close" })
            {
                var control = loot.FindChild(name, true, false) as Control;
                Require(control != null && control.IsVisibleInTree(), $"{state}: {name} visible");
                if (control != null) { var expected = control.GetMeta(ClassicWorldOverlays.ExpectedRect).AsRect2(); Require(new Rect2(control.GlobalPosition - loot.GlobalPosition, control.Size) == expected, $"{state}: {name} at {expected}"); }
            }
            var title = (Label)loot.FindChild("loot_title", true, false)!;
            Require(Fits(title) && Inside(Global((Control)loot.FindChild("loot_header", true, false)!), Global(title)), $"{state}: loot title fits the plaque");
            for (int i = 0; i < Spec.Loot.Cells.Length; i++)
            {
                var cell = loot.GetNode<Control>($"loot_cell_{i}");
                Expected(cell, loot, $"{state} cell {i}");
                var icon = cell.GetNode<TextureRect>("icon");
                Require((icon.Texture != null) == (i < filled), $"{state}: cell {i} {(i < filled ? "shows its item" : "is empty")}");
            }
            await Shot(state, ("loot", Global(loot)));
        }
        await LootState("loot", 3);
        Require(loot.GetNode<Control>("loot_cell_0").GetNode<Label>("item_count").Text == "1520" && loot.GetNode<Control>("loot_cell_2").GetNode<Label>("item_count").Text == "25"
            && loot.GetNode<Control>("loot_cell_1").GetNode<Label>("item_count").Text == "", "loot: stack counts from the native rows; single items show none");
        Call("OnLootContents", 7, new List<LootEntry> { gold, new(singles[0], 1), new(stackable, 25), new(singles[1], 1), new(singles[3], 1), new(singles[2], 1) });
        await LootState("loot-full", 6);
        var taken = -1;
        var rows = lootRows.GetChildren().OfType<Control>().Where(r => !r.IsQueuedForDeletion()).ToArray();
        rows[4].GuiInput += ev => { if (ev is InputEventMouseButton { Pressed: true }) taken = 4; };
        loot.GetNode<Control>("loot_cell_4").EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left });
        Require(taken == 4, "loot: a cell click takes its item through the native row");
        ((Button)loot.FindChild("loot_close", true, false)!).EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        Require(!lootLayer.Visible, "loot: Close runs the native close");

        System.IO.File.WriteAllText(System.IO.Path.Combine(output, prefix + "-world-overlays.json"),
            JsonSerializer.Serialize(new { checks, failures, screenshots = screens, liveTransactions = false }, new JsonSerializerOptions { WriteIndented = true }));
        foreach (var layer in GetChildren().OfType<CanvasLayer>().ToArray()) layer.Free();
        menu.Free();
        world.Free(); net.Free();
        await Frames(2);
        if (failures.Count > 0) throw new Exception("WORLD_OVERLAYS_AUDIT failed: " + string.Join("; ", failures));
        GD.Print("WORLD_OVERLAYS_AUDIT_OK " + checks.Count);
    }
}
