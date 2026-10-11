using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Editable geometry of the Inn Hostess's opening window after the original co_inn_us (327 × 240, message art,
/// white greeting, yellow text choices on a 31–32 px pitch). The client offers more choices than the original
/// three, so the art's stretched middle strip grows by the extra rows and the lower band moves down with it.
/// </summary>
public static class ClassicInnLayout
{
    public const string Art = "co_inn_us";
    public const int Width = 327, BaseHeight = 240;
    /// <summary>The art's middle strip (y 64–176) stretches; pieces from <see cref="StretchEnd"/> down move.</summary>
    public const int StretchStart = 64, StretchEnd = 176;
    /// <summary>Greeting (Text_Title).</summary>
    public static readonly Rect2 Greeting = new(10, 26, 310, 61);
    /// <summary>First choice row (btn_warehouse at y 105) and the original rows' pitch (105, 136, 168).</summary>
    public const int FirstRow = 105, RowHeight = 21, OriginalRows = 3;
    public const float RowPitch = 31.5f;
    /// <summary>Rows keep the original left edge (14) and span the frame with the same right margin.</summary>
    public const int RowX = 14, RowWidth = Width - RowX * 2;
    /// <summary>Caption inset inside a row: Text_Trade starts at x 20.</summary>
    public const int CaptionInset = 6;
    /// <summary>Space under the last row: 240 − (168 + 21).</summary>
    public const int BottomMargin = 51;

    public static Rect2 Choice(int row) => new(RowX, FirstRow + (int)(row * RowPitch), RowWidth, RowHeight);

    public static int Height(int rows) => Math.Max(BaseHeight, (int)Choice(Math.Max(rows, 1) - 1).End.Y + BottomMargin);

    /// <summary>Where an art piece of the original layout is drawn when the window is <paramref name="height"/> tall.</summary>
    public static Rect2 Piece(LayoutNode image, int height)
    {
        int extra = height - BaseHeight;
        var rect = new Rect2(image.X, image.Y, image.W, image.H);
        if (image.Y >= StretchEnd) rect.Position += new Vector2(0, extra);
        else if (image.Y >= StretchStart && image.Y + image.H == StretchEnd) rect.Size += new Vector2(0, extra);
        return rect;
    }

    /// <summary>
    /// Choice ink besides the original yellow: white under the pointer, frame gold while pressed, and the grey the
    /// sibling co_smith_us gives an unavailable choice.
    /// </summary>
    public static readonly Color Pressed = new("f2ce75"), Disabled = new("808080");

    /// <summary>The client's Close choice reads as the leave row of the sibling originals (co_smith_us, co_change_us).</summary>
    public const string LeaveCaption = "Walk away";
    public static string Caption(string client) => client == "Close" ? LeaveCaption : client;
}
