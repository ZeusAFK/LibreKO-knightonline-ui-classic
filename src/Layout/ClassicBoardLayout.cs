using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Editable geometry of the event boards. They have no original UIF; each is composed from the nation party
/// board (<c>{nation}_partyboard_us</c>): its frame, table borders, column lines and header rule, its 70x18
/// buttons in their sunken slots, the friend-list page arrows and the trade close button. Proportions follow
/// that board: 10 px table insets, 23 px headings, 18 px rows and the paging row 10 px under the table.
/// </summary>
public static class ClassicBoardLayout
{
    public const string Art = "{nation}_partyboard_us";
    public const string Gauge = "{nation}_statebar_us";
    public const float RowHeight = 18;
    public const float RowWidth = 292;

    public static Rect2 Title(float width) => new(36, 12, width - 72, 28);
    public static Rect2 Close(float width) => new(width - 24, 5, 20, 20);
    public static Rect2 Drag(float width) => new(8, 3, width - 36, 40);

    /// <summary>A list column: heading cell and the cell inside each row (row-relative x).</summary>
    public readonly record struct Column(Rect2 Head, float X, float Width, HorizontalAlignment Align);

    /// <summary>Paging row: arrows either side of the page number, optional action on the right.</summary>
    public static Rect2 PageUp(float y) => new(90, y, 40, 18);
    public static Rect2 PageDown(float y) => new(192, y, 40, 18);
    public static Rect2 PageText(float y) => new(131, y - 5, 60, 28);
    public static Rect2 PageAction(float y) => new(236, y, 70, 18);

    public static class Bounty
    {
        public static readonly Vector2 Size = new(320, 389);
        public static readonly Rect2 Box = new(10, 48, 300, 301);
        public const float HeadRule = 75, FormRule = 262;
        public static readonly Column[] Columns =
        {
            new(new(14, 51, 96, 23), 0, 96, HorizontalAlignment.Left),
            new(new(117, 51, 96, 23), 103, 96, HorizontalAlignment.Right),
            new(new(220, 51, 86, 23), 206, 86, HorizontalAlignment.Left),
        };
        public static readonly float[] Lines = { 112, 215 };
        public static readonly Rect2 Rows = new(14, 79, RowWidth, 180);
        public const int PageRows = 10;
        public static readonly Rect2 FormTitle = new(18, 268, 284, 18);
        public static readonly Rect2 TargetCaption = new(18, 290, 56, 22), Target = new(76, 290, 226, 22);
        public static readonly Rect2 RewardCaption = new(18, 318, 56, 22), Reward = new(76, 318, 148, 22);
        public static readonly Rect2 Post = new(232, 320, 70, 18);
        public const float Paging = 359;
    }

    public static class Fishing
    {
        public static readonly Vector2 Size = new(320, 358);
        public static readonly Rect2 Caption = new(14, 44, 292, 20);
        public static readonly Rect2 Box = new(10, 66, 300, 251);
        public const float HeadRule = 93;
        public static readonly Column[] Columns =
        {
            new(new(14, 69, 46, 23), 0, 46, HorizontalAlignment.Center),
            new(new(65, 69, 160, 23), 51, 160, HorizontalAlignment.Left),
            new(new(231, 69, 75, 23), 217, 75, HorizontalAlignment.Right),
        };
        public static readonly float[] Lines = { 62, 228 };
        public static readonly Rect2 Rows = new(14, 97, RowWidth, 216);
        public const int PageRows = 12;
        public const float Paging = 327;
    }

    public static class Report
    {
        public static readonly Vector2 Size = new(320, 551);
        public static readonly Rect2 Gate = new(14, 42, 292, 32);
        public static readonly Rect2 FormBox = new(10, 76, 300, 106);
        public static readonly Rect2 FormTitle = new(18, 81, 284, 18);
        public static readonly Rect2 TargetCaption = new(18, 102, 62, 22), Target = new(82, 102, 220, 22);
        public static readonly Rect2 ReasonCaption = new(18, 128, 62, 22), Reason = new(82, 128, 220, 22);
        public static readonly Rect2 File = new(202, 157, 100, 18);
        public static readonly Rect2 ListTitle = new(14, 186, 292, 20);
        public static readonly Rect2 Box = new(10, 208, 300, 255);
        public const float HeadRule = 235, DetailRule = 385;
        public static readonly Column[] Columns =
        {
            new(new(14, 211, 169, 23), 0, 169, HorizontalAlignment.Left),
            new(new(189, 211, 117, 23), 175, 117, HorizontalAlignment.Center),
        };
        public static readonly float[] Lines = { 186 };
        public static readonly Rect2 Rows = new(14, 239, RowWidth, 144);
        public const int PageRows = 8;
        /// <summary>Four whole lines of the selected reason.</summary>
        public static readonly Rect2 Detail = new(18, 391, 264, 64);
        public const float DetailLine = 16;
        public static readonly Rect2 DetailRail = new(286, 390, 18, 66);
        public const float Paging = 473;
        public static readonly Rect2 VoteYes = new(73, 499, 70, 18), VoteNo = new(179, 499, 70, 18);
        public static readonly Rect2 Status = new(14, 523, 292, 18);
    }

    public static class Forces
    {
        public static readonly Vector2 Size = new(280, 185);
        public static readonly Rect2 State = new(14, 44, 252, 18);
        public static readonly Rect2 Box = new(10, 64, 260, 80);
        public static readonly float[] Lines = { 100 };
        public const float RowTop = 67, RowPitch = 24;
        public static Rect2 Caption(int row) => new(16, RowTop + row * RowPitch, 82, RowPitch);
        public static Rect2 Value(int row) => new(110, RowTop + row * RowPitch, 154, RowPitch);
        public static readonly Rect2 Gauge = new(110, 120, 117, 14);
        public static readonly Rect2 Percent = new(228, 115, 38, 24);
        public static readonly Rect2 Join = new(55, 154, 70, 18), Leave = new(155, 154, 70, 18);
    }
}
