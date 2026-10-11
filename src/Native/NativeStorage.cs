using System.Runtime.CompilerServices;
using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Native;

public static partial class NativeSetup
{
    static partial void RegisterStorage(PluginContext context)
    {
        foreach (string id in NativeStorage.WindowIds)
            NativeWindows.Prepare(id, (window, world) => NativeStorage.Prepare(window, world));
    }
}

/// <summary>
/// The Classic storage adapter for the client's normal warehouse, VIP vault and clan warehouse. It
/// builds the named stored and carried item cells and the pager the Classic storage skin arranges,
/// names the native coin, PIN and key controls, and shows storage in Classic pages (24 cells; the
/// 48-slot VIP vault in four pages of 12). Every transfer goes through the client's own handlers:
/// right-click picks a destination, drag uses the chosen cell, stacks ask for an amount in the
/// client's prompt, and the client validates, sends and applies each transfer.
/// </summary>
public sealed class NativeStorage
{
    public enum StorageKind { Warehouse, Vip, Clan }

    public static readonly string[] WindowIds = { "warehouse", "vipwarehouse", "clanwarehouse" };

    private const float CellSize = 44f;
    private const int CarriedCells = 28;
    private const int WarehouseSlots = 192;
    private const int WarehousePageSize = 24;
    private const int VipPageSize = 12;
    private const int ClanPageSize = 24;

    private static readonly ConditionalWeakTable<HudWindow, NativeStorage> _storages = new();

    public HudWindow Window { get; }
    public StorageKind Kind { get; }
    public int PageSize { get; }
    public int Pages { get; }
    public QuantityPrompt? Amount { get; }
    public VipVaultPinPrompt? PinPrompt { get; private set; }

    /// <summary>The visible storage page: the clan bank keeps the client's page, the others page in the skin.</summary>
    public int Page
    {
        get => _nativePage ? Native.Get<int>(_world, "_clanWhPage") : _page;
        private set
        {
            if (_nativePage) Native.Call(_world, "TurnClanWhPage", value - Page);
            else _page = value;
        }
    }

    private readonly World _world;
    private readonly Inventory _inventory;
    private readonly ItemSlot[] _slots;
    private readonly int _gridStart;
    private readonly ItemSlotView[] _stored;
    private readonly ItemSlotView[] _carried = new ItemSlotView[CarriedCells];
    private readonly Label _pageLabel;
    private readonly Label? _status;
    private readonly bool _nativePage;
    private int _page;

    private bool Vip => Kind == StorageKind.Vip;
    private int Slots => _slots.Length;

    public static NativeStorage? Of(HudWindow window) => _storages.TryGetValue(window, out var storage) ? storage : null;

    /// <summary>Prepares a storage window once. Returns null when the client lacks a required member.</summary>
    public static NativeStorage? Prepare(HudWindow window, World world)
    {
        if (_storages.TryGetValue(window, out var existing)) return existing;
        StorageKind? kind = window.Id switch
        {
            "warehouse" => StorageKind.Warehouse,
            "vipwarehouse" => StorageKind.Vip,
            "clanwarehouse" => StorageKind.Clan,
            _ => null,
        };
        if (kind == null) return null;
        string slotsField = kind switch { StorageKind.Warehouse => "_warehouse", StorageKind.Vip => "_vipWh", _ => "_clanWh" };
        if (!Native.TryGet<ItemSlot[]>(world, slotsField, out var slots) || !Native.TryGet<Inventory>(world, "Inv", out var inventory))
            return null;
        var storage = new NativeStorage(window, world, kind.Value, slots, inventory);
        _storages.Add(window, storage);
        return storage;
    }

    private NativeStorage(HudWindow window, World world, StorageKind kind, ItemSlot[] slots, Inventory inventory)
    {
        Window = window;
        Kind = kind;
        _world = world;
        _slots = slots;
        _inventory = inventory;
        _gridStart = Inventory.GridStart;
        _nativePage = kind == StorageKind.Clan && Native.Has(world, "_clanWhPage");
        PageSize = kind switch { StorageKind.Warehouse => WarehousePageSize, StorageKind.Vip => VipPageSize, _ => ClanPageSize };
        Pages = Math.Max(1, (kind == StorageKind.Warehouse ? WarehouseSlots : slots.Length) / PageSize);

        var controls = new Control { Name = "storage_classic_controls", Visible = false };
        window.Body.AddChild(controls);
        _stored = new ItemSlotView[PageSize];
        HideNativeGrid(Field("_whCells", "_vipWhCells", "_clanWhCells"));

        _status = Native.Get<Label>(world, Field("_whStatus", "_vipWhStatus", "_clanWhStatus"));
        _pageLabel = new Label { Name = "storage_page", Text = $"1 / {Pages}" };
        controls.AddChild(_pageLabel);
        foreach (int step in new[] { -1, 1 })
        {
            var button = new Button { Name = step < 0 ? "storage_prev" : "storage_next", FocusMode = Control.FocusModeEnum.None };
            button.Pressed += () => TurnPage(step);
            controls.AddChild(button);
        }
        Amount = Native.Get<QuantityPrompt>(world, Field("_whAmount", "_vipWhAmount", "_clanWhAmount"));
        if (Vip)
        {
            NativeWindows.Name(Native.Get<Label>(world, "_vipWhExpiryLbl"), "storage_expiry");
            NativeWindows.Name(Native.Get<Control>(world, "_vipKeyCell"), "storage_key");
            NativeWindows.Name(Native.Get<Button>(world, "_vipSetPinBtn"), "storage_pin_set");
            NativeWindows.Name(Native.Get<Button>(world, "_vipClearPinBtn"), "storage_pin_clear");
            PreparePinPrompt();
        }
        else
        {
            NativeWindows.Name(Native.Get<Button>(world, Kind == StorageKind.Clan ? "_clanWhDepositBtn" : "_whDepositBtn"), "storage_deposit");
            NativeWindows.Name(Native.Get<Button>(world, Kind == StorageKind.Clan ? "_clanWhWithdrawBtn" : "_whWithdrawBtn"), "storage_withdraw");
        }
        window.VisibilityChanged += () =>
        {
            if (!window.Visible) return;
            if (!_nativePage) _page = 0;
            Callable.From(KeepInventoryClosed).CallDeferred();
        };
        NativeWindows.Name(_status, "storage_status");
        if (Amount != null) window.SetMeta("storage_amount", Amount);

        for (int i = 0; i < PageSize; i++)
        {
            _stored[i] = StoredCell(i);
            controls.AddChild(_stored[i]);
        }
        for (int i = 0; i < CarriedCells; i++)
        {
            _carried[i] = CarriedCell(i);
            controls.AddChild(_carried[i]);
        }
        NativeWindows.Sync(window, Sync);
    }

    /// <summary>Moves to the previous or next page, stopping at the first and last page.</summary>
    public void TurnPage(int step)
    {
        int next = Mathf.Clamp(Page + step, 0, Pages - 1);
        if (next == Page) return;
        Page = next;
        Sync();
    }

    private string Field(string warehouse, string vip, string clan) => Kind switch
    {
        StorageKind.Warehouse => warehouse,
        StorageKind.Vip => vip,
        _ => clan,
    };

    private bool Busy => Native.Get<bool>(_world, "StorageTransferBusy");

    /// <summary>Whether the VIP vault is unlocked (a key is active and not expired).</summary>
    private bool VaultOpen => !Vip || Native.Get<bool>(_world, "_vipWhOpen");

    private bool InMainBag(int abs) => abs >= _gridStart && abs < _gridStart + CarriedCells && abs < _inventory.Length;

    private void Sync()
    {
        int page = Page;
        _pageLabel.Text = $"{page + 1} / {Pages}";
        _pageLabel.TooltipText = "Storage page";
        Window.SetMeta("storage_page", page);
        Window.SetMeta("storage_pages", Pages);
        if (!Vip) Window.SetMeta("storage_money", Native.Get<int>(_world, Kind == StorageKind.Clan ? "_clanWhMoney" : "_whMoney"));
        bool locked = !VaultOpen;
        for (int i = 0; i < _stored.Length; i++)
        {
            int abs = page * PageSize + i;
            Show(_stored[i], abs < Slots ? _slots[abs] : default);
            // A locked vault marks its stored items; empty cells stay plain.
            bool itemLocked = locked && !_stored[i].Item.IsEmpty;
            if (Vip && _stored[i].Locked != itemLocked) _stored[i].Locked = itemLocked;
        }
        for (int i = 0; i < CarriedCells; i++)
        {
            int abs = _gridStart + i;
            Show(_carried[i], abs < _inventory.Length ? _inventory[abs] : default);
            var look = abs >= _inventory.Length || Fits(abs) ? SlotLook.Normal : SlotLook.Unfit;
            if (_carried[i].Look != look) _carried[i].Look = look;
        }
        // The small key plaque shows an expired key greyed, without the lock badge that would cover it.
        if (Vip && Native.Get<ItemSlotView>(_world, "_vipKeyCell") is { } key && Native.Get<Control>(key, "_lock") is { Visible: true } badge)
            badge.Visible = false;
        if (Vip && Native.Get<Button>(_world, "_vipClearPinBtn") is { } clear)
        {
            // Both PIN actions keep their place; clearing is only offered once a PIN is set.
            clear.Visible = true;
            clear.Disabled = !Native.Get<bool>(_world, "_vipWhPinSet");
        }
    }

    /// <summary>Whether the client lets this carried item into the storage (its own bag-fit rule).</summary>
    private bool Fits(int abs)
    {
        if (_inventory[abs].IsEmpty) return true;
        return Native.Call(_world, Field("WhBagFit", "VipBagFit", "ClanBagFit"), abs) is not { } fit || fit.ToString() != "Unfit";
    }

    private static void Show(ItemSlotView cell, ItemSlot item)
    {
        if (cell.Item.Equals(item)) return;
        cell.Set(item);
        if (item.IsEmpty) return;
        var def = ItemData.Get(item.ItemId);
        cell.CountLabel.Text = NativeUi.CountBadge(def, ItemData.ShownCount(def, item));
    }

    private void Tooltip(int abs, ItemSlotView cell)
    {
        if (cell.Item.IsEmpty) Native.Call(_world, "HideItemTooltip");
        else Native.Call(_world, "ShowItemTooltip", abs, cell.Item, "");
    }

    /// <summary>Moves the client's own storage grid out of the window body; the Classic cells replace it.</summary>
    private void HideNativeGrid(string field)
    {
        var cells = Native.Get<ItemSlotView[]>(_world, field);
        if (cells is not { Length: > 0 } || cells[0]?.GetParent()?.GetParent() is not Control well) return;
        var hidden = new Control { Name = "native_storage_grid", Visible = false };
        Window.AddChild(hidden);
        well.Reparent(hidden, false);
    }

    /// <summary>Keeps the carried inventory embedded: opening storage does not open the inventory window.</summary>
    private void KeepInventoryClosed()
    {
        if (!Window.Visible || Native.Get<object>(_world, "_bagPairing") is not { } pairing) return;
        if (!Native.Get<bool>(pairing, "_openedByCompanion")) return;
        Native.Set(pairing, "_openedByCompanion", false);
        Native.Call(_world, "HideMainWindow", "Inventory");
    }

    // Stored cells ------------------------------------------------------------------------------

    private ItemSlotView StoredCell(int index)
    {
        int Absolute() => Page * PageSize + index;
        var cell = new ItemSlotView(CellSize) { Index = index, Name = "storage_cell_" + index, Veiled = true };
        cell.RightClicked += _ => Native.Call(_world, Field("AskWithdraw", "AskVipWithdraw", "AskClanWithdraw"), Absolute(), -1);
        cell.Hovered += c => Tooltip(-1, c);
        cell.Unhovered += _ => Native.Call(_world, "HideItemTooltip");
        cell.Wheeled += (_, step) => TurnPage(step);
        cell.DragOut = _ => VaultOpen && !_slots[Absolute()].IsEmpty
            ? (Variant)new Godot.Collections.Dictionary { { "companionFrom", Absolute() } }
            : default(Variant);
        cell.CanDrop = (_, data) => Native.Call(_world, Field("CanDropOnWarehouse", "CanDropOnVipVault", "CanDropOnClanWh"), Absolute(), data) is true;
        cell.Dropped = (_, data) => Native.Call(_world, Field("DropOnWarehouse", "DropOnVipVault", "DropOnClanWh"), Absolute(), data);
        return cell;
    }

    // Carried inventory cells -------------------------------------------------------------------

    private ItemSlotView CarriedCell(int index)
    {
        var cell = new ItemSlotView(CellSize) { Index = _gridStart + index, Name = "storage_bag_" + index, Veiled = true };
        cell.RightClicked += c => Native.Call(_world, Field("WhTakeFromBag", "VipTakeFromBag", "ClanTakeFromBag"), c.Index);
        cell.Hovered += c => Tooltip(c.Index, c);
        cell.Unhovered += _ => Native.Call(_world, "HideItemTooltip");
        cell.DragOut = c => new Godot.Collections.Dictionary { { "invFrom", c.Index } };
        cell.CanDrop = (c, data) => !Busy && (BagMove(c.Index, data) || TakesFromStorage(data));
        cell.Dropped = (c, data) =>
        {
            var d = data.AsGodotDictionary();
            if (BagMove(c.Index, data)) Native.Call(_world, "MoveBetween", d["invFrom"].AsInt32(), c.Index);
            else if (TakesFromStorage(data))
                Native.Call(_world, Field("WithdrawInto", "VipWithdrawInto", "ClanWithdrawInto"), d["companionFrom"].AsInt32(), c.Index);
        };
        return cell;
    }

    private bool BagMove(int target, Variant data)
    {
        if (Busy || data.VariantType != Variant.Type.Dictionary) return false;
        var d = data.AsGodotDictionary();
        return d.ContainsKey("invFrom") && d["invFrom"].AsInt32() != target && InMainBag(d["invFrom"].AsInt32());
    }

    private bool TakesFromStorage(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary) return false;
        var d = data.AsGodotDictionary();
        if (!d.ContainsKey("companionFrom")) return false;
        int from = d["companionFrom"].AsInt32();
        return from >= 0 && from < Slots && !_slots[from].IsEmpty;
    }

    // VIP PIN -----------------------------------------------------------------------------------

    /// <summary>Shows the client's PIN requests in a named PIN window instead of its generic dialog.</summary>
    private void PreparePinPrompt()
    {
        var dialog = Native.Get<AcceptDialog>(_world, "_vipWhPinDlg");
        var message = Native.Get<Label>(_world, "_vipWhPinPrompt");
        var input = Native.Get<LineEdit>(_world, "_vipWhPinEdit");
        if (dialog == null || message == null || input == null) return;
        var prompt = new VipVaultPinPrompt { Name = "vipwarehouse_pin_layer" };
        _world.AddChild(prompt);
        PinPrompt = prompt;
        bool reopened = false;
        dialog.AboutToPopup += () =>
        {
            reopened = true;
            prompt.Message.Text = message.Text;
            prompt.Input.Text = input.Text;
            prompt.PopupCentered();
            Callable.From(() =>
            {
                dialog.Hide();
                if (prompt.Visible && prompt.Input.IsInsideTree()) prompt.Input.GrabFocus();
            }).CallDeferred();
        };
        prompt.Confirmed += () =>
        {
            input.Text = prompt.Input.Text;
            reopened = false;
            Native.Call(_world, "SubmitVipPin");
            if (!reopened) prompt.Hide();
        };
    }
}
