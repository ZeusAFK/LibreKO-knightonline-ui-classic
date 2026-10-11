using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Editable geometry and type of the event, ranking and war windows that keep the client's own layout:
/// the plain Classic frame with the event-window margins (<see cref="ClassicEventsLayout"/>), the gold title on
/// the content edge, the close knob 14 pixels from the right edge, 22-pixel rows, 45-pixel item cells and the
/// service buttons. The client's containers keep arranging their rows; these values set the frame around them,
/// the minimum sizes of rows and cells and the widths a window needs to read as an original window.
/// </summary>
public static class ClassicEventFamilyLayout
{
    public const int Margin = ClassicEventsLayout.Margin, TitleTop = 14, TitleHeight = 24, ContentGap = 10;
    public const int Row = 22, Button = 24, Cell = 45, BodyGap = 8;
    public const int FontSize = 13, SmallFont = 12, TinyFont = 11;

    /// <summary>Insets of the client's content margin inside the frame: content starts 48 pixels down, as in the event windows.</summary>
    public static readonly (int Left, int Top, int Right, int Bottom) Content = (Margin, ContentGap, Margin, Margin);

    /// <summary>The title strip: title on the content edge, close knob at the right margin, minimize beside it.</summary>
    public static readonly (int Left, int Top, int Right) Header = (Margin, TitleTop, Margin);
    public static readonly Vector2 Close = new(24, 24), Minimize = new(19, 12);

    /// <summary>Body width per window, where the client's natural width is too narrow or too wide for its rows.</summary>
    public static int BodyWidth(string id) => id switch
    {
        "events" => 548,
        "daily_rank" => 0,
        "rank_board" => 0,
        "nestindun" => 0,
        _ => 0,
    };
}
