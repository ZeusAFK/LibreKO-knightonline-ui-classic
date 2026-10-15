using Godot;
using LibreKO.Domain;
using LibreKO.Plugins;
using KnightOnlineUiClassic.Layout;

namespace KnightOnlineUiClassic.Windows;

// Repair uses the existing inventory; only the original cost tooltip is added.
public partial class ClassicInventoryRepair : Control
{
    /// <summary>The original tooltip's price colour when the player cannot pay (0xffdd0000).</summary>
    public static readonly Color LackColor = new("dd0000");
    /// <summary>Gap between the pointer and the tooltip on the right (original CUIRepairTooltipDlg).</summary>
    private const int PointerGap = 26;

    private readonly WindowHost _host;
    private readonly PluginGame _game;
    private readonly Control _tip;
    private readonly Label _title, _maximum, _current, _cost;
    private readonly Color _costColor;
    private readonly Control _header;
    private readonly Button _all;
    private readonly Action _cancelCarry;
    private bool _active;
    private int _slot = -1;
    public ClassicInventoryRepair(WindowHost host, PluginGame game, Control view, Action cancelCarry)
    {
        _host = host; _game = game; _cancelCarry = cancelCarry; MouseFilter = MouseFilterEnum.Ignore;
        _header = new Control { Visible = false, MouseFilter = MouseFilterEnum.Ignore }; view.AddChild(_header);
        var heading = new Label { Text = "Repair", Position = new Vector2(16, 9), Size = new Vector2(160, 24), MouseFilter = MouseFilterEnum.Ignore };
        heading.AddThemeFontOverride("font", Plugin.Kit.Bold); heading.AddThemeFontSizeOverride("font_size", 14);
        heading.AddThemeColorOverride("font_color", new Color("e2c68b")); _header.AddChild(heading);
        _all = new Button { Text = "Repair All", FocusMode = FocusModeEnum.None };
        _header.AddChild(_all);
        ClassicMerchantSkin.Button(_all, Plugin.Kit.Layout("co_change_bill_us").Find("btn_ok")!, _header, new Rect2(213, 8, 110, 25), "Repair All");
        _all.Pressed += () => { if (_host.Window.HasMeta("classic_repair_all")) _host.Window.GetMeta("classic_repair_all").AsCallable().Call(); };
        var layer = new CanvasLayer { Layer = 120 }; AddChild(layer);
        _tip = new Control { Size = new Vector2(223, 98), Visible = false, MouseFilter = MouseFilterEnum.Ignore }; layer.AddChild(_tip);
        var art = Plugin.Kit.Layout("co_tooltop_repair_us");
        foreach (var image in art.Children.Where(n => n.IsImage))
            _tip.AddChild(new TextureRect { Texture = ClassicChatControls.Atlas(image), Position = image.Position, Size = image.SizeVec,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore });
        Label Field(string id)
        {
            var node = art.Find(id)!;
            var label = new Label { Position = node.Position, Size = node.SizeVec, MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            label.AddThemeFontOverride("font", Plugin.Kit.FontFor(node)); label.AddThemeFontSizeOverride("font_size", UiKit.FontSize(node));
            label.AddThemeColorOverride("font_color", node.Color); _tip.AddChild(label); return label;
        }
        _title = Field("string_title"); _maximum = Field("string_dur_max");
        _current = Field("string_dur_current"); _cost = Field("string_repairgold");
        _costColor = art.Find("string_repairgold")!.Color;
    }
    /// <summary>Where the tooltip follows the pointer from; the preview substitutes a fixed point.</summary>
    public Func<Vector2>? Pointer { get; set; }
    /// <summary>The tooltip, for bounds checks.</summary>
    public Control Tip => _tip;
    /// <summary>Repairs exactly the clicked item through the repair mode's own action when it has one.</summary>
    public void Take(int slot)
    {
        if (_host.Window.HasMeta("classic_repair_take")) _host.Window.GetMeta("classic_repair_take").AsCallable().Call(slot);
        else _game.Inventory.Use(slot);
    }
    public void ShowTip(int slot) { _slot = slot; UpdateTip(); }
    public void HideTip() { _slot = -1; _tip.Visible = false; }
    public override void _Process(double delta)
    {
        bool active = _host.Window.IsVisibleInTree() && _host.Window.GetMeta(HotNames.ClassicRepairMode, false).AsBool();
        if (active && !_active) _cancelCarry();
        _active = active;
        _header.Visible = active;
        if (!active) { HideTip(); return; }
        _all.Disabled = _host.Window.GetMeta(HotNames.ClassicRepairPending, false).AsBool() || !Enumerable.Range(0, _game.Inventory.GridStart + _game.Inventory.GridCount)
            .Select(_game.Inventory.At).Any(item => !item.IsEmpty && ItemData.MaxDurabilityOf(item.ItemId) > 1
                && item.Durability < ItemData.MaxDurabilityOf(item.ItemId) && ItemData.Get(item.ItemId)?.SaleType != ItemData.SaleTypeLowNoRepair);
        if (_slot >= 0) UpdateTip();
    }
    private void UpdateTip()
    {
        var item = _game.Inventory.At(_slot);
        if (item.IsEmpty) { HideTip(); return; }
        int maximum = ItemData.MaxDurabilityOf(item.ItemId);
        bool valid = _slot < _game.Inventory.GridStart + _game.Inventory.GridCount
            && maximum > 1 && ItemData.Get(item.ItemId)?.SaleType != ItemData.SaleTypeLowNoRepair;
        int cost = valid ? RepairPrice.Cost(ItemData.BuyPrice(item.ItemId), maximum, item.Durability) : 0;
        _title.Text = valid ? "Repair Cost" : "Cannot repair";
        _maximum.Text = "Maximum Durability : " + maximum.ToString("n0");
        _current.Text = "Current Durability : " + item.Durability.ToString("n0");
        _cost.Text = cost.ToString("n0") + " Noahs";
        _cost.AddThemeColorOverride("font_color", cost > _game.Character.Gold ? LackColor : _costColor);
        _maximum.Visible = _current.Visible = _cost.Visible = valid;
        _tip.Position = Place(Pointer?.Invoke() ?? GetViewport().GetMousePosition(), _tip.Size, GetViewportRect().Size);
        _tip.Visible = true;
    }
    /// <summary>
    /// The original placement: above and to the right of the pointer; flipped to the left near the
    /// right edge and below the pointer near the top edge.
    /// </summary>
    public static Vector2 Place(Vector2 pointer, Vector2 size, Vector2 bounds)
    {
        bool right = pointer.X + size.X < bounds.X, above = pointer.Y - size.Y > 0;
        var at = new Vector2(right ? pointer.X + PointerGap : pointer.X - size.X, above ? pointer.Y - size.Y : pointer.Y);
        return new Vector2(Mathf.Clamp(at.X, 0, bounds.X - size.X), Mathf.Clamp(at.Y, 0, bounds.Y - size.Y)).Round();
    }
}

/// <summary>Godot names used every frame in this file, created once (a literal creates a finalizable wrapper per call).</summary>
file static class HotNames
{
    internal static readonly StringName ClassicRepairMode = "classic_repair_mode";
    internal static readonly StringName ClassicRepairPending = "classic_repair_pending";
}
