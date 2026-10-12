using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// The Classic character card (<c>userinfo</c>) inside the floating detail shell: the character facts as the
/// reviewed information rows (caption left, value right, the report rule under each), the clan block in the
/// original list frame with the podium and clan mark in inventory cells, and the rank marks (summary ribbons,
/// then one row per ranking with its place and bronze, silver and gold ribbons).
/// </summary>
public static class ClassicUserInfoLayout
{
    /// <summary>The shell is as wide as the client's card so nine ribbons fit on one summary line.</summary>
    public const float WindowWidth = 440, WindowMaxHeight = 640;
    /// <summary>Content width: the shell less the detail panel's 8 px insets.</summary>
    public const float Width = WindowWidth - 16;
    public const float FactRow = 28, FactKey = 124, TextInset = 8, Rule = 2, SectionGap = 8;
    public static Rect2 FactKeyRect(int row) => new(TextInset, row * FactRow, FactKey, FactRow - Rule);
    public static Rect2 FactValueRect(int row) => new(TextInset + FactKey, row * FactRow, Width - FactKey - TextInset * 2, FactRow - Rule);
    public static Rect2 FactRule(int row) => new(0, row * FactRow + FactRow - Rule, Width, Rule);

    /// <summary>Clan block: the original list frame, the podium and mark cells, three caption/value lines.</summary>
    public const float ClanHeight = 76, Cell = 52, ClanLine = 22;
    public static Rect2 ClanBox(float top) => new(0, top, Width, ClanHeight);
    public static Rect2 Podium(float top) => new(10, top + 12, Cell, Cell);
    public static Rect2 Mark(float top) => new(70, top + 12, Cell, Cell);
    public static Rect2 ClanKey(float top, int line) => new(134, top + 5 + line * ClanLine, 66, ClanLine);
    public static Rect2 ClanValue(float top, int line) => new(200, top + 5 + line * ClanLine, Width - 200 - TextInset, ClanLine);
    public static Rect2 ClanEmpty(float top) => new(TextInset, top, Width - TextInset * 2, ClanHeight);
    /// <summary>The client's ribbon art keeps its own size: 45 x 17, nine to a summary line.</summary>
    public const float Ribbon = 45, RibbonHeight = 17;
    public const int SummaryColumns = 9;
    public static float SummaryLeft => Mathf.Floor((Width - SummaryColumns * Ribbon) / 2);
    public static Rect2 SummaryCell(float top, int index) =>
        new(SummaryLeft + index % SummaryColumns * Ribbon, top + index / SummaryColumns * RibbonHeight, Ribbon, RibbonHeight);
    public static float SummaryHeight(int cells) => cells <= 0 ? 0 : Mathf.Ceil(cells / (float)SummaryColumns) * RibbonHeight;
    /// <summary>Ranking rows: the heading line, then title, place and the three tiers per row.</summary>
    public const float RankRow = 21, PlaceWidth = 56;
    public static float TiersLeft => Width - TextInset - 3 * Ribbon;
    public static float PlaceLeft => TiersLeft - PlaceWidth;
    public static Rect2 RankTitle(float top) => new(TextInset, top, PlaceLeft - TextInset - 4, RankRow - 1);
    public static Rect2 RankPlace(float top) => new(PlaceLeft, top, PlaceWidth, RankRow - 1);
    public static Rect2 RankTier(float top, int tier) => new(TiersLeft + tier * Ribbon, top + 2, Ribbon, RibbonHeight);
    public static Rect2 RankRule(float top) => new(0, top + RankRow - 1, Width, 1);
}
