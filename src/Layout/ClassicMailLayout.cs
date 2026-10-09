using Godot;

namespace KnightOnlineUiClassic.Layout;

// No compatible mail UIF is present in the exported reference set. Reuse the simple service composition.
public static class ClassicMailLayout
{
    // The lowest row keeps the original quest-talk insets: 20 px from the sides, 30 px above the frame bottom.
    public static Vector2 Size(string id) => new(420, id == "mail" ? 406 : id == "mailread" ? 531 : 578);
    public static Rect2 Action(int index, float y) => new(20 + index * 198, y, 182, 26);
    // Nine whole 26 px inbox rows.
    public static Rect2 Inbox => new(16, 112, 388, 234);
    public static Rect2 Message => new(16, 124, 388, 146);
    // Four whole 35 px attachment rows.
    public static Rect2 ReadAttachments => new(16, 318, 388, 137);
    public static Rect2 DraftMessage => new(16, 148, 388, 116);
    public static Rect2 DraftAttachments => new(16, 338, 388, 142);
}
