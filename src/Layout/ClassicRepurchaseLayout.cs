using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Declarative geometry of the Classic buy-back window (Kaishan's "Item repurchase") in the plain frame family:
/// the title row, one gold-lined list inset with the column captions and ten whole rows (stored item, buy-back
/// price, deletion time, Buy) beside the row scroll bar, and the Refresh button under it. Content keeps the
/// frame's 16 px inset on every side; rows are 28 px so the service buttons (22 px) sit centred in them.
/// </summary>
public static class ClassicRepurchaseLayout
{
    public const float Inset = ClassicFrame.Inset;
    public const int Rows = 10;
    public const float Width = 460, ListTop = 48, HeaderHeight = 22, RowHeight = 28, ButtonHeight = 27;
    public const float RowsTop = ListTop + 4 + HeaderHeight + 2;
    public const float ListHeight = RowsTop - ListTop + Rows * RowHeight + 4;
    public static readonly Vector2 Size = new(Width, ListTop + ListHeight + 10 + ButtonHeight + Inset);

    /// <summary>The gold-lined inset around the captions and the rows.</summary>
    public static readonly Rect2 List = new(Inset, ListTop, Width - Inset * 2, ListHeight);

    /// <summary>Column x and width: stored item, price (right-aligned), deletion time (centred), Buy.</summary>
    public static readonly (float X, float Width)[] Columns = { (24, 148), (176, 108), (288, 78), (370, 54) };
    public static Rect2 Heading(int column) => new(Columns[column].X, ListTop + 4, Columns[column].Width, HeaderHeight);
    public static Rect2 Cell(int column, int row) => new(Columns[column].X, RowsTop + row * RowHeight, Columns[column].Width, RowHeight);
    public static Rect2 Buy(int row) => new(Columns[3].X, RowsTop + row * RowHeight + 3, Columns[3].Width, 22);
    /// <summary>The rule under the captions and the rules between rows.</summary>
    public static Rect2 Rule(int row) => new(Inset + 4, RowsTop + row * RowHeight - 1, Width - Inset * 2 - 8, 1);

    public static readonly Rect2 Scroll = new(Width - Inset - 4 - 12, RowsTop, 12, Rows * RowHeight);
    public static readonly Rect2 Empty = new(Columns[0].X, RowsTop, Columns[3].X + Columns[3].Width - Columns[0].X, Rows * RowHeight);
    public static readonly Rect2 Refresh = new(Width - Inset - 120, ListTop + ListHeight + 10, 120, ButtonHeight);
}
