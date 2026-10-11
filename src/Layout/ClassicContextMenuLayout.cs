using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Geometry of the Classic context menu (player, loot item, party member, clan member and character page menus):
/// the Classic menu panel with 4 px padding, an optional title row and its gold rule, then one 22 px row per
/// action. The menu is at least 150 px wide and grows to the widest caption up to <see cref="MaxWidth"/>.
/// </summary>
public static class ClassicContextMenuLayout
{
    public const float MinWidth = 150, MaxWidth = 280, Row = 22, Inset = 6, RuleHeight = 4, SeparatorHeight = 7;
    public const int Padding = 4, FontSize = 12, TitleSize = 13;
    /// <summary>Gap between the cursor and the menu's top-left corner.</summary>
    public static readonly Vector2 CursorGap = new(2, 2);
    /// <summary>The canvas layer of an open menu: above the HUD, windows and the loot box.</summary>
    public const int Layer = 90;

    /// <summary>Row width for the widest text: the caption and both insets, kept between the minimum and maximum.</summary>
    public static float Width(float widestText) => Mathf.Clamp(Mathf.Ceil(widestText + Inset * 2), MinWidth, MaxWidth);

    /// <summary>Top-left corner of a menu of <paramref name="size"/> opened at the cursor, kept inside the room.</summary>
    public static Vector2 Place(Vector2 cursor, Vector2 size, Vector2 room)
    {
        var at = cursor + CursorGap;
        // Like the client's menus: open on the other side of the cursor when the menu would leave the screen.
        if (at.X + size.X > room.X) at.X = cursor.X - CursorGap.X - size.X;
        if (at.Y + size.Y > room.Y) at.Y = cursor.Y - CursorGap.Y - size.Y;
        at.X = Mathf.Clamp(at.X, 0, Mathf.Max(0, room.X - size.X));
        at.Y = Mathf.Clamp(at.Y, 0, Mathf.Max(0, room.Y - size.Y));
        return at.Round();
    }
}
