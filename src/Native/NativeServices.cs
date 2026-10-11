using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Native;

public static partial class NativeSetup
{
    static partial void RegisterServices(PluginContext context)
    {
        foreach (string id in NativeServices.WindowIds)
            NativeWindows.Prepare(id, (window, world) => NativeServices.Prepare(window, world));
    }
}

/// <summary>
/// Prepares the NPC service windows (repair, warp, seal, Chaotic Generator, item combination, recipe
/// book, redistribution, disassembly and the Character Seal transfer) for the Classic service skin: it names the native controls the skin
/// arranges, marks the windows as Classic-capable, mirrors selection and pending state into metas,
/// and implements the Classic-only behaviour the skin expects, such as repair through the inventory,
/// the full carried inventory in the Chaotic Generator and dragging pieces onto its socket.
/// </summary>
public static class NativeServices
{
    public static readonly string[] WindowIds = { "repair", "warp", "seal", "piecechange", "itemcombine", "combinerecipes", "class_change", "itemsmash", "char_seal_trans" };

    private const string ControlsMeta = "classic_service_controls";
    private const string PreparedMeta = "native_service";
    private const string SelectedMeta = "service_selected";
    private const string RedistributionHeader = "Every stat or mastery point";
    private const int SelectedRowBorder = 3;

    public static void Prepare(HudWindow window, World world)
    {
        if (window.HasMeta(PreparedMeta)) return;
        window.SetMeta(PreparedMeta, true);
        window.SetMeta(ControlsMeta, 1);
        switch (window.Id)
        {
            case "repair": NativeRepair.Attach(window, world); break;
            case "warp": PrepareWarp(window, world); break;
            case "seal": PrepareSeal(window, world); break;
            case "piecechange": NativePieceChange.Attach(window, world); break;
            case "itemcombine": PrepareCombine(window, world); break;
            case "combinerecipes": PrepareRecipeBook(window); break;
            case "class_change": PrepareRedistribution(window, world); break;
            case "itemsmash": PrepareSmash(window, world); break;
            case "char_seal_trans": PrepareCharacterSealTrans(window, world); break;
        }
    }

    private static void Name(World world, string field, string name) => NativeWindows.Name(Native.Get<Node>(world, field), name);

    private static void PrepareWarp(HudWindow window, World world)
    {
        Name(world, "_warpScroll", "warp_list_scroll");
        Name(world, "_warpImage", "warp_image");
        Name(world, "_warpLevels", "warp_levels");
        Name(world, "_warpDesc", "warp_description");
        Name(world, "_warpGoldLbl", "warp_gold");
        Name(world, "_warpStatus", "warp_status");
        Name(world, "_warpTravel", "warp_travel");
        NativeWindows.Sync(window, () =>
        {
            if (Native.Get<List<PanelContainer>>(world, "_warpRows") is not { } rows) return;
            int selected = Native.Get<int>(world, "_warpSelected");
            for (int i = 0; i < rows.Count; i++)
                if (GodotObject.IsInstanceValid(rows[i])) rows[i].SetMeta(SelectedMeta, i == selected);
        });
    }

    private static void PrepareSeal(HudWindow window, World world)
    {
        Name(world, "_sealSocket", "seal_socket");
        Name(world, "_sealHeadline", "seal_headline");
        Name(world, "_sealPrompt", "seal_prompt");
        Name(world, "_sealCodeRow", "seal_code_row");
        Name(world, "_sealCodeField", "seal_code");
        Name(world, "_sealPad", "seal_keypad");
        Name(world, "_sealGold", "seal_gold");
        Name(world, "_sealConfirm", "seal_confirm");
        // The client docks the inventory window beside the seal; the Classic window keeps its own bag.
        NativeBagCells.Attach(window, world, "seal_bag_", "SealTakeFromBag", "SealBagFit");
        if (Native.Get<Control>(world, "_sealSocket") is { } socket)
            socket.AddChild(new ServiceDropTarget(data => SealDropSlot(world, data) >= 0,
                data => Native.Call(world, "SealTakeFromBag", SealDropSlot(world, data))) { Name = "seal_drop" });
    }

    /// <summary>The carried slot a drag onto the seal socket comes from, or -1.</summary>
    private static int SealDropSlot(World world, Variant data)
    {
        if (!Native.Get<bool>(world, "_sealShown") || Native.Get<bool>(world, "_sealWaiting") || data.VariantType != Variant.Type.Dictionary) return -1;
        var dict = data.AsGodotDictionary();
        if (!dict.ContainsKey("invFrom")) return -1;
        int abs = dict["invFrom"].AsInt32();
        return abs >= Inventory.GridStart && abs < Inventory.GridStart + Inventory.GridCount ? abs : -1;
    }

    /// <summary>
    /// Keeps a service window's embedded Classic bag in charge: when the client opens the inventory window
    /// only as the window's bag companion, it is closed again (a player-opened inventory stays open).
    /// </summary>
    public static void KeepInventoryClosed(HudWindow window, World world)
    {
        window.VisibilityChanged += () =>
        {
            if (!window.Visible) return;
            Callable.From(() =>
            {
                if (!window.Visible || Native.Get<object>(world, "_bagPairing") is not { } pairing) return;
                if (!Native.Get<bool>(pairing, "_openedByCompanion")) return;
                Native.Set(pairing, "_openedByCompanion", false);
                Native.Call(world, "HideMainWindow", "Inventory");
            }).CallDeferred();
        };
    }

    /// <summary>Disassembly: the bench like the Chaotic Generator, with the Classic bag instead of the docked inventory.</summary>
    private static void PrepareSmash(HudWindow window, World world)
    {
        Name(world, "_smashSocket", "smash_socket");
        Name(world, "_smashMessage", "smash_message");
        Name(world, "_smashSubMessage", "smash_status");
        Name(world, "_smashStartBtn", "smash_start");
        Name(world, "_smashStopBtn", "smash_stop");
        Name(world, "_smashCancelBtn", "smash_cancel");
        if (Native.Get<Array>(world, "_smashResults") is { } results)
            for (int i = 0; i < results.Length; i++) NativeWindows.Name(results.GetValue(i) as Node, "smash_result_" + i);
        NativeBagCells.Attach(window, world, "smash_bag_", "SmashTakeFromBag", "SmashBagFit");
        DropOnSocket(world, "_smashSocket", "smash_drop", "_smashShown", "SmashTakeFromBag");
    }

    /// <summary>Character Seal scroll / Cypher Ring transfer: the seal layout with the Classic bag.</summary>
    private static void PrepareCharacterSealTrans(HudWindow window, World world)
    {
        Name(world, "_csSocket", "cstrans_socket");
        Name(world, "_csTransHeadline", "cstrans_headline");
        Name(world, "_csTransGold", "cstrans_gold");
        if (Native.Get<Label>(world, "_csTransHeadline")?.GetParent() is Container stage)
            NativeWindows.Name(stage.GetChildren().OfType<Label>().LastOrDefault(l => l.Name != "cstrans_headline"), "cstrans_notice");
        var buttons = Native.Descendants(window.Body).OfType<Button>().Where(b => b.Text is "OK" or "Cancel").ToArray();
        if (buttons.Length >= 2)
        {
            buttons[0].Name = "cstrans_ok";
            buttons[1].Name = "cstrans_cancel";
        }
        if (Native.Get<Label>(world, "_csTransGold")?.GetParent() is Container footer)
            NativeWindows.Name(footer.GetChildren().OfType<Label>().FirstOrDefault(l => l.Name != "cstrans_gold"), "cstrans_gold_caption");
        NativeBagCells.Attach(window, world, "cstrans_bag_", "CharacterSealTakeFromBag", "CharacterSealBagFit");
        DropOnSocket(world, "_csSocket", "cstrans_drop", "_csTransShown", "CharacterSealTakeFromBag");
    }

    /// <summary>Lets a carried item be dragged onto a service socket; the client's bag action takes it.</summary>
    private static void DropOnSocket(World world, string socketField, string name, string shownField, string take)
    {
        if (Native.Get<Control>(world, socketField) is not { } socket) return;
        int Slot(Variant data)
        {
            if (!Native.Get<bool>(world, shownField) || data.VariantType != Variant.Type.Dictionary) return -1;
            var dict = data.AsGodotDictionary();
            if (!dict.ContainsKey("invFrom")) return -1;
            int abs = dict["invFrom"].AsInt32();
            return abs >= Inventory.GridStart && abs < Inventory.GridStart + Inventory.GridCount ? abs : -1;
        }
        socket.AddChild(new ServiceDropTarget(data => Slot(data) >= 0, data => Native.Call(world, take, Slot(data))) { Name = name });
    }

    private static void PrepareCombine(HudWindow window, World world)
    {
        Name(world, "_itemCombineStrip", "combine_detail");
        Name(world, "_itemCombineFooter", "combine_footer");
        Name(world, "_itemCombineResult", "combine_result");
        Name(world, "_itemCombineButton", "combine_action");
        if (Native.Get<ItemSlotView[]>(world, "_itemCombineSlots") is { } slots)
            for (int i = 0; i < slots.Length; i++) NativeWindows.Name(slots[i], "combine_material_" + i);
        HookCombineAmount(world);
        NativeWindows.Sync(window, () => HookCombineAmount(world));
    }

    /// <summary>
    /// While the combination window is open, the shared amount prompt only asks for a quantity:
    /// confirming it stages the material instead of continuing to a sale approval.
    /// </summary>
    public static void HookCombineAmount(World world)
    {
        if (Native.Get<CanvasLayer>(world, "_amountLayer") is not { } layer || !GodotObject.IsInstanceValid(layer)
            || layer.HasMeta("classic_combine_amount")) return;
        layer.SetMeta("classic_combine_amount", true);
        layer.VisibilityChanged += () =>
        {
            if (!layer.Visible) return;
            if (!Native.Get<bool>(world, "_itemCombineShown"))
            {
                layer.RemoveMeta("merchant_quantity_only");
                return;
            }
            layer.SetMeta("merchant_quantity_only", true);
            layer.SetMeta("merchant_price_editable", false);
            layer.SetMeta("merchant_quantity", Native.Get<SpinBox>(world, "_amountCount") is { MaxValue: > 1 });
            layer.SetMeta("merchant_error", "");
        };
    }

    private static void PrepareRecipeBook(HudWindow window)
    {
        // Recipe rows are rebuilt on every selection; mark them as they enter the tree, before the
        // Classic skin restyles them, using the selected row style the client gave them.
        void Mark(Node node)
        {
            if (node is ItemSlotView view) ClassicCount(view);
            if (node is Button button && button.GetThemeStylebox("normal") is StyleBoxFlat style)
                button.SetMeta(SelectedMeta, style.BorderWidthLeft == SelectedRowBorder);
        }
        foreach (var node in Native.Descendants(window.Body)) Mark(node);
        NativeWindows.OnDescendantAdded(window.Body, Mark);
    }

    private static void PrepareRedistribution(HudWindow window, World world)
    {
        foreach (var label in Native.Descendants(window.Body).OfType<Label>())
            if (label.Text.StartsWith(RedistributionHeader, StringComparison.Ordinal)) { label.Name = "redistribution_description"; break; }
        NativeWindows.Sync(window, () => window.SetMeta("redistribution_pending", RedistributionPending(world)));
    }

    /// <summary>
    /// Classic item count: stackable items show "1" as well, and the count is laid out over the whole
    /// cell so it sits in the bottom-right corner like the inventory's.
    /// </summary>
    public static void ClassicCount(ItemSlotView view)
    {
        var count = view.CountLabel;
        if (!count.HasMeta("classic_count"))
        {
            count.SetMeta("classic_count", true);
            count.SizeFlagsHorizontal = count.SizeFlagsVertical = Control.SizeFlags.Fill;
            count.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        }
        CountBadge(view);
    }

    /// <summary>Applies the Classic count text to a cell after the client set its item.</summary>
    public static void CountBadge(ItemSlotView view)
    {
        var item = view.Item;
        var definition = item.IsEmpty ? null : ItemData.Get(item.ItemId);
        string text = item.IsEmpty ? "" : NativeUi.CountBadge(definition, ItemData.ShownCount(definition, item));
        if (view.CountLabel.Text != text) view.CountLabel.Text = text;
    }

    /// <summary>Whether a stat or mastery redistribution is waiting for the server.</summary>
    public static bool RedistributionPending(World world) =>
        Native.Has(world, "_reset") && Native.Get<object>(world, "_reset") is { } request
            ? Native.Get<bool>(request, "Pending")
            : RedistributionKind(world) != 0;

    /// <summary>The redistribution kind being requested, or 0.</summary>
    public static byte RedistributionKind(World world) =>
        Native.Has(world, "_reset") && Native.Get<object>(world, "_reset") is { } request
            ? Native.Get<byte>(request, "Kind")
            : Native.Get<byte>(world, "_resetKind");
}

/// <summary>
/// Classic repair works through the inventory with the hammer cursor: the client's repair catalogue
/// stays hidden, and the inventory window receives the repair mode, pending state and actions as
/// metas. Repairs still go through the client's serialized repair requests and results.
/// </summary>
public sealed class NativeRepair
{
    private const string NotEnoughGold = "Not enough Noahs to repair this item.";
    private const string Failed = "Repair failed.";

    private readonly HudWindow _panel;
    private readonly World _world;
    private readonly KnightOnlineUiClassic.Windows.ClassicRepairHammer _hammer;
    private bool _shown, _pending;

    private NativeRepair(HudWindow panel, World world)
    {
        _panel = panel;
        _world = world;
        _hammer = new KnightOnlineUiClassic.Windows.ClassicRepairHammer(() => Classic && Shown) { Name = "classic_repair_hammer" };
    }

    /// <summary>The hammer swing of the repair mode prepared on this repair window.</summary>
    public static KnightOnlineUiClassic.Windows.ClassicRepairHammer? HammerOf(HudWindow panel) =>
        panel.GetNodeOrNull<KnightOnlineUiClassic.Windows.ClassicRepairHammer>("classic_repair_hammer");

    public static void Attach(HudWindow panel, World world)
    {
        var repair = new NativeRepair(panel, world);
        panel.AddChild(repair._hammer);
        panel.VisibilityChanged += () =>
        {
            if (!panel.Visible || !repair.Classic) return;
            Callable.From(() =>
            {
                if (repair.Shown) panel.Visible = false;
                repair.Sync();
            }).CallDeferred();
        };
        var net = Net.I;
        NativeHooks.Listen("Net.ItemRepairResultEvent", () => net.ItemRepairResultEvent += repair.OnResult,
            () => net.ItemRepairResultEvent -= repair.OnResult, world);
        // The catalogue stays hidden while repairing, so state is followed from outside the window.
        panel.AddChild(new NativeTicker(repair.Sync) { Name = "native_repair_sync" });
    }

    /// <summary>The Classic skin turns the repair window into the inventory repair mode.</summary>
    private bool Classic => _panel.GetMeta("classic_inventory_repair", false).AsBool();
    private bool Shown => Native.Get<bool>(_world, "_repairShown");
    private bool InFlight => Native.Get<bool>(_world, "_repairInFlight");

    private HudWindow? Inventory =>
        Native.Get<Dictionary<string, HudWindow>>(_world, "_mainWindows") is { } windows && windows.TryGetValue("Inventory", out var window)
            ? window : null;

    private void Sync()
    {
        if (!Classic || Inventory is not { } inventory) return;
        bool shown = Shown, pending = InFlight;
        inventory.SetMeta("classic_repair_mode", shown);
        inventory.SetMeta("classic_repair_pending", pending);
        if (!inventory.HasMeta("classic_repair_take"))
        {
            // Closing the inventory ends the repair mode, as the retail hammer belongs to it.
            inventory.VisibilityChanged += () => { if (!inventory.Visible && Shown) Close(); };
            inventory.SetMeta("classic_repair_close", Callable.From(Close));
            inventory.SetMeta("classic_repair_all", Callable.From(RepairAll));
            inventory.SetMeta("classic_repair_take", Callable.From((int slot) => Take(slot)));
        }
        if (shown == _shown && pending == _pending) return;
        _shown = shown;
        _pending = pending;
        if (!shown) _hammer.Stop();
        GameCursor.Set(shown ? pending ? GameCursorKind.RepairAlt : GameCursorKind.Repair : GameCursorKind.Arrow);
    }

    /// <summary>A left click on an inventory item while repairing: repair exactly that item.</summary>
    private void Take(int slot)
    {
        if (!Shown) return;
        if (!InFlight && Affordable(slot))
        {
            Native.Call(_world, "RepairTakeFromBag", slot);
            if (InFlight) _hammer.Strike();
        }
        Sync();
    }

    private void RepairAll()
    {
        if (!Shown || InFlight) return;
        int first = Native.Call(_world, "RepairableSlots") is IEnumerable<int> slots ? slots.DefaultIfEmpty(-1).First() : -1;
        if (first < 0 || Affordable(first))
        {
            Native.Call(_world, "RepairAll");
            if (InFlight) _hammer.Strike();
        }
        Sync();
    }

    private void Close()
    {
        Native.Call(_world, "CloseRepair");
        Sync();
    }

    /// <summary>Refuses a repair the player cannot pay for before it reaches the server.</summary>
    private bool Affordable(int slot)
    {
        if (Native.Call(_world, "IsRepairable", slot) is not true) return true;
        int cost = Native.Call(_world, "RepairCostAt", slot) is int value ? value : 0;
        int gold = Native.Get<object>(_world, "Sheet") is { } sheet ? Native.Get<int>(sheet, "Gold") : 0;
        if (cost <= gold) return true;
        Native.Get<Queue<int>>(_world, "_repairQueue")?.Clear();
        Native.Get<FooterBand>(_world, "_repairFooter")?.Status(NotEnoughGold, bad: true);
        Native.Call(_world, "CombatNotice", NotEnoughGold);
        return false;
    }

    private void OnResult(bool ok, int money)
    {
        if (!ok && _pending && Classic) Native.Call(_world, "CombatNotice", Failed);
        // The client's handler ran first: Repair All has already sent its next item.
        if (ok && Classic && InFlight) _hammer.Strike();
        Sync();
    }
}

/// <summary>
/// The Classic Chaotic Generator shows the whole carried inventory (seven columns, 28 cells) and
/// accepts exchange pieces dragged from it onto the generator socket; a click or right-click on a
/// piece stages it. The client docks the inventory window instead, so the bag is the plugin's own and
/// is rebuilt whenever a carried item or the staged piece changes.
/// </summary>
public sealed class NativePieceChange
{
    private const int Columns = 7;
    private const int CellSpacing = 4;

    private readonly HudWindow _window;
    private readonly World _world;
    private readonly VBoxContainer _bag;
    private int _signature;

    private NativePieceChange(HudWindow window, World world, VBoxContainer bag)
    {
        _window = window;
        _world = world;
        _bag = bag;
    }

    public static void Attach(HudWindow window, World world)
    {
        Name(world, "_pieceMessage", "piece_message");
        Name(world, "_pieceSubMessage", "piece_status");
        Name(world, "_pieceStartBtn", "piece_start");
        Name(world, "_pieceStopBtn", "piece_stop");
        Name(world, "_pieceTalkBtn", "piece_talk");
        Name(world, "_pieceSocket", "piece_socket");
        if (Native.Get<Array>(world, "_pieceResultSockets") is { } rewards)
            for (int i = 0; i < rewards.Length; i++) NativeWindows.Name(rewards.GetValue(i) as Node, "piece_reward_" + i);
        // The client docks the inventory window; the Classic generator keeps its own full carried bag.
        var bag = new VBoxContainer { Name = "piece_bag" };
        window.Body.AddChild(bag);
        bag.AddChild(new GridContainer { Columns = Columns });
        var piece = new NativePieceChange(window, world, bag);
        if (Native.Get<Control>(world, "_pieceSocket") is { } socket)
            socket.AddChild(new ServiceDropTarget(piece.CanDrop, piece.Drop) { Name = "piece_drop" });
        NativeWindows.Sync(window, piece.Rebuild);
        NativeServices.KeepInventoryClosed(window, world);
    }

    private static void Name(World world, string field, string name) => NativeWindows.Name(Native.Get<Node>(world, field), name);

    private bool FullInventory => _window.GetMeta("piece_full_inventory", false).AsBool();
    private Inventory Inv => Native.Get<Inventory>(_world, "Inv")!;

    /// <summary>Rebuilds the Classic bag (every carried cell) whenever a carried item or the staged piece changes.</summary>
    private void Rebuild()
    {
        if (!FullInventory || !GodotObject.IsInstanceValid(_bag)) return;
        var grid = _bag.GetChildren().OfType<GridContainer>().FirstOrDefault();
        if (grid == null) return;
        var inventory = Inv;
        int staged = Native.Get<int>(_world, "_piecePosition");
        var hash = new HashCode();
        hash.Add(staged);
        for (int i = 0; i < Inventory.GridCount; i++)
            hash.Add(Inventory.GridStart + i < inventory.Length ? inventory[Inventory.GridStart + i] : default);
        int signature = hash.ToHashCode();
        if (grid.GetChildCount() == Inventory.GridCount && signature == _signature) return;
        _signature = signature;
        grid.AddThemeConstantOverride("h_separation", CellSpacing);
        grid.AddThemeConstantOverride("v_separation", CellSpacing);
        foreach (var child in grid.GetChildren())
        {
            grid.RemoveChild(child);
            child.QueueFree();
        }
        for (int i = 0; i < Inventory.GridCount; i++)
        {
            int abs = Inventory.GridStart + i;
            var item = abs < inventory.Length ? inventory[abs] : default;
            var cell = new UpgradeBackpackCell(abs, item, staged == i);
            cell.Pressed += () => Native.Call(_world, "PlacePiece", abs);
            cell.Hovered += (slot, held) => Native.Call(_world, "ShowItemTooltip", slot, held, "");
            cell.Unhovered += () => Native.Call(_world, "HideItemTooltip");
            grid.AddChild(cell);
        }
    }

    /// <summary>The carried slot a piece drag comes from, or -1 when the generator cannot take it.</summary>
    private int DropSlot(Variant data)
    {
        if (!Native.Get<bool>(_world, "_pieceShown") || Native.Get<bool>(_world, "_pieceBusy") || Native.Get<bool>(_world, "_pieceSpinning")
            || data.VariantType != Variant.Type.Dictionary) return -1;
        var dict = data.AsGodotDictionary();
        if (!dict.ContainsKey("invFrom")) return -1;
        int abs = dict["invFrom"].AsInt32();
        var inventory = Inv;
        return abs >= Inventory.GridStart && abs < Inventory.GridStart + Inventory.GridCount && abs < inventory.Length
               && !inventory[abs].IsEmpty && ItemData.IsExchangePiece(inventory[abs].ItemId) ? abs : -1;
    }

    private bool CanDrop(Variant data) => DropSlot(data) >= 0;

    private void Drop(Variant data)
    {
        int abs = DropSlot(data);
        if (abs >= 0 && Native.Get<int>(_world, "_piecePosition") != abs - Inventory.GridStart) Native.Call(_world, "PlacePiece", abs);
    }
}

/// <summary>Runs an action every frame, before the skins, whether or not its parent is visible.</summary>
public partial class NativeTicker : Node
{
    private readonly Action _tick;

    public NativeTicker() => _tick = () => { };

    public NativeTicker(Action tick)
    {
        _tick = tick;
        ProcessPriority = int.MinValue;
    }

    public override void _Process(double delta) => _tick();
}

/// <summary>A transparent drop target laid over a native control that does not accept drops itself.</summary>
public partial class ServiceDropTarget : Control
{
    private readonly Func<Variant, bool> _canDrop;
    private readonly Action<Variant> _drop;

    public ServiceDropTarget() : this(_ => false, _ => { }) { }

    public ServiceDropTarget(Func<Variant, bool> canDrop, Action<Variant> drop)
    {
        _canDrop = canDrop;
        _drop = drop;
        MouseFilter = MouseFilterEnum.Pass;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => _canDrop(data);
    public override void _DropData(Vector2 atPosition, Variant data) => _drop(data);
}

/// <summary>
/// A carried inventory cell for the Classic service benches: a left click (released without dragging)
/// or right-click selects the item, and dragging it uses the normal inventory drag protocol.
/// </summary>
public partial class UpgradeBackpackCell : PanelContainer
{
    private const float CellSize = 48f;
    private const float StagedAlpha = 0.4f;
    private const int CountFontSize = 11;
    private const int CountOutline = 4;
    private const float IconInset = 2f;

    public event Action? Pressed;
    public event Action<int, ItemSlot>? Hovered;
    public event Action? Unhovered;

    private readonly ItemSlot _item;
    private readonly int _absSlot;
    private Vector2 _grab;
    private bool _dragStarted;

    public UpgradeBackpackCell() : this(-1, default, false) { }

    public UpgradeBackpackCell(int absSlot, ItemSlot item, bool staged)
    {
        _item = item;
        _absSlot = absSlot;
        MouseEntered += () => { if (!_item.IsEmpty) Hovered?.Invoke(absSlot, _item); };
        MouseExited += () => Unhovered?.Invoke();
        CustomMinimumSize = new Vector2(CellSize, CellSize);
        AddThemeStyleboxOverride("panel", item.IsEmpty ? UiTheme.Slot() : UiTheme.Slot(staged ? UiTheme.Gold : UiTheme.Edge));
        if (item.IsEmpty) return;

        var icon = new TextureRect
        {
            Texture = ItemData.Icon(item.ItemId),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = staged ? new Color(1f, 1f, 1f, StagedAlpha) : Colors.White,
        };
        icon.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(icon);
        if (UpgradeBadge.Show(this, item.ItemId) is { } badge) badge.Modulate = icon.Modulate;

        var definition = ItemData.Get(item.ItemId);
        string text = NativeUi.CountBadge(definition, ItemData.ShownCount(definition, item));
        if (text.Length == 0) return;
        var count = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        count.AddThemeFontSizeOverride("font_size", CountFontSize);
        count.AddThemeColorOverride("font_color", Colors.White);
        count.AddThemeColorOverride("font_outline_color", Colors.Black);
        count.AddThemeConstantOverride("outline_size", CountOutline);
        count.SetAnchorsPreset(LayoutPreset.BottomRight);
        AddChild(count);
    }

    public override void _GuiInput(InputEvent ev)
    {
        if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            _grab = click.Position;
            _dragStarted = false;
        }
        if (_item.IsEmpty) return;
        if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }
            || ev is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } released
               && !_dragStarted && new Rect2(Vector2.Zero, Size).HasPoint(released.Position))
            Pressed?.Invoke();
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_item.IsEmpty || _absSlot < 0) return default;
        _dragStarted = true;
        var preview = new Control { MouseFilter = MouseFilterEnum.Ignore };
        preview.AddChild(new TextureRect
        {
            Texture = ItemData.Icon(_item.ItemId),
            Size = Size - new Vector2(IconInset * 2, IconInset * 2),
            Position = new Vector2(IconInset, IconInset) - _grab,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        DragLayer.Show(this, preview);
        return new Godot.Collections.Dictionary { { "invFrom", _absSlot } };
    }
}

/// <summary>
/// The carried inventory of a Classic service window whose client docks the inventory window as its
/// bag companion instead (seal). The cells mirror the carried items, mark the staged item, and hand a
/// right-click to the client's own companion action; dragging uses the normal inventory protocol.
/// </summary>
public static class NativeBagCells
{
    private const float CellSize = 45f;

    public static void Attach(HudWindow window, World world, string prefix, string take, string fit)
    {
        if (!Native.TryGet<Inventory>(world, "Inv", out var inventory)) return;
        var holder = new Control { Name = prefix + "cells", Visible = false };
        window.Body.AddChild(holder);
        var cells = new ItemSlotView[Inventory.GridCount];
        for (int i = 0; i < cells.Length; i++)
        {
            var cell = new ItemSlotView(CellSize) { Index = Inventory.GridStart + i, Name = prefix + i, Veiled = true };
            cell.RightClicked += c => Native.Call(world, take, c.Index);
            cell.Hovered += c => Native.Call(world, "ShowItemTooltip", c.Index, c.Item, "");
            cell.Unhovered += _ => Native.Call(world, "HideItemTooltip");
            cell.DragOut = c => c.Item.IsEmpty ? default(Variant) : (Variant)new Godot.Collections.Dictionary { { "invFrom", c.Index } };
            holder.AddChild(cell);
            cells[i] = cell;
        }
        NativeWindows.Sync(window, () =>
        {
            for (int i = 0; i < cells.Length; i++)
            {
                int abs = Inventory.GridStart + i;
                var item = abs < inventory.Length ? inventory[abs] : default;
                if (!cells[i].Item.Equals(item))
                {
                    cells[i].Set(item);
                    NativeServices.CountBadge(cells[i]);
                }
                bool staged = !item.IsEmpty && Native.Call(world, fit, abs) is { } state && state.ToString() == "Staged";
                var look = staged ? SlotLook.Selected : SlotLook.Normal;
                if (cells[i].Look != look) cells[i].Look = look;
            }
        });
        NativeServices.KeepInventoryClosed(window, world);
    }
}
