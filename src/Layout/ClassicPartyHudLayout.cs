using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// The Classic party list (co_party_us): the "Party" caption, one 131 x 45 member cell per member with the
/// original HP and MP bars and the name under them, and the party actions on the party board's button art.
/// </summary>
public static class ClassicPartyHudLayout
{
    public const string Art = "co_party_us";
    public const string ButtonArt = "{nation}_partyboard_us";
    public const string LayoutId = "classic_party";
    /// <summary>
    /// The Classic party list's spot: the top-right screen edge, as the Classic party roster docked. The left
    /// edge under the status bars, where the client puts its party HUD, holds the Classic minimap.
    /// </summary>
    public static Vector2 DefaultPosition(Vector2 screen) => new(Mathf.Max(0, screen.X - Width), 0);
    public const float Width = 131;
    public static readonly Rect2 Drag = new(1, 2, 123, 22);
    public const float RowsTop = 25, RowPitch = 45;
    public static Rect2 Row(int index) => new(0, RowsTop + index * RowPitch, Width, RowPitch);
    /// <summary>Inside a member cell: the bars and name at their original places (Area_0 starts at y 27).</summary>
    public static readonly Rect2 Hp = new(8, 1, 108, 12), Mp = new(8, 11, 108, 7), Name = new(9, 20, 100, 15);
    /// <summary>The party meter (damage dealt or healing) under the name, as the client's row meter.</summary>
    public static readonly Rect2 Meter = new(8, 37, 108, 4);
    public static readonly Rect2 Selection = new(6, 0, 112, 38);
    public static readonly Color MeterDamage = Color.Color8(182, 132, 13), MeterHealing = Color.Color8(29, 165, 162);
    /// <summary>
    /// Move up and Move down (the client's own row order) at the cell's right edge beside the HP and MP bars,
    /// on the party board's up and down page arrows at a third of their size.
    /// </summary>
    public const string ArrowArt = ButtonArt;
    public const string ArrowUp = "btn_page_up", ArrowDown = "btn_page_down";
    public static readonly Rect2 MoveUp = new(119, 1, 12, 7), MoveDown = new(119, 10, 12, 7);
    /// <summary>Two columns of the party board's small buttons under the last member; the meter, the automatic
    /// re-invite and, while the commander bar is hidden, its restore action each span both.</summary>
    public static Rect2 Action(int index) => new(6 + index % 2 * 60, index / 2 * 21, 58, 18);
    public static readonly Rect2 MeterAction = new(6, 42, 118, 18);
    public static readonly Rect2 RejoinAction = new(6, 63, 118, 18);
    public static readonly Rect2 CommanderAction = new(6, 84, 118, 18);
    public const float ActionsHeight = 85, CommanderHeight = 21;
    public static float Height(int members, bool commander = false) =>
        RowsTop + members * RowPitch + ActionsHeight + (commander ? CommanderHeight : 0);

    /// <summary>
    /// The Classic Party War Board: the plain Classic frame beside the party list, the title row with the
    /// transparency slider and close button, the Damage and Healing tabs (chat tab art), and one 18 px row per
    /// member (rank and name over the class-coloured share bar, percent on the right).
    /// </summary>
    public static class WarBoard
    {
        public const string LayoutId = "classic_war_board";
        public const float Width = 250, RowHeight = 18, RowGap = 2;
        public static readonly Rect2 Title = new(16, 14, 130, 24);
        public static readonly Rect2 Alpha = new(150, 18, 56, 16);
        public static Rect2 Close(float width) => ClassicFrame.Close(width);
        public static readonly Rect2 Drag = new(8, 8, 138, 36);
        public static Rect2 Tab(int index) => new(16 + index * 74, 46, 72, 20);
        public const float RowsTop = 74;
        public static Rect2 Row(int index) => new(16, RowsTop + index * (RowHeight + RowGap), Width - 32, RowHeight);
        public const float Icon = 16;
        public static float Height(int rows) => RowsTop + Math.Max(1, rows) * (RowHeight + RowGap) - RowGap + ClassicFrame.Inset;
        /// <summary>Beside the party list (top-right), as the client docks its board beside its party HUD.</summary>
        public static Vector2 DefaultPosition(Vector2 screen) => new(Mathf.Max(0, screen.X - ClassicPartyHudLayout.Width - Width - 8), 0);
        /// <summary>The client's class fills (warrior, rogue, mage, priest, Kurian) behind the share bars.</summary>
        public static readonly Dictionary<int, Color> ClassFills = new()
        {
            [1] = Color.Color8(112, 9, 33), [2] = Color.Color8(47, 98, 48), [3] = Color.Color8(109, 54, 112),
            [4] = Color.Color8(0, 57, 112), [5] = Color.Color8(116, 99, 34),
        };
    }
}
