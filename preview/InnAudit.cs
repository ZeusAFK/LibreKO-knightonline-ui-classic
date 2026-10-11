using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using KnightOnlineUiClassic;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Windows;
using System.Collections;
using System.Reflection;
using System.Text.Json;

public partial class Preview
{
    /// <summary>
    /// The Inn Hostess's opening window: the client's own reply (OnWarehouseOpen) shown on the original co_inn_us
    /// art, its states under real pointer input, its bounds, and every choice reaching the client's service.
    /// </summary>
    private async Task CaptureInnAudit(int nation)
    {
        string output = OS.GetEnvironment("LIBREKO_AUDIT_OUTPUT_DIR");
        if (output.Length == 0) output = ProjectSettings.GlobalizePath("res://../../research/inn-audit");
        System.IO.Directory.CreateDirectory(output);
        foreach (string pack in new[] { "build/client/LibreKO.pck", "build/client/source-content/knightonline.pck", "build/client/source-content/content/npcs.pck", "build/client/source-content/content/weapons.pck" })
            if (!ProjectSettings.LoadResourcePack(ProjectSettings.GlobalizePath("res://../../" + pack), false)) throw new Exception("Missing audit pack " + pack);
        GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Disabled; GetWindow().Size = new Vector2I(1000, 700);
        AddChild(new ColorRect { Color = new Color("252822"), Size = new Vector2(1000, 700), MouseFilter = MouseFilterEnum.Ignore });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var net = new Net(); typeof(Net).GetProperty("I")!.SetValue(null, net);
        typeof(Net).GetProperty("LastEnter")!.SetValue(net, new MyInfo { Nation = nation, Class = nation == 1 ? 105 : 205, Race = nation == 1 ? 1 : 11,
            Name = "Inn Preview", Gear = new int[8], Inventory = new LibreKO.Domain.ItemSlot[InventoryConstants.InventoryTotal] });
        net.Sheet.SeedWealth(12_000_000, 0); net.Sheet.SeedProgress(60, 0, 1);
        ItemData.EnsureLoaded();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var world = new World();
        ((Inventory)typeof(World).GetProperty("Inv", flags)!.GetValue(world)!).EnsureLength(InventoryConstants.InventoryTotal);
        DetailCall(world, "BuildInventoryPanel");
        var orphan = (Control)DetailField(world, "_invContent")!; orphan.Visible = false; world.AddChild(orphan);
        DetailCall(world, "BuildItemTooltip");
        DetailCall(world, "BuildNpcDialog");
        DetailCall(world, "WarehouseInit"); DetailCall(world, "BuildVipWarehousePanel"); DetailCall(world, "BuildSealWindow");
        var sealLayer = new CanvasLayer(); world.AddChild(sealLayer); typeof(World).GetField("_csLayer", flags)!.SetValue(world, sealLayer); DetailCall(world, "BuildCharacterSealWindow");
        DetailCall(world, "ClanCreateInit");
        var npcLayer = (Node)DetailField(world, "_npcLayer")!; npcLayer.Reparent(this);
        // Clan naming focuses its field, which needs the scene tree; the window itself stays out of the captures.
        var clanLayer = (Node)DetailField(world, "_clanCreateLayer")!; clanLayer.Reparent(this);
        var window = (HudWindow)DetailField(world, "_npcPanel")!;

        var actorType = typeof(World).GetNestedType("Ent", flags)!;
        var actor = Activator.CreateInstance(actorType, nonPublic: true)!;
        foreach (var (field, value) in new (string, object)[] { ("NpcId", 12000), ("ModelId", 12000), ("Level", 50), ("NpcType", 31), ("Name", "[Inn Hostess] Lined"), ("IsNpc", true), ("Gear", new int[8]) })
            actorType.GetField(field)!.SetValue(actor, value);
        ((IDictionary)DetailField(world, "_ents")!).Add(701, actor);
        typeof(World).GetField("_vendorNpcId", flags)!.SetValue(world, 701); typeof(World).GetField("_npcTalkId", flags)!.SetValue(world, -1);
        typeof(World).GetField("_vendorNpcName", flags)!.SetValue(world, "[Inn Hostess] Lined");
        var portrait = new NpcPortraitProbe(world); _windowData!.NpcPortraitSource = () => portrait.NpcPortrait;

        var dialogue = (ClassicNpcDialogue)CharacterDetailsSkin.Apply(window.Body)!;
        var inn = window.GetChildren().OfType<ClassicInnChoice>().Single();
        string prefix = nation == 1 ? "karus" : "human";
        var checks = new List<string>(); var screens = new List<object>();
        void Require(bool valid, string message) { if (!valid) throw new Exception("INN_AUDIT: " + message); checks.Add(message); }
        async Task Frames(int count = 8) { for (int i = 0; i < count; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
        void Pointer(Vector2 point, bool? press = null)
        {
            if (press == null) Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            else Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = press.Value, Position = point, GlobalPosition = point });
        }
        Image Shot() => GetViewport().GetTexture().GetImage();
        int Count(Image image, Rect2 rect, Color ink, float tolerance = .1f)
        {
            int found = 0;
            for (int y = (int)rect.Position.Y; y < (int)rect.End.Y; y++)
                for (int x = (int)rect.Position.X; x < (int)rect.End.X; x++)
                {
                    var c = image.GetPixel(x, y);
                    if (Mathf.Abs(c.R - ink.R) <= tolerance && Mathf.Abs(c.G - ink.G) <= tolerance && Mathf.Abs(c.B - ink.B) <= tolerance) found++;
                }
            return found;
        }
        void Save(Image image, Rect2 rect, string state)
        {
            using var crop = image.GetRegion(new Rect2I((int)rect.Position.X - 10, (int)rect.Position.Y - 10, (int)rect.Size.X + 20, (int)rect.Size.Y + 20));
            string file = prefix + "-" + state + ".png"; crop.SavePng(System.IO.Path.Combine(output, file));
            screens.Add(new { state, file, window = window.GetGlobalRect().ToString(),
                rows = inn.Visible ? inn.Rows.Select(r => new { caption = r.Text, rect = r.GetGlobalRect().ToString(), mode = r.GetDrawMode().ToString() }).ToArray() : null });
        }
        async Task Open()
        {
            DetailCall(world, "CloseNpcDialog");
            DetailCall(world, "OnWarehouseNpc"); await Frames();
        }

        // The previous presentation: the shared Classic NPC dialogue with the client's inn choices.
        inn.ProcessMode = ProcessModeEnum.Disabled;
        await Open();
        for (int i = 0; i < 100 && dialogue.Portrait.Visible && !dialogue.Portrait.IsReady; i++) await Frames(1);
        await Frames(4);
        using (var before = Shot()) Save(before, window.GetGlobalRect(), "inn-shared-dialogue");
        Require(dialogue.Visible && !inn.Visible, "Without the inn view the client's inn choices still show in the shared Classic NPC dialogue");
        inn.ProcessMode = ProcessModeEnum.Inherit;

        await Open();
        var native = Descendants((Node)DetailField(world, "_npcMenuBox")!).OfType<Button>().ToArray();
        var art = Plugin.Kit.Layout(ClassicInnLayout.Art);
        Require(inn.Active && inn.Visible && !dialogue.Visible, "The Inn Hostess's reply shows the co_inn_us window instead of the shared NPC dialogue");
        Require(inn.Rows.Count == native.Length && native.Length >= 2, $"One original text row per client choice ({native.Length})");
        Require(inn.Rows.Select(r => r.Text).SequenceEqual(native.Select(b => ClassicInnLayout.Caption(b.Text))), "Rows keep the client's captions and order: " + string.Join(" | ", native.Select(b => b.Text)));
        Require(native.Last().Text == "Close" && inn.Rows[^1].Text == ClassicInnLayout.LeaveCaption, "The client's Close choice is the last row and reads as the original leave row");
        var frame = window.GetGlobalRect(); var viewport = new Rect2(Vector2.Zero, GetViewportRect().Size);
        Require(frame.Size == new Vector2(ClassicInnLayout.Width, ClassicInnLayout.Height(native.Length)) && inn.Size == frame.Size,
            $"Original width and height grown by whole rows only: {frame.Size} for {native.Length} rows");
        Require(ClassicInnLayout.Height(ClassicInnLayout.OriginalRows) == art.H && ClassicInnLayout.Width == art.W, "Three rows give the original 327 × 240 window");
        Require(viewport.Encloses(frame) && Mathf.Abs(frame.GetCenter().X - viewport.GetCenter().X) <= 1 && Mathf.Abs(frame.GetCenter().Y - viewport.GetCenter().Y) <= 1, "Window opens centred inside the viewport: " + frame);
        Require(inn.Greeting.GetRect() == ClassicInnLayout.Greeting && inn.Greeting.Text == ((RichTextLabel)Descendants(window.Body).OfType<RichTextLabel>().First()).GetParsedText().Trim(),
            "The client's greeting sits in the original Text_Title rectangle");
        Require(inn.Greeting.GetLineCount() <= inn.Greeting.GetVisibleLineCount() && inn.Greeting.GetLineCount() <= 3, $"Greeting fits its rectangle ({inn.Greeting.GetLineCount()} lines)");
        Require(inn.Greeting.GetThemeFontSize("font_size") == UiKit.FontSize(art.Find("Text_Title")!) && inn.Greeting.GetThemeColor("font_color") == art.Find("Text_Title")!.Color, "Greeting uses the original white bold Arial");
        var ink = art.Find("Text_Trade")!;
        for (int i = 0; i < inn.Rows.Count; i++)
        {
            var row = inn.Rows[i];
            Require(row.GetRect() == ClassicInnLayout.Choice(i), $"Row {i} actual bounds {row.GetRect()} match the declared row");
            Require(frame.Encloses(row.GetGlobalRect()), $"Row {i} inside the frame");
            Require(i == 0 || row.Position.Y - inn.Rows[i - 1].GetRect().End.Y >= 10, $"Row {i} keeps the original gap to the row above");
            float width = row.GetThemeFont("font").GetStringSize(row.Text, HorizontalAlignment.Left, -1, row.GetThemeFontSize("font_size")).X;
            Require(width <= row.Size.X - ClassicInnLayout.CaptionInset * 2 && row.TooltipText.Length == 0, $"Caption '{row.Text}' is not cut ({width:0} px)");
            Require(row.GetThemeFontSize("font_size") == UiKit.FontSize(ink) && row.GetThemeColor("font_color") == ink.Color, $"Row {i} uses the original yellow Arial");
        }
        Require(frame.End.Y - inn.Rows[^1].GetGlobalRect().End.Y == ClassicInnLayout.BottomMargin, "Original space under the last row");
        Require(inn.Rows[0].GetGlobalRect().Position.X - frame.Position.X == frame.End.X - inn.Rows[0].GetGlobalRect().End.X, "Rows have equal side margins");
        foreach (var piece in art.Children.Where(n => n.IsImage && n.Texture != null))
        {
            var drawn = ClassicInnLayout.Piece(piece, (int)frame.Size.Y);
            Require(new Rect2(Vector2.Zero, frame.Size).Encloses(drawn), "Art piece inside the window " + drawn);
        }
        Require(art.Children.Where(n => n.IsImage && n.Texture != null).Select(n => ClassicInnLayout.Piece(n, (int)frame.Size.Y)).Sum(r => r.Area) == frame.Size.X * frame.Size.Y,
            "The art pieces cover the whole window without gaps or overlaps");
        Require(NpcPortraitCache.ActiveRenderers == 0, "No NPC portrait is rendered for the inn window");

        using (var normal = Shot())
        {
            Save(normal, frame, "inn-open");
            Require(Count(normal, new Rect2(frame.Position.X, frame.GetCenter().Y, 3, 1), new Color("252822"), .02f) == 0, "Frame art drawn at the left edge");
            Require(Count(normal, inn.Rows[0].GetGlobalRect(), ink.Color) >= 20, "Normal row shows the original yellow caption");
        }
        var first = inn.Rows[0]; var point = first.GetGlobalRect().Position + new Vector2(30, 10);
        Pointer(point); await Frames(3);
        using (var hover = Shot())
        {
            Save(hover, frame, "inn-hover");
            Require(first.GetDrawMode() == BaseButton.DrawMode.Hover && Count(hover, first.GetGlobalRect(), Colors.White) >= 20 && Count(hover, first.GetGlobalRect(), ink.Color) == 0, "Hover turns the caption white");
        }
        int[] fired = new int[native.Length];
        for (int i = 0; i < native.Length; i++) { int index = i; native[i].Pressed += () => fired[index]++; }
        Pointer(point, true); await Frames(3);
        using (var pressed = Shot())
        {
            Save(pressed, frame, "inn-pressed");
            Require(first.GetDrawMode() == BaseButton.DrawMode.Pressed && Count(pressed, first.GetGlobalRect(), ClassicInnLayout.Pressed) >= 20, "Pressed caption turns frame gold and drops one pixel");
            Require(fired.Sum() == 0, "Nothing fires before the button is released");
        }
        Pointer(point, false); await Frames(3);
        Require(fired[0] == 1 && fired.Sum() == 1, "Releasing the first row presses the client's own first choice once");
        Require((bool)DetailField(world, "_whShown")! && !window.Visible, "Use storage opens the client's warehouse and closes the inn window");
        DetailCall(world, "CloseWarehouse");
        Pointer(Vector2.Zero); await Frames(2);

        // Each remaining choice through real pointer input, checked on the client's own state.
        var expected = new Dictionary<string, (Action Before, Func<bool> Opened, Action After)>
        {
            ["Use [VIP] storage"] = (() => typeof(World).GetField("_vipWhInFlight", flags)!.SetValue(world, true), () => !(bool)DetailField(world, "_vipWhInFlight")!, () => { }),
            ["Create a Clan"] = (() => { }, () => (bool)DetailField(world, "_clanCreateShown")!, () => DetailCall(world, "CloseClanCreate")),
            ["Seal / Cancel (anti-theft)"] = (() => { }, () => (bool)DetailField(world, "_sealShown")! && DetailField(world, "_sealMode")!.ToString() == "Secret", () => DetailCall(world, "CloseSealWindow")),
            ["Seal / Cancel"] = (() => { }, () => (bool)DetailField(world, "_sealShown")! && DetailField(world, "_sealMode")!.ToString() == "Bind", () => DetailCall(world, "CloseSealWindow")),
            ["Character Seal/Unseal"] = (() => { }, () => ((HudWindow)DetailField(world, "_csWindow")!).Visible, () => DetailCall(world, "CloseCharacterSeal")),
            ["Close"] = (() => { }, () => !(bool)DetailField(world, "_npcDialogShown")!, () => { }),
        };
        var verified = new List<string> { native[0].Text };
        for (int i = 1; i < native.Length; i++)
        {
            await Open();
            native = Descendants((Node)DetailField(world, "_npcMenuBox")!).OfType<Button>().ToArray();
            int calls = 0; native[i].Pressed += () => calls++;
            string caption = native[i].Text;
            expected.TryGetValue(caption, out var probe);
            probe.Before?.Invoke();
            var at = inn.Rows[i].GetGlobalRect().GetCenter();
            Pointer(at); await Frames(2); Pointer(at, true); await Frames(2); Pointer(at, false); await Frames(3);
            Require(calls == 1 && !window.Visible, $"Row '{caption}' presses the client's own choice once and the inn window closes");
            if (probe.Opened != null) { Require(probe.Opened(), $"Row '{caption}' reaches the client's service"); verified.Add(caption); }
            probe.After?.Invoke();
            Pointer(Vector2.Zero); await Frames(1);
        }

        // A choice the client disables is shown greyed out and cannot be pressed.
        await Open();
        native = Descendants((Node)DetailField(world, "_npcMenuBox")!).OfType<Button>().ToArray();
        int clan = Array.FindIndex(native, b => b.Text == "Create a Clan"); if (clan < 0) clan = 1;
        native[clan].Disabled = true; int disabledCalls = 0; native[clan].Pressed += () => disabledCalls++;
        await Frames(2);
        var blocked = inn.Rows[clan]; var blockedAt = blocked.GetGlobalRect().GetCenter();
        Pointer(blockedAt); await Frames(3);
        using (var disabled = Shot())
        {
            Save(disabled, window.GetGlobalRect(), "inn-disabled");
            Require(blocked.Disabled && blocked.GetDrawMode() == BaseButton.DrawMode.Disabled && Count(disabled, blocked.GetGlobalRect(), ClassicInnLayout.Disabled, .08f) >= 20
                && Count(disabled, blocked.GetGlobalRect(), ink.Color) == 0, "A disabled client choice is grey, also under the pointer");
        }
        Pointer(blockedAt, true); await Frames(2); Pointer(blockedAt, false); await Frames(2);
        Require(disabledCalls == 0 && window.Visible && !(bool)DetailField(world, "_clanCreateShown")!, "A disabled row does not press the client's choice");
        Pointer(Vector2.Zero); await Frames(2);

        // Another NPC's menu after the inn returns to the shared Classic NPC dialogue.
        DetailCall(world, "CloseNpcDialog");
        DetailCall(world, "BeginNpcDialog", "[Inn Hostess] Lined", "How can I help you? Select a service below.");
        DetailCall(world, "AddNpcMenuButton", "1.   Tell me more...", (Action)(() => { }));
        DetailCall(world, "AddNpcMenuButton", "2.   Close", (Action)(() => DetailCall(world, "CloseNpcDialog")));
        DetailCall(world, "EndNpcDialog", 2); await Frames(10);
        Require(!inn.Visible && dialogue.Visible && window.GetGlobalRect().Size.X == ClassicNpcLayout.Width, "A quest or talk menu after the inn shows the shared NPC dialogue again");
        // The running game's path: the hostess's reply raises the client's network event, the client (subscribed in
        // WarehouseInit) builds its menu and the plugin's handler marks it once that menu exists.
        NativeInn.Prepare(window, world);
        ((Action)typeof(Net).GetField("WarehouseNpcEvent", flags)!.GetValue(net)!)();
        await Frames(4);
        Require(inn.Visible && inn.Active && !dialogue.Visible && window.GetGlobalRect().Size == frame.Size && inn.Rows.Count == native.Length,
            "The client's network event after another menu opens the inn window again");
        using (var network = Shot()) Save(network, window.GetGlobalRect(), "inn-network-reopen");
        DetailCall(world, "CloseNpcDialog"); DetailCall(world, "WarehouseDispose");

        System.IO.File.WriteAllText(System.IO.Path.Combine(output, prefix + "-inn.json"), JsonSerializer.Serialize(new
        {
            nation = prefix, clientHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(ProjectSettings.GlobalizePath("res://../../LibreKO/Client/.godot/mono/temp/bin/ExportRelease/LibreKO.dll")))),
            choices = inn.Rows.Select(r => r.Text).ToArray(), servicesVerified = verified, window = frame.ToString(), checks, screens
        }, new JsonSerializerOptions { WriteIndented = true }));
        _windowData.NpcPortraitSource = null; NpcPortraitCache.Clear();
        npcLayer.Free(); clanLayer.Free(); world.Free(); net.Free();
        GD.Print($"INN_AUDIT_OK {prefix} {checks.Count} checks");
    }
}
