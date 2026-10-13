using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// The client's mouse-wheel stack split on a Classic drag. While a stack is dragged the wheel picks how many move
/// (the client's <see cref="StackDial"/> steps), the count on the drag icon shows it in the cell's count type (white
/// for the whole stack, gold for a part), and the drag data carries it under <see cref="DragLayer.AmountKey"/>, as
/// the client's own cells publish it. The label is a child of the drag preview, so it ends with the drag.
/// </summary>
public partial class ClassicStackDial : Label
{
    /// <summary>The colour of a part of the stack, as the client's own drag count shows it.</summary>
    public static readonly Color PartColour = UiTheme.GoldBright;
    private const double MsecPerSecond = 1000.0;

    private readonly StackDial _dial;
    private readonly Godot.Collections.Dictionary _data;

    private ClassicStackDial(StackDial dial, Godot.Collections.Dictionary data)
    {
        _dial = dial;
        _data = data;
        Name = "stack_dial";
    }

    public int Amount => _dial.Amount;
    public bool Whole => _dial.Whole;

    /// <summary>
    /// Arms the dial on a drag preview made by <see cref="IconDragPreview"/> when the dragged item is a stack;
    /// <paramref name="count"/> is the cell's count label, whose font and placement the drag count repeats.
    /// </summary>
    public static ClassicStackDial? Arm(Control preview, Godot.Collections.Dictionary data, GameItem item, Label count)
    {
        var def = ItemData.Get(item.ItemId);
        if (Platform.TouchUi || def is null or { IsChargeItem: true } || !StackDial.Applies(def.Countable, item.Count)) return null;
        if (!preview.IsInsideTree() || preview.GetChildCount() == 0 || preview.GetChild(0) is not Control icon) return null;
        var dial = new ClassicStackDial(new StackDial(item.Count), data);
        icon.AddChild(dial);
        ItemCountStyle.Apply(dial, icon, count.GetThemeFont("font"));
        // The preview icon is the cell's inset icon; the cell count sits on the icon's bottom-right corner.
        dial.OffsetRight = count.OffsetRight + ItemCountStyle.Inset;
        dial.OffsetBottom = count.OffsetBottom + ItemCountStyle.Inset;
        dial.Publish();
        return dial;
    }

    /// <summary>
    /// Gives the count the client draws on a native cell's drag icon (<see cref="ItemSlotView.PicksAmount"/>) the cell's
    /// Classic count type and corner; the client keeps colouring it as the wheel turns. Called from the cell's drag-out
    /// callback: the client builds the drag icon once that returns, so the count is styled one deferred call later.
    /// </summary>
    public static void StyleNativeCount(ItemSlotView cell)
    {
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(cell) || !cell.IsInsideTree() || !cell.GetViewport().GuiIsDragging()) return;
            if (cell.GetTree().Root.GetNodeOrNull<CanvasLayer>("DragLayer") is not { } layer || layer.GetChildCount() == 0
                || layer.GetChild(0) is not { } root || root.GetChildCount() == 0) return;
            var count = KnightOnlineUiClassic.Native.Native.Descendants(root.GetChild(root.GetChildCount() - 1))
                .OfType<Label>().LastOrDefault(l => l is not UpgradeBadge && l.GetParent() is TextureRect);
            if (count?.GetParent() is not TextureRect icon || cell.Size.Y <= 0) return;
            // The square item art is drawn centred in the icon rect; a GM build widens the rect later for the item id
            // under it, so the count keeps to the centred square.
            float side = icon.Size.Y, scale = side / cell.Size.Y;
            var colour = count.GetThemeColor("font_color");
            ItemCountStyle.Apply(count, icon, cell.CountLabel.GetThemeFont("font"));
            count.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(ItemCountStyle.FontSize * scale));
            count.AnchorLeft = count.AnchorRight = 0.5f;
            count.OffsetLeft = -side / 2;
            count.OffsetRight = side / 2 - ItemCountStyle.Inset * scale;
            count.OffsetBottom = -ItemCountStyle.Inset * scale;
            count.AddThemeColorOverride("font_color", colour);
        }).CallDeferred();
    }

    /// <summary>The amount a drop carries: the wheel's pick, or null when the drag had no dial.</summary>
    public static int? Picked(Variant data) =>
        data.VariantType == Variant.Type.Dictionary && data.AsGodotDictionary().TryGetValue(DragLayer.AmountKey, out var amount)
            ? amount.AsInt32() : null;

    /// <summary>A picked part of the stack (less than <paramref name="count"/>), or null for the whole stack.</summary>
    public static int? Part(int? picked, int count) => picked is int part && part > 0 && part < count ? part : null;

    public override void _Input(InputEvent ev)
    {
        if (ev is not InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel) return;
        if (!GetViewport().GuiIsDragging()) return;
        // The dial wins over the cells under the pointer (storage cells turn their page on the wheel).
        GetViewport().SetInputAsHandled();
        if (!wheel.Pressed) return;
        _dial.Turn(wheel.ButtonIndex == MouseButton.WheelUp ? StackDial.Up : StackDial.Down, Time.GetTicksMsec() / MsecPerSecond);
        Publish();
    }

    private void Publish()
    {
        _data[DragLayer.AmountKey] = _dial.Amount;
        Text = _dial.Amount.ToString();
        AddThemeColorOverride("font_color", _dial.Whole ? Colors.White : PartColour);
    }
}
