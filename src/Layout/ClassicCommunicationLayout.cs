using Godot;

namespace KnightOnlineUiClassic.Layout;

// A dedicated Messenger UIF is absent from the inspected exports; reuse the simple service composition.
public static class ClassicCommunicationLayout
{
    // Every row keeps the plain frame's even 16 px inset, the lowest one 16 px above the frame bottom.
    public static Vector2 Size(string id) => new(420, 458);
    // Whole 30 px rows: eight buddies.
    public static Rect2 BuddyList => new(16, 92, 388, 240);
}
