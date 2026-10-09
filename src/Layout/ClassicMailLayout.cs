using Godot;

namespace KnightOnlineUiClassic.Layout;

// No compatible mail UIF is present in the exported reference set. Reuse the simple service composition.
public static class ClassicMailLayout
{
    // Bottom actions keep the original quest frames' ~30 px clearance above the corner curls.
    public static Vector2 Size(string id) => new(420, id == "mail" ? 400 : id == "mailread" ? 531 : 578);
    // Nine whole 26 px inbox rows.
    public static Rect2 Inbox => new(16, 112, 388, 234);
    public static Rect2 Message => new(16, 124, 388, 146);
    // Four whole 35 px attachment rows.
    public static Rect2 ReadAttachments => new(16, 318, 388, 137);
    public static Rect2 DraftMessage => new(16, 148, 388, 116);
    public static Rect2 DraftAttachments => new(16, 338, 388, 142);
}
