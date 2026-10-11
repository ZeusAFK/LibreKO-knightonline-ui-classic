using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Editable geometry of an original NPC opening window: the 327 × 240 message art (co_inn_us, co_smith_us,
/// co_change_us), a white greeting and the choices as plain text rows. The art's middle strip (y 64–176)
/// stretches, so the window grows or shrinks by whole rows when the client offers a different number of
/// choices, and by whole lines when the client's greeting needs more room than the original string.
/// </summary>
public sealed record ClassicNpcOpeningLayout(
    string Art,
    // The greeting (Text_Title).
    Rect2 Greeting,
    // The layout string whose face, size and yellow ink the choice rows use.
    string RowInk,
    // Left edge of the original buttons; rows span the frame with the same margin on the right.
    int RowX,
    // Caption inset inside a row (the original strings start this far right of their buttons).
    int CaptionInset,
    int FirstRow,
    float RowPitch,
    int RowHeight,
    // The original window's choices, the leave row included.
    int OriginalRows,
    // Space under the last row in the original window.
    int BottomMargin)
{
    public const int Width = 327, BaseHeight = 240;
    /// <summary>The art's middle strip stretches; pieces from <see cref="StretchEnd"/> down move with it.</summary>
    public const int StretchStart = 64, StretchEnd = 176;

    /// <summary>Inn Hostess (co_inn_us): btn_warehouse 14, 105; rows on a 31/32 px pitch; 51 px under btn_sale.</summary>
    public static readonly ClassicNpcOpeningLayout Inn = new("co_inn_us", new Rect2(10, 26, 310, 61), "Text_Trade",
        RowX: 14, CaptionInset: 6, FirstRow: 105, RowPitch: 31.5f, RowHeight: 21, OriginalRows: 3, BottomMargin: 51);

    /// <summary>
    /// Blacksmith and the other repairing merchants (co_smith_us): buttons from x 38 at y 98, 125/127, 153/155 and
    /// 180, captions 6 px in, Arial 10 pt bold; 39 px under "Walk away." (btn_close 38, 180, 20 px + 1).
    /// </summary>
    public static readonly ClassicNpcOpeningLayout Smith = new("co_smith_us", new Rect2(22, 24, 287, 47), "Text_Trade",
        RowX: 38, CaptionInset: 6, FirstRow: 98, RowPitch: 27.5f, RowHeight: 21, OriginalRows: 4, BottomMargin: 39);

    /// <summary>
    /// Stat and skill point redistribution (co_change_us): Btn_repoint0/1 and btn_close at x 16, y 99/131/162,
    /// captions at x 20, Arial 10 pt; 57 px under the leave row.
    /// </summary>
    public static readonly ClassicNpcOpeningLayout Change = new("co_change_us", new Rect2(15, 20, 293, 44), "Text_repoint0",
        RowX: 16, CaptionInset: 4, FirstRow: 99, RowPitch: 31.5f, RowHeight: 21, OriginalRows: 3, BottomMargin: 57);

    public int RowWidth => Width - RowX * 2;

    /// <summary>Row <paramref name="row"/>; <paramref name="shift"/> is how far a longer greeting pushes the rows down.</summary>
    public Rect2 Choice(int row, int shift = 0) => new(RowX, FirstRow + shift + (int)(row * RowPitch), RowWidth, RowHeight);

    public int Height(int rows, int shift = 0) => (int)Choice(Math.Max(rows, 1) - 1, shift).End.Y + BottomMargin;

    /// <summary>The greeting rectangle grown by the lines the client's greeting needs beyond the original string's.</summary>
    public Rect2 GreetingRect(int shift = 0) => Greeting with { Size = Greeting.Size + new Vector2(0, shift) };

    /// <summary>How far the rows move down so a greeting of <paramref name="height"/> pixels keeps the original gap.</summary>
    public int Shift(float height) => Math.Max(0, Mathf.CeilToInt(height - Greeting.Size.Y));

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
    /// Choice ink besides the original yellow: white under the pointer, frame gold while pressed, and the grey
    /// co_smith_us gives an unavailable choice.
    /// </summary>
    public static readonly Color Pressed = new("f2ce75"), Disabled = new("808080");

    /// <summary>The client's Close choice reads as the leave row of the originals (co_smith_us, co_change_us).</summary>
    public const string LeaveCaption = "Walk away";
    public static string Caption(string client) => client == "Close" ? LeaveCaption : client;
}
