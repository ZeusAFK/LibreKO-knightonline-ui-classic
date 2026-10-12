using Godot;
using Message = KnightOnlineUiClassic.Layout.ClassicOverlayLayout.Message;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// The Juraid user vote windows on the original message box (<c>{nation}_messagebox_us</c>), sized to their
/// content with the shared message rhythm: title band, 15 px, content, 15 px, buttons, 10 px.
/// The select window lists the four reasons as radio rows with the original check socket, the 30-letter field
/// (Edit_Common) and the cost line; the question window shows the reason and the original countdown bar over
/// Yes, Draw and No; the notice lists the warning lines over OK.
/// </summary>
public static class ClassicUserVoteLayout
{
    public const int TitleSize = 13, TextSize = 13, ReasonSize = 12;
    public static float Left => Message.Text.Position.X;
    public static float TextWidth => Message.Text.Size.X;
    public static float Top => Message.TextTop;
    public const float LineGap = 8, ReasonRow = 20, Field = 23, BarHeight = 9, BarWidth = 175;
    public static Rect2 Text(float top, float height) => new(Left, top, TextWidth, height);
    public static Rect2 Reason(float top, int index) => new(Left + 4, top + index * ReasonRow, TextWidth - 8, ReasonRow);
    public static float Reasons(int count) => count * ReasonRow;
    public static Rect2 Edit(float top) => new(Left, top, TextWidth, Field);
    public static Rect2 Bar(float top) => new(Mathf.Floor((Message.Width - BarWidth) / 2), top, BarWidth, BarHeight);
    public static Vector2 Size(float contentBottom) => new(Message.Width, Message.HeightFor(contentBottom - Top));
    /// <summary>Three answers share the button row: 89 px buttons with equal 16 px gaps and 14 px margins.</summary>
    public const float AnswerWidth = 89, AnswerGap = 16, AnswerMargin = 14;
    public static Rect2 Answer(float height, int index) =>
        new(AnswerMargin + index * (AnswerWidth + AnswerGap), height - Message.ButtonBottom - Message.Button.Y, AnswerWidth, Message.Button.Y);
}
