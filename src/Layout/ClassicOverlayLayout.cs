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
    /// <summary>
    /// The original message box art, sized to its content. Every prompt on it uses the same vertical rhythm:
    /// title band, <see cref="TextGap"/>, the message at its wrapped height, <see cref="ActionGap"/>, the 97 x 29
    /// buttons and <see cref="ButtonBottom"/>. The art lengthens through its middle band (y 64) and shortens by
    /// trimming the inner rows of its top and bottom halves, so no prompt keeps an empty band.
    /// </summary>
    public static class Message
    {
        public const float Width = 327, Height = 156, MiddleBand = 64;
        public static readonly Rect2 Title = new(12, 10, 303, 15);
        public static readonly Rect2 Text = new(21, 28, 285, 58);
        public static readonly Vector2 Button = new(97, 29);
        public const float ButtonGap = 19, ButtonBottom = 10;
        /// <summary>Title band to message, and message to buttons: both read as an even 16-17 pixel gap between glyphs and art.</summary>
        public const float TextGap = 15, ActionGap = 15;
        /// <summary>Under a message without buttons (a non-dismissable notice).</summary>
        public const float TextBottom = 15;
        /// <summary>Message top in a box without a title (the glyphs sit as far from the frame as the buttons do).</summary>
        public const float UntitledTop = 22;
        public static float TextTop => Title.End.Y + TextGap;
        public static Rect2 TextAt(float top, float height) => new(Text.Position.X, top, Text.Size.X, height);
        /// <summary>Box height for a message of <paramref name="text"/> pixels that starts at <paramref name="top"/>.</summary>
        public static float HeightFor(float text, bool buttons = true, float? top = null) =>
            Mathf.Ceil((top ?? TextTop) + text + (buttons ? ActionGap + Button.Y + ButtonBottom : TextBottom));
        public static Rect2 Single(float height) => new(115, height - ButtonBottom - Button.Y, Button.X, Button.Y);
        public static Rect2 Left(float height) => new(57, height - ButtonBottom - Button.Y, Button.X, Button.Y);
        public static Rect2 Right(float height) => new(173, height - ButtonBottom - Button.Y, Button.X, Button.Y);
    }

    /// <summary>Temple event registration: the message box with a countdown and its bar above the status.</summary>
    public static class Registration
    {
        public static readonly Rect2 Title = Message.Title;
        public static readonly Rect2 Timer = new(21, Message.TextTop, 285, 18);
        public static readonly Rect2 Bar = new(76, Timer.End.Y + 5, 175, 9);
        public static readonly Rect2 Status = new(21, Bar.End.Y + 5, 285, 38);
        public static readonly Vector2 Size = new(Message.Width, Message.HeightFor(Status.End.Y - Message.TextTop));
        public static Rect2 Join => Message.Left(Size.Y);
        public static Rect2 Close => Message.Right(Size.Y);
    }

    /// <summary>Death notice: the experience loss in the title band, the respawn hint as the message (sized to its lines).</summary>
    public static class Death
    {
        public static readonly Rect2 Loss = Message.Title;
        public static Vector2 Size(float hint) => new(Message.Width, Message.HeightFor(hint));
        public static Rect2 Hint(float hint) => Message.TextAt(Message.TextTop, hint);
        public static Rect2 Ok(float hint) => Message.Single(Size(hint).Y);
    }

    /// <summary>Invite by name: the message box with its original edit field (<c>Edit_Common</c>).</summary>
    public static class NamePrompt
    {
        public static readonly Rect2 Title = Message.Title;
        public static readonly Rect2 Text = new(21, Message.TextTop, 285, 18);
        public static readonly Rect2 Edit = new(81, Text.End.Y + 8, 173, 23);
        public static readonly Vector2 Size = new(Message.Width, Message.HeightFor(Edit.End.Y - Message.TextTop));
        public static Rect2 Send => Message.Left(Size.Y);
        public static Rect2 Cancel => Message.Right(Size.Y);
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
