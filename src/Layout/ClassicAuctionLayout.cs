using Godot;

namespace KnightOnlineUiClassic.Layout;

// The original auction workflow has four pages; all pages share the same content bounds.
public static class ClassicAuctionLayout
{
    // The body ends 16 px above the frame when the refresh cooldown line is shown; otherwise that line is the only reserved band.
    public static readonly Vector2 Size = new(880, 583);
    public static readonly Rect2 Body = new(16, 54, 848, 513);
    public static readonly Rect2 Lots = new(0, 0, 848, 160);
    public static readonly Rect2 Selection = new(0, 168, 848, 30);
    public static Rect2 Section(int column) => new(column * 286, 206, 276, 210);
}
