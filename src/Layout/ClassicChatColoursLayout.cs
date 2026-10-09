using Godot;

namespace KnightOnlineUiClassic.Layout;

// No dedicated colour-editor UIF was identified; use the original service materials with a simple fixed list.
public static class ClassicChatColoursLayout
{
    // Rows and actions keep the original quest-talk insets: 20 px from the sides, 30 px above the frame bottom.
    public static Vector2 Size => new(360, 440);
    public static Rect2 Label(int slot) => new(26, 60 + slot * 28, 226, 24);
    public static Rect2 Pick(int slot) => new(260, 60 + slot * 28, 74, 24);
    public static Rect2 Rows => new(20, 56, 320, 312);
    public static Rect2 Default => new(20, 384, 152, 26);
    public static Rect2 Apply => new(188, 384, 152, 26);
}
