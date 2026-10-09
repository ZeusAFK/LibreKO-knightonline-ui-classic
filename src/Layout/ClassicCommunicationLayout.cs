using Godot;

namespace KnightOnlineUiClassic.Layout;

// Dedicated Messenger and room UIFs are absent from the inspected exports; reuse the simple service composition.
public static class ClassicCommunicationLayout
{
    // Every row keeps the plain frame's even 16 px inset, the lowest one 16 px above the frame bottom.
    public static Vector2 Size(string id) => id == "messenger" ? new(420, 458) : new(460, 560);
    // Whole 30 px rows: eight buddies, five rooms.
    public static Rect2 BuddyList => new(16, 92, 388, 240);
    public static Rect2 RoomList => new(16, 92, 428, 150);
    public static Rect2 RoomLog => new(16, 338, 428, 164);
}
