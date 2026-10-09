using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;

namespace KnightOnlineUiClassic.Windows;

public partial class ClassicChatPanel : Control
{
    public readonly Control Body=new();
    /// <summary>The 319 px body is centred in the 363 px plain frame: 22 px on the sides and below it.</summary>
    public const float Side=22,BodyTop=48;
    /// <summary>Panel height for a body of the given height.</summary>
    public static int Height(float body)=>(int)(BodyTop+body+Side);
    public ClassicChatPanel(string title,int height)
    {
        ZIndex=300;Size=new Vector2(363,height);CustomMinimumSize=Size;MouseFilter=MouseFilterEnum.Stop;
        var chrome=new ClassicFrame {Size=Size,BackgroundColor=Colors.Black,BackgroundAlpha=1};AddChild(chrome);
        Resized+=()=> {chrome.Size=Size;Body.Size=new Vector2(319,Size.Y-BodyTop-Side);};
        var caption=ClassicNpcLayout.Text(title,13,true);caption.Position=new Vector2(Side,14);caption.Size=new Vector2(287,24);AddChild(caption);
        caption.AddThemeColorOverride("font_color",ClassicDesign.FrameTitle);
        var close=new Button {TooltipText="Close",FocusMode=FocusModeEnum.None};AddChild(close);
        ClassicMerchantSkin.Button(close,Plugin.Kit.Layout("co_questtalk_us").Find("btn_close")!,this,new Rect2(363-Side-24,14,24,24),"");
        close.FocusMode=FocusModeEnum.None;close.Pressed+=()=>Visible=false;
        Body.Position=new Vector2(Side,BodyTop);Body.Size=new Vector2(319,height-BodyTop-Side);AddChild(Body);
        NativeLayout.Attach(this,"classic_"+title.Replace(' ','_').ToLowerInvariant(),caption,
            ()=>new Vector2(Mathf.Round((GetViewportRect().Size.X-Size.X)/2),Mathf.Round((GetViewportRect().Size.Y-Size.Y)/2)),legacyResizeGrip:true);
    }
    public static Button Button(string text)
    {
        // The plain-frame service action art; the ornamented quest plates belong to the quest NPC windows.
        var button=new Button {Text=text,FocusMode=FocusModeEnum.None,ClipText=true};
        ClassicReportDesign.StyleButton(button,false,Plugin.Kit.Layout("co_change_bill_us").Find("btn_ok")!);
        button.AddThemeStyleboxOverride("focus",new StyleBoxEmpty());
        foreach(string state in new[]{"font_color","font_hover_color","font_pressed_color","font_hover_pressed_color","font_focus_color"}) button.AddThemeColorOverride(state,new Color("efd9b4"));
        button.AddThemeColorOverride("font_disabled_color",new Color("887e70"));
        button.AddThemeFontOverride("font",Plugin.Kit.Bold);button.AddThemeFontSizeOverride("font_size",12);
        button.CustomMinimumSize=new Vector2(0,24);
        return button;
    }
    public override void _UnhandledKeyInput(InputEvent ev)
    {
        if(!IsVisibleInTree() || ev is not InputEventKey {Pressed:true,Echo:false,Keycode:Key.Escape}) return;
        Visible=false;GetViewport().SetInputAsHandled();
    }
}
