using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Editable geometry of the native world overlays. Prompts use the original 327-pixel message box
/// (title, message band and the 97 x 29 button art at its native places); one-line banners and chips
/// use the original nation information plaque at its native 22-pixel height; the loot window uses the
/// original dropped-item grid under that plaque.
/// </summary>
public static class ClassicOverlayLayout
{
    /// <summary>The original message box; the middle band (y 64) stretches when a prompt needs more room.</summary>
    public static class Message
    {
        public const float Width = 327, Height = 156, MiddleBand = 64;
        public static readonly Rect2 Title = new(12, 10, 303, 15);
        public static readonly Rect2 Text = new(21, 28, 285, 58);
        public static readonly Vector2 Button = new(97, 29);
        public const float ButtonGap = 19, ButtonBottom = 10;
        public static Rect2 Single(float height) => new(115, height - ButtonBottom - Button.Y, Button.X, Button.Y);
        public static Rect2 Left(float height) => new(57, height - ButtonBottom - Button.Y, Button.X, Button.Y);
        public static Rect2 Right(float height) => new(173, height - ButtonBottom - Button.Y, Button.X, Button.Y);
    }

    /// <summary>Temple event registration: the message box with a countdown and its bar above the status.</summary>
    public static class Registration
    {
        public static readonly Vector2 Size = new(Message.Width, 176);
        public static readonly Rect2 Title = Message.Title;
        public static readonly Rect2 Timer = new(21, 30, 285, 20);
        public static readonly Rect2 Bar = new(76, 55, 175, 9);
        public static readonly Rect2 Status = new(21, 69, 285, 38);
        public static Rect2 Join => Message.Left(Size.Y);
        public static Rect2 Close => Message.Right(Size.Y);
    }

    /// <summary>Death notice: the experience loss in the title band, the respawn hint as the message.</summary>
    public static class Death
    {
        public static readonly Vector2 Size = new(Message.Width, Message.Height);
        public static readonly Rect2 Loss = Message.Title;
        public static readonly Rect2 Hint = Message.Text;
        public static Rect2 Ok => Message.Single(Size.Y);
    }

    /// <summary>The information plaque: 22 pixels high, 4-pixel caps, captions inset like the original.</summary>
    public static class Plaque
    {
        public const float Height = 22, Inset = 8;
        public const int CapLeft = 4, CapRight = 5, CapTop = 4, CapBottom = 4;
        /// <summary>Original chat tab art (55 x 15), used for actions on a plaque.</summary>
        public static readonly Vector2 Action = new(55, 15);
        public const float BarHeight = 7;
    }

    /// <summary>The registration countdown plaque: title and time on one line, the original bar below.</summary>
    public static class Countdown
    {
        public static readonly Vector2 Size = new(212, 34);
        public static readonly Rect2 Title = new(Plaque.Inset, 3, 142, 16);
        public static readonly Rect2 Timer = new(150, 3, 54, 16);
        public static readonly Rect2 Bar = new(Plaque.Inset, 21, 196, Plaque.BarHeight);
    }

    /// <summary>The in-zone leave plaque: zone name and the original chat tab art as the Leave action.</summary>
    public static class Leave
    {
        public static readonly Vector2 Size = new(212, Plaque.Height);
        public static readonly Rect2 Title = new(Plaque.Inset, 3, 138, 16);
        public static readonly Rect2 Action = new(212 - Plaque.Inset - 55, 4, 55, 15);
    }

    /// <summary>The original dropped-item grid under an information plaque with the loot title and close.</summary>
    public static class Loot
    {
        public static readonly Vector2 Size = new(102, Plaque.Height + 151);
        public static readonly Rect2 Header = new(0, 0, 102, Plaque.Height);
        public static readonly Rect2 Title = new(6, 3, 72, 16);
        public static readonly Rect2 Close = new(80, 1, 20, 20);
        public static readonly Vector2 Grid = new(0, Plaque.Height);
        /// <summary>The six original item areas, in slot order.</summary>
        public static readonly Rect2[] Cells =
        {
            new(4, 4, 45, 45), new(53, 4, 45, 45), new(4, 53, 45, 45),
            new(53, 52, 45, 45), new(3, 102, 46, 44), new(53, 101, 45, 45),
        };
        public const float CountHeight = 15;
    }
}
