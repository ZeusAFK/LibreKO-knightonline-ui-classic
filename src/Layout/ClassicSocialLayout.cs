using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Editable geometry of the Classic social windows that the client builds as flow layouts (nation chat,
/// clan and join-request boards, leader transfer, commander). They share the plain Classic
/// frame: the gold title and round close button on the title row, the content 16 px from the sides and
/// the bottom, and the original action buttons, edit fields, sections and list rows of the Messenger.
/// </summary>
public static class ClassicSocialLayout
{
    /// <summary>Windows drawn in the Classic social frame.</summary>
    public static readonly string[] WindowIds =
    {
        "nation_chat", "nation_chat_create", "nation_chat_password", "nation_chat_search", "nation_chat_members",
        "commander", "clan_board", "clan_join_list", "clan_message", "clanhandover",
        "wish_board", "wish_message", "wish_requests",
    };

    /// <summary>Content inset from the left, right and bottom edges (the plain frame's even padding).</summary>
    public const float Inset = ClassicFrame.Inset;
    /// <summary>Content top: the title row (14..38) and the same 10 px gap the Messenger keeps above its captions.</summary>
    public const float ContentTop = 48;
    /// <summary>Narrowest frame, so the title and the close button never collide.</summary>
    public const float MinimumWidth = 220;

    /// <summary>Original action buttons (Messenger "Whisper", Chat Rooms "Join"): one height everywhere.</summary>
    public const float ActionHeight = 24;
    public const float InputHeight = 24;
    /// <summary>Caption room inside an action button, left and right.</summary>
    public const float ButtonPadding = 10;
    public const int FontSize = 12, HeadingSize = 13, NameSize = 14;
    /// <summary>Spacing inside the flow layouts: rows of controls and controls within a row.</summary>
    public const int RowGap = 8, ControlGap = 6;

    /// <summary>Gold window title on the title row, left of the close button.</summary>
    public static Rect2 Title(float width) => ClassicFrame.Title(width);
    public static Rect2 Close(float width) => ClassicFrame.Close(width);
    /// <summary>The title row is the drag handle (the close button excluded).</summary>
    public static Rect2 Drag(float width) => new(8, 8, width - 56, 36);
    public static Rect2 Content(Vector2 size) => new(Inset, ContentTop, size.X - Inset * 2, size.Y - ContentTop - Inset);
    public static Vector2 Frame(Vector2 content) =>
        new(Mathf.Max(MinimumWidth, Mathf.Ceil(content.X + Inset * 2)), Mathf.Ceil(content.Y + ContentTop + Inset));

    /// <summary>Player menu: the Classic menu panel, one 22 px row per action under the player's name.</summary>
    public const float MenuWidth = 150, MenuRow = 22, MenuInset = 6;
    public const int MenuPadding = 4;

    /// <summary>
    /// Text prompt on the original message box: title band, the message at its wrapped height, the original
    /// edit field (Edit_Common, 173 x 23) 8 px under it, then the OK and Cancel art with the common gaps.
    /// </summary>
    public const float PromptLine = 18;
    public static Rect2 PromptText(float height) => ClassicOverlayLayout.Message.TextAt(ClassicOverlayLayout.Message.TextTop, height);
    public static Rect2 PromptEdit(float text) => new(77, ClassicOverlayLayout.Message.TextTop + text + 8, 173, 23);
    public static Vector2 PromptSize(float text) =>
        new(ClassicOverlayLayout.Message.Width, ClassicOverlayLayout.Message.HeightFor(PromptEdit(text).End.Y - ClassicOverlayLayout.Message.TextTop));
}
