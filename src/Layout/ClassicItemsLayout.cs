using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Declarative geometry of the Classic item windows of the plain frame family (EXP Seal, Temporary Item List,
/// Loot Filter, Character Seal pages and the Cypher Ring sheet). Content keeps the frame's 16 px inset on every
/// side; rows use the inventory's 45 px cells and the service windows' 27 px buttons.
/// </summary>
public static class ClassicItemsLayout
{
    public const float Inset = ClassicFrame.Inset;
    public const float ButtonHeight = 27, RowHeight = 22;

    public static Vector2 Size(string id, string page = "") => id switch
    {
        "exp_seal" => new(300, 185),
        "timed_items" => new(380, 310),
        "loot_filter" => new(360, 460),
        "char_seal" => page switch { "select" => new(380, 273), "code" => new(380, 290), _ => new(380, 327) },
        "char_seal_sheet" => new(380, page == "achievements" ? 575 : 527),
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    // EXP Seal: the jar socket beside its fill, the ON / OFF switch, then the amount to bottle.
    public static readonly Rect2 ExpJar = new(16, 50, 45, 45);
    public static readonly Rect2 ExpPercent = new(73, 50, 211, 22), ExpAmount = new(73, 73, 211, 22);
    public static readonly Rect2 ExpCaption = new(16, 111, 120, 25), ExpOn = new(140, 111, 68, 25), ExpOff = new(216, 111, 68, 25);
    public static readonly Rect2 ExpPick = new(16, 144, 140, 25), ExpObtain = new(164, 144, 120, 25);

    // Temporary Item List: notice, three item rows with a row scroll bar, and the "do not show today" switch.
    public const float TimedRowTop = 84, TimedRowPitch = 60, TimedRowHeight = 56;
    public static readonly Rect2 TimedNotice = new(16, 46, 348, 32);
    public static Rect2 TimedCell(int row) => new(16, TimedRowTop + row * TimedRowPitch + 5, 45, 45);
    public static Rect2 TimedName(int row) => new(72, TimedRowTop + row * TimedRowPitch + 2, 272, 18);
    public static Rect2 TimedPlace(int row) => new(72, TimedRowTop + row * TimedRowPitch + 20, 272, 16);
    public static Rect2 TimedTime(int row) => new(72, TimedRowTop + row * TimedRowPitch + 37, 272, 17);
    public static readonly Rect2 TimedScroll = new(350, TimedRowTop, 14, 3 * TimedRowPitch - 4);
    public static readonly Rect2 TimedHideLabel = new(16, 272, 316, 22), TimedHide = new(338, 272, 26, 22);

    // Loot Filter: switches by group and grade, the price floor and the per-item rules.
    public static readonly Rect2 LootEnabled = new(16, 48, 328, RowHeight);
    public static readonly Rect2 LootGroupsCaption = new(16, 78, 328, 20);
    public static Rect2 LootGroup(int index) => new(16, 100 + index * RowHeight, 328, RowHeight);
    public static readonly Rect2 LootGradesCaption = new(16, 196, 328, 20);
    public static Rect2 LootGrade(int index) => new(16 + index % 3 * 110, 218 + index / 3 * RowHeight, 108, RowHeight);
    public static readonly Rect2 LootPriceLabel = new(16, 270, 150, 24), LootPrice = new(172, 270, 172, 24);
    public static readonly Rect2 LootRulesCaption = new(16, 302, 328, 20), LootRules = new(16, 324, 328, 120);

    // Character Seal: the help page, the character slot list and the code keypad.
    public static Rect2 SealHelpTitle(int index) => new(16, 48 + index * 114, 348, 20);
    public static Rect2 SealHelpText(int index) => new(16, 70 + index * 114, 348, 84);
    public static Rect2 SealPageLeft(float top) => new(16, top, 166, ButtonHeight);
    public static Rect2 SealPageRight(float top) => new(198, top, 166, ButtonHeight);
    public const float SealHelpButtons = 284, SealSelectButtons = 230, SealCodeEdit = 212, SealCodeButtons = 247;
    public static readonly Rect2 SealTitle = new(16, 48, 348, 20);
    public static Rect2 SealSlot(int index) => new(16, 74 + index * 30, 348, 26);
    public static readonly Rect2 SealPrompt = new(16, 200, 348, 20);
    public static readonly Rect2 SealStars = new(16, 74, 348, 30);
    public const float SealKey = 40, SealKeyGap = 6;
    public static readonly Rect2 SealKeys = new(78, 114, 5 * SealKey + 4 * SealKeyGap, 2 * SealKey + SealKeyGap);

    // Cypher Ring sheet: identity, statistics and wealth on the left, gear on the right, the bag below.
    public const float SheetCell = 40, SheetGap = 4;
    public static readonly Rect2 SheetName = new(16, 46, 348, 22);
    public static readonly Rect2 SheetIdentity = new(16, 74, 200, 78);
    public static readonly Rect2 SheetAchievementsButton = new(16, 158, 200, 25);
    public static readonly Rect2 SheetStats = new(16, 191, 200, 92);
    public static readonly Rect2 SheetWealth = new(16, 291, 200, 38);
    public static readonly Rect2 SheetEquip = new(236, 74, 3 * SheetCell + 2 * SheetGap, 5 * SheetCell + 4 * SheetGap);
    public static readonly Rect2 SheetBag = new(38, 339, 7 * SheetCell + 6 * SheetGap, 4 * SheetCell + 3 * SheetGap);
    public static readonly Rect2 SheetAchievements = new(16, 519, 348, 40);
}
