using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Editable geometry of the Classic chat room, the client's group-chat style room window (3277df5). The room
/// follows the whisper and messenger family: the plain Classic frame with the gold title row, the room's door and
/// channel-list actions in Classic cells, the original whisper minimize button and the round close; below the
/// title the members column (a black field with the gold line, Classic list rows, the two leader actions under
/// it), the message log field, the voice row and the composer row, all at the plain frame's even 16 px inset.
/// Minimized, the room is the compact whisper bar.
/// </summary>
public static class ClassicChatRoomLayout
{
    public const float Inset = ClassicFrame.Inset;
    /// <summary>Content top: the title row (14..38) and the 10 px gap the social frame keeps above its content.</summary>
    public const float ContentTop = 48;
    public const float ColumnWidth = 140, ColumnGap = 8;
    public const float LogWidth = 360, LogHeight = 330;
    /// <summary>The voice row and the composer row: one height, one gap, like the Messenger's action row.</summary>
    public const float RowHeight = 24, RowGap = 6;
    /// <summary>Icon buttons (members, settings, deafen, microphone, voice settings, leave, record, send, play) sit in a 24 px Classic cell.</summary>
    public const float Cell = 24;
    public const float HeaderGap = 6;
    public const int FontSize = 12, HeadingSize = 13, LineSize = 13, SmallSize = 10, NoticeSize = 11;
    /// <summary>Whole 22 px member rows, as in the nation chat lists.</summary>
    public const float RosterRow = 22;
    public const float VolumeWidth = 96;
    /// <summary>
    /// The client's emoticon button at the right end of the composer input, at the inline emoticon size so it sits
    /// inside the 24 px field with the cells' margin; the text stops before it.
    /// </summary>
    public const float EmoticonSize = 16, EmoticonInset = 5, EmoticonRoom = EmoticonSize + EmoticonInset * 2;
    /// <summary>The original whisper minimize button (co_whisper_open_us btn_hide).</summary>
    public static readonly Vector2 MinimizeSize = new(19, 15);
    /// <summary>Gap between the original chat check box and its caption in the room's popups.</summary>
    public const int CheckGap = 6;

    // Reactions (fa3cfc3). The client sizes and places the react button, the pill and the popups; the Classic
    // materials keep that geometry, so the popups still open right above (or below) their post.
    /// <summary>The smiley in the post's react cell (the cell itself is the 24 px <see cref="Cell"/>).</summary>
    public const int ReactIcon = 14;
    /// <summary>The pill's padding: the client's, with the 1 px Classic line in place of its 2 px edge.</summary>
    public const float PillPadLeft = 5, PillPadRight = 6, PillPadY = 1;
    public const int PillCountSize = 11;
    /// <summary>The quick bar's padding around its 34 px picks.</summary>
    public const float ReactionBarPadding = 4;
    /// <summary>The details card's padding, and its filter chips' padding.</summary>
    public const float ReactionCardPadX = 10, ReactionCardTop = 8, ReactionCardBottom = 6;
    public const float ReactionChipPadX = 9, ReactionChipPadY = 3;
    /// <summary>The emoticon pickers' padding.</summary>
    public const float PickerPadding = 6;

    public static float Width(bool members) => Inset + (members ? ColumnWidth + ColumnGap : 0) + LogWidth + Inset;
    public static float Height(bool voice) => ContentTop + ContentHeight(voice) + Inset;
    public static float ContentHeight(bool voice) => LogHeight + RowGap + (voice ? RowHeight + RowGap : 0) + RowHeight;
    public static Vector2 Size(bool members, bool voice) => new(Width(members), Height(voice));

    public static Rect2 Close(float width) => ClassicFrame.Close(width);
    public static Rect2 Minimize(float width) =>
        new(Close(width).Position.X - HeaderGap - MinimizeSize.X, 14 + Mathf.Floor((24 - MinimizeSize.Y) / 2), MinimizeSize.X, MinimizeSize.Y);
    /// <summary>Header action cells right to left: index 0 sits next to the minimize button.</summary>
    public static Rect2 HeaderAction(float width, int index) =>
        new(Minimize(width).Position.X - HeaderGap - (index + 1) * Cell - index * HeaderGap, 14, Cell, Cell);
    public static Rect2 Title(float width, int actions)
    {
        float right = actions > 0 ? HeaderAction(width, actions - 1).Position.X : Minimize(width).Position.X;
        return new Rect2(Inset, 14, right - HeaderGap - Inset, 24);
    }
    /// <summary>The title row is the drag handle (the buttons on it are laid over the grip).</summary>
    public static Rect2 Drag(float width) => new(8, 8, width - 56, 36);

    public static Rect2 Content(bool members, bool voice) => new(Inset, ContentTop, Width(members) - Inset * 2, ContentHeight(voice));
    public static float LogLeft(bool members) => Inset + (members ? ColumnWidth + ColumnGap : 0);
    /// <summary>The members column: the roster field over the leader actions, the full content height.</summary>
    public static Rect2 Column(bool voice) => new(Inset, ContentTop, ColumnWidth, ContentHeight(voice));
    public static Rect2 Roster(bool voice) => new(Inset, ContentTop, ColumnWidth, LogHeight);
    public static Rect2 Leader(bool voice) => new(Inset, ContentTop + LogHeight + RowGap, ColumnWidth, ContentHeight(voice) - LogHeight - RowGap);
    public static Rect2 Log(bool members) => new(LogLeft(members), ContentTop, LogWidth, LogHeight);
    public static Rect2 Voice(bool members) => new(LogLeft(members), ContentTop + LogHeight + RowGap, LogWidth, RowHeight);
    public static Rect2 Composer(bool members, bool voice) =>
        new(LogLeft(members), ContentTop + LogHeight + RowGap + (voice ? RowHeight + RowGap : 0), LogWidth, RowHeight);
}
