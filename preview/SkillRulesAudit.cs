using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Plugins;
using KnightOnlineUiClassic;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Native;
using KnightOnlineUiClassic.Windows;
using System.Reflection;
using System.Text.Json;

/// <summary>Forwards the plugin's skill-bar calls to the client's bridge and records the ones that change or fire the bar.</summary>
public class RecordingHotbar : DispatchProxy
{
    public object Inner = null!;
    public readonly List<int> Activated = new();
    public readonly List<(int Abs, int Id, int From)> Drops = new();

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "ActivateAbs") Activated.Add((int)args![0]!);
        if (method.Name == "Drop") Drops.Add(((int)args![0]!, (int)args[1]!, (int)args[2]!));
        return method.Invoke(Inner, args);
    }
}

/// <summary>Genie skill cells for the skill rules audit; records the cells the plugin changes.</summary>
public class StubGenie : DispatchProxy
{
    public bool Running;
    public readonly Dictionary<(GenieCellKind, int), int> Cells = new();
    public readonly List<(GenieCellKind, int, int)> Sets = new();

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        switch (method!.Name)
        {
            case "get_Running": return Running;
            case "Cell": return new GenieCellView(Cells.GetValueOrDefault(((GenieCellKind)args![0]!, (int)args[1]!)), null, "");
            case "SetCell":
                var key = ((GenieCellKind)args![0]!, (int)args[1]!);
                Sets.Add((key.Item1, key.Item2, (int)args[2]!));
                Cells[key] = (int)args[2]!;
                return true;
        }
        return method.ReturnType != typeof(void) && method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}

public partial class Preview
{
    private const BindingFlags AnyMember = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>
    /// Every way a skill or item reaches the skill bar, run against the client's own World, skill data and plugin bridge:
    /// the Classic skill window (right-click, mouse drag), drops from other windows, the Chaos dungeon, refused edits,
    /// passive skills saved on old bars, and the slot states (usable, cooldown, short count, icon-less skill, ready flash).
    /// </summary>
    private async Task CaptureSkillRulesAudit(PluginGame game, int nation)
    {
        string output = OS.GetEnvironment("LIBREKO_AUDIT_OUTPUT_DIR") is { Length: > 0 } directory ? directory
            : ProjectSettings.GlobalizePath("res://../../research/skill-rules-audit");
        System.IO.Directory.CreateDirectory(output);
        foreach (string pack in new[] { "build/client/LibreKO.pck", "build/client/source-content/knightonline.pck" })
            if (!ProjectSettings.LoadResourcePack(ProjectSettings.GlobalizePath("res://../../" + pack), false)) throw new Exception("Missing skill rules pack: " + pack);
        GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
        GetWindow().Size = new Vector2I(560, 650);
        AddChild(new ColorRect { Color = new Color("252822"), Size = new Vector2(560, 650), MouseFilter = MouseFilterEnum.Ignore });
        string prefix = nation == 1 ? "karus" : "human";
        var checks = new List<string>();
        var captures = new List<object>();
        void Require(bool valid, string text) { if (!valid) throw new Exception("SKILL_RULES_AUDIT: " + text); checks.Add(text); }

        var net = new Net(); typeof(Net).GetProperty("I")!.SetValue(null, net);
        var login = new LoginNet(); typeof(LoginNet).GetProperty("I")!.SetValue(null, login);
        net.Sheet.SeedProgress(83, 0, 100);
        net.Vitals.Seed(3000, 3000, 2000, 2000);
        typeof(Net).GetProperty("LastEnter")!.SetValue(net, new MyInfo { Nation = nation, Class = nation * 100 + 6, Race = nation == 1 ? 1 : 11,
            Name = "Skill Rules", Gear = new int[8], Inventory = new LibreKO.Domain.ItemSlot[InventoryConstants.InventoryTotal] });
        SkillData.EnsureLoaded(); ItemData.EnsureLoaded();
        byte[] seed = { 4, 0, 0, 0, 0, 60, 20, 23, 10 };
        net.Mastery.Seed(seed);
        var world = new World();
        FieldInfo Field(string name) => typeof(World).GetField(name, AnyMember)!;
        Field("_selfClass").SetValue(world, nation * 100 + 6);
        ((Inventory)typeof(World).GetProperty("Inv", AnyMember)!.GetValue(world)!).EnsureLength(InventoryConstants.InventoryTotal);
        var bar = (int[])Field("_hotbar").GetValue(world)!;
        var bridge = Activator.CreateInstance(typeof(World).GetNestedType("PluginGameBridge", BindingFlags.NonPublic)!, AnyMember, null, new object[] { world }, null)!;
        var attach = typeof(PluginGame).GetMethod("Attach", AnyMember)!;
        attach.Invoke(game, Enumerable.Repeat(bridge, attach.GetParameters().Length).ToArray());
        var genieBridge = Activator.CreateInstance(typeof(World).GetNestedType("PluginGenieBridge", BindingFlags.NonPublic)!, AnyMember, null, new object[] { world }, null)!;
        typeof(PluginGame).GetMethod("AttachGenie", AnyMember)!.Invoke(game, new[] { genieBridge });
        var recorder = DispatchProxy.Create<IGameHotbar, RecordingHotbar>();
        ((RecordingHotbar)(object)recorder).Inner = bridge;
        var hotbarProxy = typeof(PluginGame).GetField("_hotbar", AnyMember)!.GetValue(game)!;
        hotbarProxy.GetType().GetField("Source")!.SetValue(hotbarProxy, recorder);
        var record = (RecordingHotbar)(object)recorder;
        NativeGame.Source = new ClientGame();
        typeof(NativeHudChat).GetField("_world", AnyMember)!.SetValue(null, new WeakReference<World>(world));
        string localBar = (string)typeof(World).GetMethod("LocalHotbarPath", AnyMember)!.Invoke(world, null)!;
        void Raise() => typeof(PluginGame).GetMethod("RaiseHotbar", AnyMember)!.Invoke(game, null);
        void SetZone(int zone) => Field("_zone").SetValue(world, zone);
        void SetLoaded(bool loaded) => Field("_hotLoadedFromServer").SetValue(world, loaded);
        void SetGenie(bool running) => typeof(Net).GetProperty("GenieRunning")!.SetValue(net, running);
        void SetLocked(bool locked) => typeof(Config).GetProperty("HotbarLocked", AnyMember)!.SetValue(null, locked);
        void ClearBar() { Array.Clear(bar); Raise(); }

        var shell = new Control(); AddChild(shell);
        var host = (WindowHost)Create(typeof(WindowHost), "skills", "Skills", shell, (Action)(() => { }));
        var window = new SkillWindow(host) { Position = new Vector2(16, 30) }; shell.AddChild(window);
        var hotbar = new HotkeyBar(); AddChild(hotbar);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        hotbar.Position = new Vector2(420, 40);
        Control Bar() => hotbar.GetNode<Control>("canvas/main_bar");
        HotSlot Slot(int index) => Bar().GetNode<HotSlot>($"slot_{index}");
        void Invoke(string method, params object[] args) => typeof(SkillWindow).GetMethod(method, AnyMember)!.Invoke(window, args);
        List<SkillCell> Cells() => (List<SkillCell>)typeof(SkillWindow).GetField("_cells", AnyMember)!.GetValue(window)!;
        void SetClass(int local) { Field("_selfClass").SetValue(world, nation * 100 + local); Invoke("Rebuild"); }
        SkillCell Show(int skillId)
        {
            foreach (var tab in game.Skills.Tabs)
            {
                int index = game.Skills.Skills(tab.Category).ToList().FindIndex(s => s.Id == skillId);
                if (index < 0) continue;
                Invoke("SelectTab", tab.Category);
                typeof(SkillWindow).GetField("_page", AnyMember)!.SetValue(window, index / 6);
                Invoke("RefreshPage");
                return Cells().First(c => c.SkillId == skillId);
            }
            throw new Exception("SKILL_RULES_AUDIT: skill " + skillId + " is not listed for class " + game.Character.Class);
        }
        void RightClick(Control control) => control._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right });
        Godot.Collections.Dictionary Payload(params (string Key, int Value)[] entries)
        {
            var data = new Godot.Collections.Dictionary();
            foreach (var (key, value) in entries) data[key] = value;
            return data;
        }
        async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        // A real mouse drag through the viewport: press on the source, move in steps, release over the target.
        async Task<bool> MouseDrag(Control from, Control to)
        {
            Vector2 a = from.GetGlobalRect().GetCenter(), b = to.GetGlobalRect().GetCenter();
            Input.ParseInputEvent(new InputEventMouseMotion { Position = a, GlobalPosition = a });
            await Frames(2);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = a, GlobalPosition = a, ButtonMask = MouseButtonMask.Left });
            await Frames(2);
            bool dragging = false;
            for (int step = 1; step <= 8; step++)
            {
                var at = a.Lerp(b, step / 8f);
                Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at, Relative = (b - a) / 8f, ButtonMask = MouseButtonMask.Left });
                await Frames(1);
                dragging |= GetViewport().GuiIsDragging();
            }
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = b, GlobalPosition = b });
            await Frames(3);
            return dragging;
        }
        async Task Capture(string state, Rect2I region, bool now = false)
        {
            Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(550, 640), GlobalPosition = new Vector2(550, 640) });
            // Short-lived states (a running cooldown, the ready flash) are taken on the next drawn frame.
            if (now) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            else await Settled();
            string name = prefix + "-" + state;
            using var image = GetViewport().GetTexture().GetImage();
            using var crop = image.GetRegion(region);
            crop.SavePng(output + "/" + name + ".png");
            captures.Add(new { state, file = name + ".png" });
        }
        Rect2I WindowRegion() => new((int)window.GlobalPosition.X, (int)window.GlobalPosition.Y, 365, 574);
        Rect2I BarRegion() => new Rect2I((int)hotbar.GlobalPosition.X - 4, (int)hotbar.GlobalPosition.Y - 4, (int)hotbar.Size.X + 8, (int)hotbar.Size.Y + 8);

        try
        {
            Raise();
            Require(Bar().GetNode<Label>("page_label").Text == "1" && (int)Field("_hotPage").GetValue(world)! == 0, "The skill bar opens on the client's page 1");
            Require(record.Drops.Count == 0, "Opening the bar changes no slot");

            // 1. The owner's report: the Kurian/Porutu Master 2 (level 62) skill is passive and never goes on the bar.
            SetClass(15);
            int awakeningId = nation * 100000 + 15802;
            var awakening = Show(awakeningId);
            Require(awakening.State == SkillLearnState.Learned, "Awakening is learned at Master 10");
            RightClick(awakening); Raise();
            Require(!bar.Contains(awakeningId), "Right-click does not put the passive Awakening on the bar");
            Require(awakening._GetDragData(Vector2.Zero).VariantType == Variant.Type.Nil, "Awakening starts no drag");
            Require(!await MouseDrag(awakening, Slot(0)) && bar[0] == 0, "A mouse drag of Awakening onto the bar does nothing");
            Require(!Slot(0)._CanDropData(Vector2.Zero, Payload(("id", awakeningId))), "The bar refuses a dropped passive skill");
            int passives = 0;
            foreach (var tab in game.Skills.Tabs)
                foreach (var skill in game.Skills.Skills(tab.Category))
                    if (SkillData.Get(skill.Id) is { Type1: MagicType.None } passive)
                    {
                        passives++;
                        Require(!Slot(1)._CanDropData(Vector2.Zero, Payload(("id", passive.Id))), "The bar refuses the passive " + passive.Id);
                    }
            Require(passives >= 10, "The Kurian master has its passive skills listed (" + passives + ")");
            await Capture("awakening-refused", WindowRegion());

            // 2. A learned active skill: right-click adds it, a mouse drag drops it on the chosen slot.
            SetClass(6);
            int screamId = nation * 100000 + 6802;
            var scream = Show(screamId);
            RightClick(scream); Raise();
            Require(bar[0] == screamId, "Right-click puts the active Master 2 skill Scream in the first free slot");
            Require(await MouseDrag(scream, Slot(3)) && bar[3] == screamId, "A mouse drag puts Scream on the slot it is dropped on");
            Require(record.Drops.Contains((3, screamId, -1)), "The drop goes through the client's skill bar");
            // 3. A skill that is not learned yet (Exceed Break needs 15 Master points) is never placed.
            var exceed = Show(nation * 100000 + 6815);
            int filled = bar.Count(id => id != 0);
            RightClick(exceed); Raise();
            Require(bar.Count(id => id != 0) == filled && exceed._GetDragData(Vector2.Zero).VariantType == Variant.Type.Nil,
                "A Master skill that is not learned is neither added nor dragged");
            // 4. A Special-type skill is placeable.
            SetClass(12);
            var special = game.Skills.Tabs.SelectMany(t => game.Skills.Skills(t.Category)).First(s => SkillData.Get(s.Id)!.Type1 == MagicType.Special && s.Available);
            RightClick(Show(special.Id)); Raise();
            Require(bar.Contains(special.Id), "A Special-type skill (" + special.Id + ") goes on the bar");
            Require(Slot(0)._CanDropData(Vector2.Zero, Payload(("id", special.Id))), "The bar accepts a dropped Special-type skill");

            // 5. The Chaos dungeon: only the Chaos skill tree goes on its temporary bar.
            SetClass(6);
            ClearBar();
            SetZone(ChaosBattle.Zone);
            scream = Show(screamId);
            RightClick(scream); Raise();
            Require(!bar.Contains(screamId) && !scream.CanDrag, "In the Chaos dungeon class skills stay off the bar");
            Require(!Slot(2)._CanDropData(Vector2.Zero, Payload(("id", screamId))), "The Chaos bar refuses a class skill");
            int chaosSkill = SkillData.All.Where(s => s.Tree == ChaosBattle.SkillTree).Min(s => s.Id);
            Require(Slot(2)._CanDropData(Vector2.Zero, Payload(("id", chaosSkill))), "The Chaos bar accepts a Chaos skill");
            Slot(2)._DropData(Vector2.Zero, Payload(("id", chaosSkill))); Raise();
            Require(bar[2] == chaosSkill, "A Chaos skill drops onto the Chaos bar");
            SetZone(0);
            ClearBar();

            // 6. Items: usable and wearable items from the bag; never shop or stall goods, never materials.
            var potion = ItemData.All().First(i => i.Effect1 != 0 && SkillData.Get(i.Effect1) is { IsPotion: true });
            var material = ItemData.All().First(i => i.Effect1 == 0 && ItemData.EquipSlotFor(i) < 0 && i.Id > 100_000_000);
            var weapon = ItemData.All().First(i => ItemData.EquipSlotFor(i) >= 0 && i.Id > 100_000_000);
            Require(Slot(1)._CanDropData(Vector2.Zero, Payload(("id", potion.Id), ("invFrom", 14))), "A potion from the bag is accepted");
            Require(!Slot(1)._CanDropData(Vector2.Zero, Payload(("id", weapon.Id), ("invFrom", 15))), "Equipment from the bag is refused: the original bar takes usable items only");
            Require(!Slot(1)._CanDropData(Vector2.Zero, Payload(("id", material.Id), ("invFrom", 16))), "A material the bar cannot use is refused");
            Require(!Slot(1)._CanDropData(Vector2.Zero, Payload(("id", potion.Id), ("companionFrom", 0))), "An NPC shop item is refused");
            Require(!Slot(1)._CanDropData(Vector2.Zero, Payload(("id", potion.Id), ("shopFrom", 0))), "Another player's stall item is refused");

            // 7. Refused edits: the Genie holds the bar; a locked bar keeps its order but still takes new entries.
            bar[0] = screamId; Raise();
            SetGenie(true);
            Require(Slot(0)._GetDragData(Vector2.Zero).VariantType == Variant.Type.Nil, "While the Genie hunts, no slot drag starts");
            SetGenie(false);
            SetLocked(true);
            Require(!Slot(1)._CanDropData(Vector2.Zero, Payload(("id", screamId), ("barAbs", 0))), "A locked bar refuses slot moves");
            Require(Slot(1)._CanDropData(Vector2.Zero, Payload(("id", screamId))), "A locked bar still takes a skill from the window");
            Require(Slot(0)._GetDragData(Vector2.Zero).VariantType == Variant.Type.Nil, "A locked bar starts no slot drag");
            SetLocked(false);
            ClearBar();

            // 8. A passive saved on an old bar: never cast, and cleared once the saved bar is applied.
            SetLoaded(false);
            bar[7] = awakeningId; Raise();
            Require(bar[7] == awakeningId, "Nothing is cleared before the client applies the saved bar");
            var saved = Slot(7);
            saved._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left });
            saved._GuiInput(new InputEventMouseButton { Pressed = false, ButtonIndex = MouseButton.Left });
            Require(!record.Activated.Contains(7), "Clicking a saved passive does not cast it");
            SetGenie(true); SetLoaded(true); Raise();
            Require(bar[7] == awakeningId, "Nothing is cleared while the Genie holds the bar");
            SetGenie(false); Raise();
            Require(bar[7] == 0 && record.Drops.Contains((7, 0, -1)), "A saved passive is cleared through the client's own drop");
            bar[8] = screamId; Raise();
            saved = Slot(8);
            saved._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left });
            saved._GuiInput(new InputEventMouseButton { Pressed = false, ButtonIndex = MouseButton.Left });
            Require(record.Activated.Contains(8), "Clicking an active skill fires it");
            ClearBar();

            // 9. Slot states: usable, MP short, cooldown, short count, icon-less skill, selection, ready flash.
            net.Vitals.Seed(3000, 3000, 100, 2000);
            var usable = game.Skills.Skills(SkillPage.Basic).Select(s => SkillData.Get(s.Id)!)
                .First(s => s.Type1 != MagicType.None && s.NeedWeapon == SkillUsable.NeedsNoWeapon && s.Msp <= 100 && s.Sp == 0 && s.UseItem == 0 && s.NeedBuffSkill == 0 && s.Recast > 0);
            var costly = game.Skills.Skills(MasteryPoints.FirstTree).Select(s => SkillData.Get(s.Id)!).First(s => s.Msp > 100 && s.Recast > 0);
            var nameless = SkillData.All.First(s => s.Type1 != MagicType.None && SkillData.Icon(s.Id) == null && s.Id < SkillPage.UsableItemFirstId);
            bar[0] = usable.Id; bar[1] = costly.Id; bar[2] = usable.Id; bar[3] = potion.Id; bar[4] = nameless.Id; bar[5] = weapon.Id;
            // About half of the recast is still left when the bar is captured (after the 0.3 s settle below).
            net.SkillCooldowns[usable.Id] = Time.GetTicksMsec() / 1000.0 + usable.RecastSeconds * 0.5 + 0.4;
            Raise();
            game.Hotbar.Select(5); Raise();
            await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
            var hotbarUsable = typeof(World).GetMethod("HotbarUsable", AnyMember)!;
            // The client dims only entries that cast a skill; equipment kept on an old bar is never dimmed.
            bool NativeUsable(int id) => (SkillData.Get(id) ?? SkillData.Get(ItemData.Get(id)?.Effect1 ?? 0)) is not { } cast
                || (bool)hotbarUsable.Invoke(world, new object[] { cast, Time.GetTicksMsec() / 1000.0 })!;
            bool Dim(int index) => Slot(index).GetNode<TextureRect>("content/icon").Modulate != Colors.White;
            Require(Slot(0).GetNode<ColorRect>("content/cooldown").Visible && !Dim(0), "A skill on cooldown shows the fill and is not dimmed");
            Require(Dim(1) && !NativeUsable(costly.Id), "A skill the MP cannot pay for is dimmed, as the client's own test says");
            var fill = Slot(0).GetNode<ColorRect>("content/cooldown");
            await Capture("hotbar-cooldown", BarRegion(), now: true);
            Require(fill.Visible && fill.Size.Y >= 8f, "The cooldown fill is still running in the cooldown capture (" + fill.GetRect() + ")");
            Require(net.SkillCooldowns.Remove(usable.Id), "Cooldown fixture removed");
            await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
            Require(!Dim(0) && NativeUsable(usable.Id), "A usable skill is shown at full colour");
            Require(Slot(3).GetNode<Label>("content/count").Text == "0" && Slot(3).GetNode<Label>("content/count").GetThemeColor("font_color") != Colors.White && Dim(3),
                "A potion that is out shows a red count and is dimmed");
            Require(Slot(3).TooltipText == "", "An item slot leaves its tooltip to the client's item card");
            Require(Slot(1).TooltipText.Length > 0, "A skill slot carries the client's skill tooltip");
            Require(Slot(4).GetNode<Label>("content/name").Text == nameless.Name, "A skill without an icon shows its name");
            Require(Bar().GetNode<HotSlot>("slot_5").GetNode<Panel>("selection").Visible, "The selected slot shows its frame");
            foreach (int index in new[] { 0, 1, 2, 3, 5 })
                Require(Dim(index) == (Slot(index).GetNode<ColorRect>("content/cooldown").Visible ? false : !NativeUsable(bar[index])),
                    "Slot " + index + " is dimmed exactly when the client's own test says it cannot be used");
            await Capture("hotbar-states", BarRegion());
            typeof(PluginGame).GetMethod("RaiseHotbarReady", AnyMember)!.Invoke(game, new object[] { new[] { usable.Id } });
            await Frames(2);
            var flash = Slot(0).GetNode<Control>("content/ready").GetChildren().OfType<TextureRect>().Where(t => t.Visible && t.Texture != null).ToList();
            Require(flash.Count == 1 && Slot(1).GetNode<Control>("content/ready").GetChildren().OfType<TextureRect>().All(t => !t.Visible),
                "The client's ready flash plays on the slot whose skill is ready");
            await Capture("hotbar-ready-flash", BarRegion(), now: true);
            net.Vitals.Seed(3000, 3000, 2000, 2000);

            // 10. The taskbar's Skill button runs the client's skill key (the Chaos skill page in the Chaos dungeon).
            bool skillKey = false;
            ((Dictionary<KeyAction, Action>)Field("_keyActions").GetValue(world)!)[KeyAction.Skills] = () => skillKey = true;
            var taskbar = new Taskbar(); AddChild(taskbar);
            var taskbarView = (LayoutView)typeof(Taskbar).GetField("_view", AnyMember)!.GetValue(taskbar)!;
            taskbarView.Get<BaseButton>("btn_skill")!.EmitSignal(BaseButton.SignalName.Pressed);
            Require(skillKey, "The taskbar Skill button runs the client's skill key");
            taskbar.QueueFree();

            // 11. The Genie's skill cells are castable shortcuts too: its skill picker offers no passive skill, and
            // passive skills saved in its cells are cleared (never while the Genie runs).
            var picker = new PopupMenu { Name = "genie_skill_picker_probe" };
            AddChild(picker);
            picker.AddItem("Clear Slot", 0);
            picker.AddItem("Awakening", awakeningId);
            picker.AddItem("Scream", screamId);
            NativeSkills.FilterGenieSkillPicker(picker, shell);
            NativeSkills.FilterGenieSkillPicker(picker, shell);
            picker.Popup();
            await Frames(2);
            var offered = Enumerable.Range(0, picker.ItemCount).Select(picker.GetItemId).ToArray();
            Require(offered.SequenceEqual(new[] { 0, screamId }), "The Genie skill picker drops the passive Awakening and keeps Scream: " + string.Join(",", offered));
            picker.Hide(); picker.QueueFree();
            int supportPassive = SkillData.ForClass(nation * 100 + 6).First(s => s.Type1 == MagicType.None && s.Moral == SkillTarget.Self).Id;
            var genie = DispatchProxy.Create<IGameGenie, StubGenie>();
            var cells = (StubGenie)(object)genie;
            cells.Cells[(GenieCellKind.Attack, 0)] = screamId;
            cells.Cells[(GenieCellKind.Attack, 1)] = awakeningId;
            cells.Cells[(GenieCellKind.Support, 3)] = supportPassive;
            cells.Cells[(GenieCellKind.Recovery, 0)] = potion.Id;
            cells.Running = true;
            NativeSkills.ClearRefusedGenieSkills(genie);
            Require(cells.Sets.Count == 0, "Nothing is cleared from the Genie cells while the Genie runs");
            cells.Running = false;
            NativeSkills.ClearRefusedGenieSkills(genie);
            Require(cells.Sets.SequenceEqual(new[] { (GenieCellKind.Attack, 1, 0), (GenieCellKind.Support, 3, 0) }),
                "Saved passive skills are cleared from the Genie cells; Scream and the potion stay");
        }
        finally
        {
            SetGenie(false); SetLocked(false);
            string file = ProjectSettings.GlobalizePath(localBar);
            if (System.IO.File.Exists(file)) System.IO.File.Delete(file);
        }
        System.IO.File.WriteAllText(output + "/" + prefix + "-skill-rules.json", JsonSerializer.Serialize(new { nation, captures, checks,
            data = "Real LibreKO skill and item data, the client's World skill bar and plugin bridge; controlled class, level, mastery points, MP and cooldown" },
            new JsonSerializerOptions { WriteIndented = true }));
        GD.Print("SKILL_RULES_AUDIT_OK " + prefix + " checks=" + checks.Count + " captures=" + captures.Count);
        hotbar.QueueFree(); shell.QueueFree();
        await Frames(2);
        world.Free(); net.Free(); login.Free();
    }
}
