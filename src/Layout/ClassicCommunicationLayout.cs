using Godot;

namespace KnightOnlineUiClassic.Layout;

// Dedicated Messenger and room UIFs are absent from the inspected exports; reuse the simple service composition.
public static class ClassicCommunicationLayout
{
    // The lowest row keeps the original quest-talk insets: 20 px from the sides, 30 px above the frame bottom.
    public static Vector2 Size(string id) => id == "messenger" ? new(420, 472) : new(460, 574);
    // Whole 30 px rows: eight buddies, five rooms.
    public static Rect2 BuddyList => new(16, 92, 388, 240);
    public static Rect2 RoomList => new(16, 92, 428, 150);
    public static Rect2 RoomLog => new(16, 338, 428, 164);
}
