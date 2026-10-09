using Godot;
namespace KnightOnlineUiClassic.Layout;

public static class ClassicMarketPriceLayout
{
    // The lowest row keeps the original quest-talk insets: 20 px from the sides, 30 px above the frame bottom.
    public static readonly Vector2 Size = new(840, 574);
    public static readonly Rect2 Results = new(16, 240, 316, 194);
    public static readonly Rect2 Chart = new(344, 78, 480, 372);
    public static readonly Rect2 Selected = new(16, 450, 46, 46);
    public static readonly Rect2 Status = new(20, 518, 692, 26);
    public static readonly Rect2 Cancel = new(724, 518, 96, 26);
}
