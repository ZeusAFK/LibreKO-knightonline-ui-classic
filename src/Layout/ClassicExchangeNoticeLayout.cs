using Godot;
using Box = KnightOnlineUiClassic.Layout.ClassicOverlayLayout.Message;
namespace KnightOnlineUiClassic.Layout;
/// <summary>
/// Editable nation message-box composition used by the original trade permission flow. The box fits its
/// message with the common message rhythm; the request keeps the original Yes/No places at the sides.
/// </summary>
public static class ClassicExchangeNoticeLayout
{
    public static readonly Rect2 Title=new(12,10,297,16);
    /// <summary>The final question has no title: its message starts where the glyphs sit as far from the frame as the buttons.</summary>
    public static float MessageTop(bool final)=>final?Box.UntitledTop:Box.TextTop;
    public static Rect2 Message(bool final,float text)=>Box.TextAt(MessageTop(final),text);
    public static Vector2 Size(bool final,float text)=>new(Box.Width,Box.HeightFor(text,true,MessageTop(final)));
    private static float ButtonTop(float height)=>height-Box.ButtonBottom-Box.Button.Y;
    public static Rect2 Accept(float height)=>new(ClassicDesign.Karus?11:9,ButtonTop(height),97,29);
    public static Rect2 Decline(float height)=>new(ClassicDesign.Karus?219:220,ButtonTop(height),97,29);
    public static Rect2 FinalAccept(float height)=>Box.Left(height);
    public static Rect2 FinalDecline(float height)=>Box.Right(height);
    public static Rect2 WaitCancel(float height)=>Box.Single(height);
}
