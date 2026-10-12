using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Editable geometry and type of the quest notice (<c>quest_available</c>), the quest tracker and the premium chips.
/// The quest notice follows the original 1.298 login tip <c>co_rookietip_us</c>: the plain message-box frame
/// (<c>ui_message_us</c>) 343 pixels wide, the round close knob, a yellow Arial Bold heading over white Arial Bold
/// text; its pager uses the Level Guide arrows (<c>{nation}_levelguide_us</c> <c>btn_down</c>/<c>btn_up</c>, 32 × 17).
/// The tracker is the width of the original quest notify box (<c>co_quest_mini_tip_us</c>, 219 pixels) in the
/// same plain frame. The premium chips are information plaques, like the event title chips beside them, with the
/// original tooltip (<c>co_tooltip_us</c>: translucent black, white Arial Bold).
/// </summary>
public static class ClassicQuestHudLayout
{
    /// <summary>The login quest notice.</summary>
    public static class Notice
    {
        /// <summary>Width of the original co_rookietip_us frame.</summary>
        public const int Width = 343;
        /// <summary>Content insets inside the frame: the event-window margins (title strip above the content).</summary>
        public const int Margin = ClassicEventFamilyLayout.Margin, ContentGap = ClassicEventFamilyLayout.ContentGap;
        public const int BodyWidth = Width - Margin * 2;
        /// <summary>Vertical gap between the pager, the state caption, the text and each answer row.</summary>
        public const int Gap = 6;
        /// <summary>Pager row: the Level Guide arrows at both ends, the quest name centred between them.</summary>
        public const int PagerHeight = 22, PagerGap = 6;
        /// <summary>Caption inset inside an answer row, so a trimmed caption stays clear of the button caps.</summary>
        public const int CaptionInset = 10;
        public static readonly Vector2 Arrow = new(32, 17);
        public const int AnswerHeight = ClassicEventFamilyLayout.Button, FontSize = 13, ButtonFont = 12;
        /// <summary>The window's top edge never rises above this when it follows a longer page.</summary>
        public const int ScreenMargin = 8;
    }

    /// <summary>The active quest tracker on the HUD.</summary>
    public static class Tracker
    {
        /// <summary>Original quest notify width (co_quest_mini_tip_us) minus the frame padding.</summary>
        public const int Width = 220, PadX = 12, PadY = 9, ContentWidth = Width - PadX * 2;
        /// <summary>HUD panels stay see-through: the plain frame over a translucent black ground.</summary>
        public const float Alpha = .72f;
        public const int HeaderHeight = 22, RowGap = 4, PagerHeight = 22, BarHeight = 4, BarIndent = 8;
        public const int TitleFont = 13, QuestFont = 12, ObjectiveFont = 12, PageFont = 12;
        public static readonly Vector2 Arrow = new(32, 17), Fold = new(19, 15);
    }

    /// <summary>The account and clan premium chips.</summary>
    public static class Premium
    {
        public const float Height = ClassicOverlayLayout.Plaque.Height, Inset = ClassicOverlayLayout.Plaque.Inset, PadY = 3;
        public const int FontSize = 12;
        /// <summary>Gap between the clan bonus chip's "Clan" and its O/X marks.</summary>
        public const int BonusGap = 6;
        /// <summary>Original tooltip: black at 187/255 behind white Arial Bold 10 pt (13 px).</summary>
        public static readonly Color TooltipGround = new(0, 0, 0, 187f / 255f);
        public const int TooltipFont = 13, TooltipPadX = 6, TooltipPadY = 4;
    }

    /// <summary>Level Guide page number ink (text_page): light parchment on El Morad, silver on Karus.</summary>
    public static Color PageInk => new(ClassicDesign.Karus ? "c0c0c0" : "f2d5b0");
    /// <summary>Premium not active: the plaque's muted caption.</summary>
    public static Color PlaqueMuted => new(ClassicDesign.Karus ? "4d4a44" : "b4aea1");
}
