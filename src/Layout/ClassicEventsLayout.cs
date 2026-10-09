using Godot;

namespace KnightOnlineUiClassic.Layout;

// Event windows without an original UIF: the quest frame, inventory cells, quest insets and service
// buttons composed with the same 14-pixel margins, 22-pixel rows and 49-pixel cell pitch as the
// reviewed Classic service windows. Content ends 30 pixels above the frame's lower edge, clear of the
// quest frame's bottom corner curls (they reach 16 pixels in from the sides up to that height).
public static class ClassicEventsLayout
{
    public const int Margin = 14, Row = 24, CellPitch = 49, CardHeight = 42, CardGap = 6, Bottom = 30;
    public static readonly Vector2 Cell = new(45, 45);
    public static Rect2 Title(float width) => new(22, 14, width - 80, 24);
    public static Rect2 Close(float width) => new(width - 38, 14, 24, 24);
    public static Rect2 Minimize(float width, bool closable) => new(width - (closable ? 66 : 40), 20, 19, 12);
    public static Rect2 Grip(float width) => new(14, 8, width - (width > 200 ? 84 : 50), 32);

    // Minimized windows keep the original quest title plate.
    public static readonly Vector2 Minimized = new(232, 32);
    public static readonly Rect2 MinimizedTitle = new(18, 4, 170, 24);
    public static Rect2 MinimizedToggle(bool closable) => new(closable ? 172 : 198, 10, 19, 12);
    public static readonly Rect2 MinimizedClose = new(200, 6, 20, 20);
    public static readonly Rect2 MinimizedGrip = new(8, 2, 160, 28);

    public static class CollectionRace
    {
        public const float Width = 330, MaxHeight = 520, Top = 48, Bottom = ClassicEventsLayout.Bottom;
        public static readonly Rect2 Content = new(Margin, Top, Width - Margin * 2, 0);
        public const int InfoCaption = 150;
    }

    public static class Lottery
    {
        // The frame ends below the prize area, which fits its cells (plus a scroll bar when they overflow) or a
        // whole number of winner rows.
        public const float Width = 344, Bottom = ClassicEventsLayout.Bottom, PrizeCells = 52, PrizeBar = 14;
        public const int WinnerPitch = 26, WinnerRows = 4;
        public static readonly Vector2 Size = new(Width, 324 + PrizeCells + Bottom);
        public static readonly Rect2 Info = new(14, 50, 316, 80);
        public static Rect2 InfoRow(int row) => new(22, 54 + row * Row, 300, Row);
        public static readonly Rect2 Tickets = new(14, 136, 316, 32);
        public static readonly Rect2 TicketsRow = new(22, 140, 300, Row);
        public static readonly Rect2 Player = new(14, 174, 316, Row);
        public static readonly Rect2 Requirement = new(24, 209, 45, 45);
        public static readonly Rect2 Cost = new(112, 202, 218, 24);
        public static readonly Rect2 CostCoin = new(86, 201, 22, 25);
        public static readonly Rect2 Buy = new(84, 231, 246, 29);
        public static readonly Rect2 Status = new(14, 266, 316, Row);
        public static readonly Rect2 PrizeCaption = new(14, 294, 316, Row);
        public const float PrizeRule = 318;
        public static Rect2 Prizes(float height) => new(14, 324, 316, height);
        public static float Winners(int count) => Math.Clamp(count, 1, WinnerRows) * WinnerPitch - 2;
    }

    public static class Roulette
    {
        public static readonly Vector2 Size = new(330, 272 + 102 + Bottom);
        public static readonly Rect2 ReelCaption = new(14, 48, 302, Row);
        public const int ReelCells = 5, ReelGap = 12;
        public static Rect2 ReelCell(int index) => new(28 + index * (45 + ReelGap), 78, 45, 45);
        public static readonly Rect2 Result = new(14, 130, 302, Row);
        public static readonly Rect2 Coins = new(14, 160, 302, 32);
        public static readonly Rect2 CoinsCaption = new(22, 164, 150, Row);
        public static readonly Rect2 CoinsValue = new(172, 164, 136, Row);
        public static readonly Rect2 Spin = new(92, 202, 146, 29);
        public static readonly Rect2 LogCaption = new(14, 242, 302, Row);
        public const float LogRule = 266;
        /// <summary>Four whole 24-pixel prize rows at a 26-pixel pitch.</summary>
        public static readonly Rect2 Log = new(14, 272, 302, 102);
        public static Rect2 LogRow(int index) => new(0, index * 26, 286, 24);
    }

    public static class Tournament
    {
        public static readonly Vector2 Size = new(330, 181 + Bottom);
        public static readonly Rect2 Info = new(14, 50, 302, 84);
        public static Rect2 Caption(int row) => new(24, 56 + row * 26, 130, Row);
        public static Rect2 Value(int row) => new(154, 56 + row * 26, 152, Row);
        public static readonly Rect2 Register = new(14, 152, 146, 29);
        public static readonly Rect2 Unregister = new(170, 152, 146, 29);
    }
}
