using Godot;

namespace KnightOnlineUiClassic.Layout;

// Live service controls use one-pixel coordinates and the inventory's 49-pixel pitch.
public static class ClassicServiceLayout
{
    public static Vector2 Size(string id) => id switch
    {
        "warp" => new(320, 482),
        "repair" => new(366, 488),
        // The lowest row keeps the 14 px content margin to the plain frame's bottom edge.
        "seal" => new(366, 532),
        "piecechange" => new(366, 516),
        "itemcombine" => new(366, 416),
        "combinerecipes" => new(790, 481),
        // The redistribution menu closes 20 px under its last option, as far as the description sits from the top.
        "class_change" => new(327, 207),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    public static Rect2 Cell(int index, int columns, int x, int y) => new(x + index % columns * 49, y + index / columns * 49, 45, 45);
}
