using Godot;
namespace KnightOnlineUiClassic.Layout;

public static class ClassicMarketPriceLayout
{
    // Every row keeps the plain frame's even 16 px inset, the lowest one 16 px above the frame bottom.
    public static readonly Vector2 Size = new(840, 560);
    public static readonly Rect2 Results = new(16, 240, 316, 194);
    public static readonly Rect2 Chart = new(344, 78, 480, 372);
    public static readonly Rect2 Selected = new(16, 450, 46, 46);
    public static readonly Rect2 Status = new(16, 518, 700, 26);
    public static readonly Rect2 Cancel = new(728, 518, 96, 26);
}
