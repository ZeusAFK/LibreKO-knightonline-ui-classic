using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Native;

public static partial class NativeSetup
{
    static partial void RegisterTrade(PluginContext context) => NativeWindows.Prepare("exchange", NativeTrade.Prepare);
}

/// <summary>
/// The Classic player trade on top of the client's exchange: the offers and the carried items the skin
/// shows in its own grids, the <c>ex_*</c> state metas it reads, named modal layers, the original request
/// message, a coin amount prompt, and a mouse-only final approval before the client's own confirmation is
/// sent. The client docks the inventory beside its trade window; the Classic trade carries its own
/// inventory grid, so the inventory the trade opened is closed again.
/// </summary>
public static class NativeTrade
{
    public const int OfferSlots = 12;
    public const string FinalQuestion = "Are you sure you want to trade?";

    private sealed class State
    {
        public CanvasLayer? Request, Final;
        public Label? RequestLabel, FinalLabel;
        public bool FinalPending, Gold;
        public (int Mine, int Theirs, int MyGold, int TheirGold) Offers;
    }

    private static readonly ConditionalWeakTable<World, State> _states = new();

    private static State StateOf(World world) => _states.GetOrCreateValue(world);

    private static readonly ConditionalWeakTable<HudWindow, World> _worlds = new();

    /// <summary>The world whose exchange the trade window shows (also for a window kept outside the world).</summary>
    public static World? WorldOf(HudWindow window) => _worlds.TryGetValue(window, out var world) && GodotObject.IsInstanceValid(world) ? world : Native.WorldOf(window);

    public static CanvasLayer? RequestLayer(World world) => StateOf(world).Request;

    public static CanvasLayer? FinalLayer(World world) => StateOf(world).Final;

    public static bool FinalPending(World world) => StateOf(world).FinalPending;

    /// <summary>An item in an offer grid or in the carried inventory; <c>Slot</c> is the bag slot it came from.</summary>
    public readonly record struct TradeItem(int Slot, int ItemId, int Count, short Durability);

    /// <summary>The offered items in offer order (mine or the partner's).</summary>
    public static IReadOnlyList<TradeItem> Offers(World world, bool mine)
    {
        var result = new List<TradeItem>();
        foreach (var offer in Native.Get<IEnumerable>(world, mine ? "_exMyOffer" : "_exTheirOffer") ?? Array.Empty<object>())
            result.Add(new TradeItem(Native.Get<int>(offer!, "SourceAbs"), Native.Get<int>(offer!, "ItemId"), Native.Get<int>(offer!, "Count"), Native.Get<short>(offer!, "Dura")));
        return result;
    }

    /// <summary>The carried inventory grid (bag slots), with the items still in the bag.</summary>
    public static IReadOnlyList<TradeItem> Bag(World world)
    {
        var result = new List<TradeItem>();
        if (Native.Get<Inventory>(world, "Inv") is not { } inv) return result;
        for (int abs = Inventory.GridStart; abs < Inventory.GridStart + Inventory.GridCount && abs < inv.Length; abs++)
            if (!inv[abs].IsEmpty) result.Add(new TradeItem(abs, inv[abs].ItemId, inv[abs].Count, inv[abs].Durability));
        return result;
    }

    /// <summary>
    /// Offers a bag slot through the client (quantity prompt, refusals and pending state stay native). A part of the
    /// stack picked with the mouse wheel is offered without the prompt (ff78eef: <c>OfferSlot(int, int? amount)</c>).
    /// </summary>
    public static void Offer(World world, int abs, int? part = null)
    {
        if (part is int amount && Native.TryCall(world, "OfferSlot", out _, abs, (int?)amount)) return;
        Native.Call(world, "OfferSlot", abs);
    }

    public static void Tooltip(World world, TradeItem item, bool bag) =>
        Native.Call(world, "ShowItemTooltip", bag ? item.Slot : -1, new ItemSlot { ItemId = item.ItemId, Count = (short)item.Count, Durability = item.Durability }, "");

    public static void HideTooltip(World world) => Native.Call(world, "HideItemTooltip");

    /// <summary>Closes the inventory window the client opened as the trade's bag companion.</summary>
    private static void KeepInventoryClosed(World world)
    {
        if (!Native.Get<bool>(world, "_exShown") || Native.Get<object>(world, "_bagPairing") is not { } pairing) return;
        if (!Native.Get<bool>(pairing, "_openedByCompanion")) return;
        Native.Set(pairing, "_openedByCompanion", false);
        Native.Call(world, "HideMainWindow", "Inventory");
    }

    public static void Prepare(HudWindow window, World world)
    {
        if (window.HasMeta("native_exchange")) return;
        window.SetMeta("native_exchange", true);
        _worlds.AddOrUpdate(window, world);
        var state = StateOf(world);
        NativeWindows.Name(Native.Get<Label>(world, "_exStatus"), "exchange_status");
        window.VisibilityChanged += () =>
        {
            if (window.Visible) Callable.From(() => { if (GodotObject.IsInstanceValid(world)) KeepInventoryClosed(world); }).CallDeferred();
        };
        if (Native.Get<CanvasLayer>(world, "_exWaitLayer") is { } wait) { wait.Name = "exchange_wait"; wait.SetMeta("exchange_wait", true); }
        if (Native.Get<CanvasLayer>(world, "_exAmountLayer") is { } amount)
        {
            amount.Name = "exchange_amount";
            amount.SetMeta("exchange_amount", true);
            amount.SetMeta("ex_amount_error", "");
            amount.VisibilityChanged += () =>
            {
                if (!amount.Visible) { state.Gold = false; return; }
                amount.SetMeta("ex_amount_error", "");
                amount.SetMeta("ex_gold", state.Gold);
            };
            if (Native.Descendants(amount).OfType<Button>().FirstOrDefault(b => b.Text == "Offer") is { } offer)
                NativeVendor.Rewire(offer, () => ConfirmAmount(world));
        }

        state.Request = Layer(world, "exchange_request", 76, out state.RequestLabel,
            ("exchange_accept", () => Answer(world, true)), ("exchange_decline", () => Answer(world, false)));
        state.Final = Layer(world, "exchange_final", 77, out state.FinalLabel,
            ("exchange_final_accept", () => AcceptFinal(world)), ("exchange_final_decline", () => CloseFinal(world)));
        state.Final.AddChild(new NativeTradeSync(world) { Name = "native_trade_sync" });

        if (Native.Get<Button>(world, "_exConfirmBtn") is { } confirm) NativeVendor.Rewire(confirm, () => RequestFinal(world));
        if (Native.Descendants(window.Body).OfType<Button>().FirstOrDefault(b => b.Text == "Add gold") is { } gold)
            NativeVendor.Rewire(gold, () => OpenGold(world));

        // The original request message replaces the desktop dialog in the same call that opened it.
        var net = Net.I;
        Action<int> request = _ => Sync(world);
        NativeHooks.Listen("Net.ExchangeRequestEvent", () => net.ExchangeRequestEvent += request, () => net.ExchangeRequestEvent -= request, world);
        // A changed offer withdraws a pending final approval in the same reply that changed it.
        Action<int, int, short, int> partnerAdded = (_, _, _, _) => Sync(world);
        NativeHooks.Listen("Net.ExchangeOtherAddEvent", () => net.ExchangeOtherAddEvent += partnerAdded, () => net.ExchangeOtherAddEvent -= partnerAdded, world);
        Sync(world);
    }

    private static CanvasLayer Layer(World world, string name, int layer, out Label label, (string Name, Action Pressed) accept, (string Name, Action Pressed) decline)
    {
        var canvas = new CanvasLayer { Name = name, Layer = layer, Visible = false };
        canvas.SetMeta(name, true);
        world.AddChild(canvas);
        var blocker = new ColorRect { Color = Colors.Transparent, MouseFilter = Control.MouseFilterEnum.Stop };
        blocker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(blocker);
        var centre = new CenterContainer();
        centre.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(centre);
        var box = new VBoxContainer();
        centre.AddChild(box);
        label = UiTheme.Text("", 13);
        box.AddChild(label);
        var buttons = new HBoxContainer();
        box.AddChild(buttons);
        foreach (var (id, text, pressed) in new[] { (accept.Name, "Accept", accept.Pressed), (decline.Name, "Decline", decline.Pressed) })
        {
            var button = new Button { Name = id, Text = text, FocusMode = Control.FocusModeEnum.None };
            button.Pressed += pressed;
            buttons.AddChild(button);
        }
        return canvas;
    }

    /// <summary>Mirrors the trade state into the window metas and keeps the Classic modals in step.</summary>
    internal static void Sync(World world)
    {
        var state = StateOf(world);
        if (Native.Get<HudWindow>(world, "_exPanel") is not { } panel) return;
        bool shown = Native.Get<bool>(world, "_exShown"), amountShown = Native.Get<bool>(world, "_exAmountShown");
        if (state.FinalPending && (!shown || Native.Get<bool>(world, "_exConfirmedByMe") || Offers(world) != state.Offers)) CloseFinal(world);
        if (!amountShown) state.Gold = false;

        panel.SetMeta("ex_locked", Native.Get<bool>(world, "_exConfirmedByMe"));
        panel.SetMeta("ex_partner_locked", Native.Get<bool>(world, "_exConfirmedByPartner"));
        panel.SetMeta("ex_pending", Native.Get<bool>(world, "_exAddInFlight"));
        panel.SetMeta("ex_modal", amountShown || state.FinalPending);
        panel.SetMeta("ex_wallet", Net.I.Sheet.Gold);
        panel.SetMeta("ex_my_gold", Native.Get<int>(world, "_exMyGoldOffer"));
        panel.SetMeta("ex_other_gold", Native.Get<int>(world, "_exTheirGoldOffer"));
        panel.SetMeta("ex_partner", Native.Get<string>(world, "_exPartnerName") ?? "Player");
        panel.SetMeta("ex_self", Net.I.LastEnter.Name ?? "You");
        Native.Get<CanvasLayer>(world, "_exAmountLayer")?.SetMeta("ex_gold", state.Gold);

        if (Native.Get<Label>(world, "_exStatus") is { } status)
        {
            string text = status.Text switch
            {
                "Waiting for partner…" => "Waiting for partner...",
                "Finalising…" => "Finalising...",
                "Partner confirmed — press Confirm." => "Partner confirmed",
                var other => other,
            };
            if (text != status.Text) status.Text = text;
        }

        bool pending = Native.Get<bool>(world, "_exRequestPending");
        if (state.Request is { } request && panel.HasMeta("classic_exchange"))
        {
            if (pending && !request.Visible)
            {
                state.RequestLabel!.Text = $"{Native.Get<string>(world, "_exPartnerName")} wants to trade with you.\nDo you agree?";
                request.Visible = true;
            }
            else if (!pending && request.Visible) request.Visible = false;
            // The Classic request replaces the desktop dialog; hiding it does not answer the request.
            if (Native.Get<Window>(world, "_exAskDialog") is { Visible: true } dialog) dialog.Hide();
        }
    }

    private static void Answer(World world, bool accept)
    {
        if (StateOf(world).Request is { } request) request.Visible = false;
        if (Native.Get<Window>(world, "_exAskDialog") is { Visible: true } dialog) dialog.Hide();
        Native.Call(world, "AnswerExchangeRequest", accept);
        Sync(world);
    }

    private static (int, int, int, int) Offers(World world) => (
        Native.Get<ICollection>(world, "_exMyOffer")?.Count ?? 0, Native.Get<ICollection>(world, "_exTheirOffer")?.Count ?? 0,
        Native.Get<int>(world, "_exMyGoldOffer"), Native.Get<int>(world, "_exTheirGoldOffer"));

    /// <summary>The Trade button asks first; Enter never reaches this decision.</summary>
    public static void RequestFinal(World world)
    {
        var state = StateOf(world);
        if (!Native.Get<bool>(world, "_exShown") || Native.Get<bool>(world, "_exConfirmedByMe") || state.FinalPending) return;
        if (Native.Get<bool>(world, "_exAddInFlight") || Native.Get<bool>(world, "_exAmountShown"))
        {
            Native.Call(world, "SetExStatus", "Finish adding your offer before confirming.", true);
            return;
        }
        Native.Call(world, "HideItemTooltip");
        state.FinalLabel!.Text = FinalQuestion;
        state.Offers = Offers(world);
        state.FinalPending = true;
        state.Final!.Visible = true;
        Sync(world);
    }

    public static void CloseFinal(World world)
    {
        var state = StateOf(world);
        state.FinalPending = false;
        if (state.Final != null) state.Final.Visible = false;
    }

    private static void AcceptFinal(World world)
    {
        var state = StateOf(world);
        if (!state.FinalPending) return;
        bool unchanged = Offers(world) == state.Offers;
        CloseFinal(world);
        if (!unchanged || !Native.Get<bool>(world, "_exShown") || Native.Get<bool>(world, "_exConfirmedByMe")
            || Native.Get<bool>(world, "_exAddInFlight") || Native.Get<bool>(world, "_exAmountShown")) return;
        Native.Call(world, "OnExchangeConfirm");
        Sync(world);
    }

    /// <summary>Opens the original amount frame for coins instead of the client's gold text field.</summary>
    public static void OpenGold(World world)
    {
        var state = StateOf(world);
        int gold = Net.I.Sheet.Gold;
        if (!Native.Get<bool>(world, "_exShown") || Native.Get<bool>(world, "_exConfirmedByMe") || Native.Get<bool>(world, "_exConfirmedByPartner")
            || Native.Get<bool>(world, "_exAddInFlight") || state.FinalPending || gold <= 0) return;
        if (Native.Get<CanvasLayer>(world, "_exAmountLayer") is not { } layer || Native.Get<SpinBox>(world, "_exAmountSpin") is not { } spin) return;
        Native.Call(world, "HideItemTooltip");
        state.Gold = true;
        Native.Set(world, "_exAmountSlot", -1);
        Native.Set(world, "_exAmountMax", gold);
        if (Native.Get<TextureRect>(world, "_exAmountIcon") is { } icon) icon.Texture = UiIcons.Get("system/coin");
        if (Native.Get<Label>(world, "_exAmountName") is { } name) name.Text = "Offer coins";
        if (Native.Get<Label>(world, "_exAmountHint") is { } hint) hint.Text = $"You have {gold:n0}";
        spin.MaxValue = gold;
        spin.Value = 1;
        spin.GetLineEdit().Text = "1";
        layer.SetMeta("ex_gold", true);
        layer.Visible = true;
        Native.Set(world, "_exAmountShown", true);
        spin.GetLineEdit().GrabFocus();
        spin.GetLineEdit().SelectAll();
        Sync(world);
    }

    private static void ConfirmAmount(World world)
    {
        var state = StateOf(world);
        if (!Native.Get<bool>(world, "_exAmountShown") || Native.Get<CanvasLayer>(world, "_exAmountLayer") is not { } layer) return;
        var hint = Native.Get<Label>(world, "_exAmountHint");
        if (state.Gold)
        {
            var edit = Native.Get<SpinBox>(world, "_exAmountSpin")!.GetLineEdit();
            int max = Native.Get<int>(world, "_exAmountMax");
            if (!int.TryParse(edit.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 1 || value > max)
            {
                if (hint != null) hint.Text = $"Valid quantity: 1–{max:n0}.";
                layer.SetMeta("ex_amount_error", hint?.Text ?? "");
                edit.GrabFocus();
                edit.SelectAll();
                return;
            }
            Native.Call(world, "CloseExchangeAmount");
            state.Gold = false;
            if (Native.Get<LineEdit>(world, "_exGoldEdit") is { } goldEdit) goldEdit.Text = value.ToString(CultureInfo.InvariantCulture);
            Native.Call(world, "OnAddGold");
        }
        else
        {
            Native.Call(world, "ConfirmExchangeAmount");
            if (Native.Get<bool>(world, "_exAmountShown")) layer.SetMeta("ex_amount_error", hint?.Text ?? "");
        }
        Sync(world);
    }

    /// <summary>Offer capacity as the server counts it: repeated countable items share a slot.</summary>
    public static int OfferCount(World world)
    {
        var counted = new HashSet<int>();
        int count = 0;
        foreach (var item in Native.Get<IEnumerable>(world, "_exMyOffer") ?? Array.Empty<object>())
        {
            int id = Native.Get<int>(item!, "ItemId");
            if ((ItemData.Get(id)?.Countable ?? 0) == 0 || counted.Add(id)) count++;
        }
        return count;
    }
}

/// <summary>Runs the trade synchronisation every frame, including while the trade window is hidden.</summary>
public partial class NativeTradeSync : Node
{
    private readonly World? _world;

    public NativeTradeSync() { }

    public NativeTradeSync(World world)
    {
        _world = world;
        ProcessPriority = int.MinValue;
    }

    public override void _Process(double delta)
    {
        if (_world != null && IsInstanceValid(_world)) NativeTrade.Sync(_world);
    }
}
