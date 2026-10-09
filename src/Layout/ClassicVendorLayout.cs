using Godot;
namespace KnightOnlineUiClassic.Layout;

/// <summary>Editable shop geometry on the original transaction artwork; cells keep their native trade ownership.</summary>
public static class ClassicVendorLayout
{
    public static readonly Vector2 Size = new(365, 578);
    public static readonly Rect2 Title = new(53, 16, 258, 24);
    public static readonly Rect2 DragHandle = new(2, 0, 328, 94);
    public static Rect2 Close => new(336, ClassicDesign.Karus ? 18 : 17, 20, 20);
    public static readonly Rect2 Empty = new(98, 181, 140, 22);
    public static readonly Rect2 WalletCaption = new(202, 319, 34, 20), Coin = new(206, 316, 22, 25), WalletValue = new(231, 319, 119, 20);
    public static readonly Rect2 Capacity = new(14, 319, 181, 20);
    public static readonly Rect2 Status = new(14, 293, 336, 16);
    public const int Columns = 6, CellSize = 45, CellPitch = 49, BagColumns = 7, BagSize = 45, BagPitch = 49;
    /// <summary>Catalogue sockets occupy the original painted 6 × 4 grid (x 22–315, y 96–293).</summary>
    public static Rect2 CatalogueCell(int index) => new(23 + index % Columns * CellPitch, 96 + index / Columns * CellPitch, CellSize, CellSize);
    /// <summary>The carried inventory uses the Inventory/Warehouse lower grid.</summary>
    public static Rect2 BagCell(int index) => new(13 + index % BagColumns * BagPitch, 349 + index / BagColumns * BagPitch, BagSize, BagSize);
    public static Rect2 PageLabel(Rect2 up, Rect2 down) => new(up.Position.X, up.End.Y, up.Size.X, down.Position.Y - up.End.Y);
}
