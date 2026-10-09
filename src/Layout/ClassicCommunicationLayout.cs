using Godot;

namespace KnightOnlineUiClassic.Layout;

// Dedicated Messenger and room UIFs are absent from the inspected exports; reuse the simple service composition.
public static class ClassicCommunicationLayout
{
    // Bottom rows keep the original quest frames' ~30 px clearance above the corner curls.
    public static Vector2 Size(string id) => id == "messenger" ? new(420, 472) : new(460, 574);
    // Whole 30 px rows: eight buddies, five rooms.
    public static Rect2 BuddyList => new(16, 92, 388, 240);
    public static Rect2 RoomList => new(16, 92, 428, 150);
    public static Rect2 RoomLog => new(16, 338, 428, 164);
}
