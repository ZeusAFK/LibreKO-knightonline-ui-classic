using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Names and geometry of the HUD pieces drawn on the original art read through <see cref="LocalAssets"/>: the status
/// bar's clock and letter plates, the event schedule window and the top-right group (premium, time, notice bars,
/// GM List, content box). Each piece keeps the Classic design when its layouts are not found; these values only say
/// where the plugin places native content on the art.
/// </summary>
public static class ClassicMykoHudLayout
{
    /// <summary>Letter plates (<c>btn_pus</c>, <c>btn_searchdrop</c>, <c>btn_dailyrank</c>) and the clock (<c>btn_scheduler</c>).</summary>
    public const string HpMenu = "re_{nation}_hpmenu";
    public const string Scheduler = "re_{nation}_scheduler";
    public const string Notice = "re_eventnotice";
    public const string Content = "re_{nation}_eventcontent";
    /// <summary>The premium bar (<c>btn_premium</c>) and the clan premium bar (<c>btn_clanpremium</c>).</summary>
    public const string Premium = "re_eventpremium";
    /// <summary>The small time plate (<c>str_note</c>, <c>str_time</c>).</summary>
    public const string Time = "co_time";

    public static readonly string[] Layouts = { HpMenu, Scheduler, Notice, Content, Premium, Time };

    // ---- Letter plates -------------------------------------------------------------------------------

    /// <summary>The letter plates sit in one column of the plate sheet, one plate every 29 pixels below P.</summary>
    public const int PlatePitch = 29;

    /// <summary>Rows below the P plate of the letters the sheet has besides P, D and R (the menu's buttons).</summary>
    public static readonly Dictionary<string, int> SheetRows = new() { ["P"] = 0, ["D"] = 2, ["E"] = 4, ["Q"] = 5 };

    /// <summary>The sheet has no A or G plate: these are drawn on the plate's letterless border and fill.</summary>
    public const int PlateBorder = 3, PlateFillColumn = 4;
    /// <summary>The sheet's letters: 19 pixels high, standing on row 23 of the 28-pixel plate.</summary>
    public const int LetterFontSize = 28, LetterBaseline = 23;

    // ---- Notice bars and content box ------------------------------------------------------------------

    /// <summary>The notice bar (<c>btn_notice</c>, 169×23) and its expand/collapse knob 16×16 at (152, 5).</summary>
    public static readonly Vector2 Bar = new(169, 23);
    public const int BarGap = 1, KnobRight = 1, KnobTop = 5, CaptionLeft = 17;
    public const string GmListTitle = "GM List", NoGms = "No GMs online.";
    public const string TimeNote = "Time";
    /// <summary>The top-right group: right edge this far in from the screen edge (the client's premium chip spot), top edge.</summary>
    public const int GroupRight = 18, GroupTop = 12;
    public const int ContentGap = 2, ContentLineScroll = 1;

    // ---- Event schedule -------------------------------------------------------------------------------

    public const int ScheduleRows = 10;
    /// <summary>The title strip a drag may start on (left of the close button).</summary>
    public static readonly Rect2 SchedulerDrag = new(0, 0, 268, 26);
    /// <summary>The name column of the list: empty and loading lines are centred here, clear of the column rule.</summary>
    public static readonly Rect2 ScheduleList = new(38, 99, 149, 230);
    public const string TimeCaption = "Local Time :";
    public static readonly Color Muted = new("a0a0a0"), Action = new("ffff80"), Running = new("80ff80");
}
