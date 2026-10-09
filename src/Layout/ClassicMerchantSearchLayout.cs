using Godot;

namespace KnightOnlineUiClassic.Layout;

public static class ClassicMerchantSearchLayout
{
    // The lowest row keeps the original quest-talk insets: 20 px from the sides, 30 px above the frame bottom.
    public static readonly Vector2 Size = new(780, 536);
    public static readonly Rect2 List = new(16, 160, 748, 300);
    public static Rect2 Row(int index, int column) => column switch
    {
        // Row controls stay 4 px inside the list border.
        0 => new Rect2(20, 162 + index * 30, 78, 26),
        1 => new Rect2(104, 162 + index * 30, 96, 26),
        2 => new Rect2(208, 162 + index * 30, 80, 26),
        3 => new Rect2(296, 163 + index * 30, 24, 24),
        4 => new Rect2(328, 162 + index * 30, 268, 26),
        _ => new Rect2(604, 162 + index * 30, 156, 26),
    };
}
