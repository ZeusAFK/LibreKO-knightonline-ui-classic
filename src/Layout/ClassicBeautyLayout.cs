using Godot;
namespace KnightOnlineUiClassic.Layout;

// Free appearance service; reuse the original arrows and simple service actions.
public static class ClassicBeautyLayout
{
    public static readonly Rect2 Name = new(254, 62, 290, 26);
    public static readonly Rect2 Identity = new(254, 90, 290, 22);
    public static readonly Rect2 Editor = new(254, 130, 290, 144);
    // The client's head zoom sits between the turn arrows, centred under the preview.
    public static readonly Rect2 TurnLeft = new(64, 374, 19, 19);
    public static readonly Rect2 Zoom = new(91, 372, 70, 23);
    public static readonly Rect2 TurnRight = new(169, 374, 19, 19);
}

// The hair palette: the client's colour field with the Classic hex row, beside the Beauty Shop.
public static class ClassicHairPaletteLayout
{
    public static readonly Vector2 Size = new(330, 424);
    public static readonly Rect2 Prompt = new(16, 44, 298, 22);
    public static readonly Rect2 Picker = new(16, 72, 298, 291);
    public static readonly Rect2 Accept = new(16, 379, 137, 29);
    public static readonly Rect2 Cancel = new(177, 379, 137, 29);
}
