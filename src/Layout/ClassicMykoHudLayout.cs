using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Names and geometry of the HUD pieces drawn on the original art read through <see cref="LocalAssets"/>: the status
/// bar's clock spot and the top-right group (premium, time, notice bars,
/// GM List, content box). Each piece keeps the Classic design when its layouts are not found; these values only say
/// where the plugin places native content on the art.
/// </summary>
public static class ClassicMykoHudLayout
{
    /// <summary>The status bar menu; its clock (<c>btn_scheduler</c>) marks where the row's hourglass goes.</summary>
    public const string HpMenu = "re_{nation}_hpmenu";
    public const string Notice = "re_eventnotice";
    /// <summary>The content box under the group; its frame and text style also draw the one-line time box.</summary>
    public const string Content = "re_{nation}_eventcontent";
    /// <summary>The premium bar (<c>btn_premium</c>) and the clan premium bar (<c>btn_clanpremium</c>).</summary>
    public const string Premium = "re_eventpremium";

    public static readonly string[] Layouts = { HpMenu, Notice, Content, Premium };

    // ---- Notice bars and content box ------------------------------------------------------------------

    /// <summary>The notice bar (<c>btn_notice</c>, 169×23) and its expand/collapse knob 16×16 at (152, 5).</summary>
    public static readonly Vector2 Bar = new(169, 23);
    public const int BarGap = 1, KnobRight = 1, KnobTop = 5, CaptionLeft = 17;
    public const string GmListTitle = "GM List", NoGms = "No GMs online.";
    /// <summary>The time box: bar-sized, on the content box's frame (this many pixels wide), "Time : 14:33" centred.</summary>
    public const string TimeNote = "Time : ";
    public const int TimeFrame = 4;
    /// <summary>The top-right group: right edge this far in from the screen edge (the client's premium chip spot), top edge.</summary>
    public const int GroupRight = 18, GroupTop = 12;
    public const int ContentGap = 2, ContentLineScroll = 1;

    /// <summary>The caption ink of a bar that shows no premium.</summary>
    public static readonly Color Muted = new("a0a0a0");
}
