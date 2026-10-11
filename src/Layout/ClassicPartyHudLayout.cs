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
    /// <summary>Same spot as the client's party HUD: the left screen edge, under the status bars.</summary>
    public static readonly Vector2 DefaultPosition = new(12, 176);
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
    /// <summary>Two columns of the party board's small buttons under the last member; the meter spans both.</summary>
    public static Rect2 Action(int index) => new(6 + index % 2 * 60, index / 2 * 21, 58, 18);
    public static readonly Rect2 MeterAction = new(6, 42, 118, 18);
    public const float ActionsHeight = 64;
    public static float Height(int members) => RowsTop + members * RowPitch + ActionsHeight;
}
