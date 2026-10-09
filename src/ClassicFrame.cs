using Godot;
namespace KnightOnlineUiClassic;

/// <summary>
/// Shared Classic window frame. The default is the original message-box border (ui_message_us): one thin
/// straight line, identical on all four sides, for every service, market, mail and event window. Only the
/// quest NPC windows set <see cref="Ornate"/> and keep the 1298 quest-talk corners and edges.
/// </summary>
public partial class ClassicFrame : Control
{
    /// <summary>Width of the plain border band (line plus its dark inner shadow).</summary>
    public const float PlainEdge = 8;
    /// <summary>Even inner padding of the plain frame family: content keeps 16 px from every outer edge.</summary>
    public const float Inset = 16;
    /// <summary>Gold window title on the content's left edge, level with the close button.</summary>
    public static Rect2 Title(float width) => new(Inset, 14, width - Inset * 2 - 32, 24);
    /// <summary>Round close button 16 px from the right edge, centred on the title row.</summary>
    public static Rect2 Close(float width) => new(width - Inset - 24, 14, 24, 24);
    public float BackgroundAlpha { get; set; } = 0.86f;
    public Color BackgroundColor { get; set; } = new(0.025f,0.022f,0.018f);
    public bool Ornate { get; set; }
    public ClassicFrame() { MouseFilter=MouseFilterEnum.Ignore; Resized+=QueueRedraw; }
    public override void _Draw()
    {
        if(Ornate) DrawOrnate(); else DrawPlain();
    }
    private void DrawPlain()
    {
        var texture=Plugin.Kit.Texture("ui_message_us.png");
        if(texture==null) return;
        const float e=PlainEdge;
        float w=Size.X,h=Size.Y;
        DrawRect(new Rect2(3,3,w-6,h-6),new Color(BackgroundColor,BackgroundAlpha));
        // The original message box is a 129 px square (x 330..458, y 2..130); its 8 px corners and straight
        // runs are repeated at 1:1 so every side keeps the same 3 px line at any window size.
        DrawTextureRectRegion(texture,new Rect2(0,0,e,e),new Rect2(330,2,e,e));
        DrawTextureRectRegion(texture,new Rect2(w-e,0,e,e),new Rect2(451,2,e,e));
        DrawTextureRectRegion(texture,new Rect2(0,h-e,e,e),new Rect2(330,123,e,e));
        DrawTextureRectRegion(texture,new Rect2(w-e,h-e,e,e),new Rect2(451,123,e,e));
        Edge(texture,new Rect2(e,0,w-e*2,e),new Rect2(338,2,56,e),56,false);
        Edge(texture,new Rect2(e,h-e,w-e*2,e),new Rect2(338,123,56,e),56,false);
        Edge(texture,new Rect2(0,e,e,h-e*2),new Rect2(330,10,e,56),56,true);
        Edge(texture,new Rect2(w-e,e,e,h-e*2),new Rect2(451,10,e,56),56,true);
    }
    private void DrawOrnate()
    {
        var texture=Plugin.Kit.Texture("ui_quest_us.png");
        if(texture==null) return;
        const float edge=42;
        float w=Size.X,h=Size.Y;
        DrawRect(new Rect2(4,4,w-8,h-8),new Color(BackgroundColor,BackgroundAlpha));
        void Slice(Rect2 dst,Rect2 src)=>DrawTextureRectRegion(texture,dst,src);
        Slice(new Rect2(0,0,edge,edge),new Rect2(0,0,edge,edge));
        Slice(new Rect2(w-edge,0,edge,edge),new Rect2(320,0,edge,edge));
        Slice(new Rect2(0,h-edge,edge,edge),new Rect2(0,269,edge,edge));
        Slice(new Rect2(w-edge,h-edge,edge,edge),new Rect2(320,269,edge,edge));
        // Up to the strip length an edge is drawn once (fitted, never enlarged). Longer edges repeat a
        // seamless run of the same strip at 1:1, so wide and tall windows show the original stone grain.
        Edge(texture,new Rect2(edge,0,w-edge*2,6),new Rect2(48,0,260,6),267,false);
        Edge(texture,new Rect2(edge,h-8,w-edge*2,8),new Rect2(48,303,260,8),236,false);
        Edge(texture,new Rect2(0,edge,7,h-edge*2),new Rect2(0,60,7,160),139,true);
        Edge(texture,new Rect2(w-7,edge,7,h-edge*2),new Rect2(355,60,7,160),139,true);
    }
    private void Edge(Texture2D texture,Rect2 dst,Rect2 src,float period,bool vertical)
    {
        float length=vertical ? dst.Size.Y : dst.Size.X, strip=vertical ? src.Size.Y : src.Size.X;
        if(length<=0) return;
        // The ornate strips are fitted when short (never enlarged); the plain line is cut, never squeezed.
        if(length<=strip && Ornate) { DrawTextureRectRegion(texture,dst,src); return; }
        for(float offset=0;offset<length;offset+=period)
        {
            float run=Math.Min(period,length-offset);
            if(vertical)
                DrawTextureRectRegion(texture,new Rect2(dst.Position.X,dst.Position.Y+offset,dst.Size.X,run),new Rect2(src.Position.X,src.Position.Y,src.Size.X,run));
            else
                DrawTextureRectRegion(texture,new Rect2(dst.Position.X+offset,dst.Position.Y,run,dst.Size.Y),new Rect2(src.Position.X,src.Position.Y,run,src.Size.Y));
        }
    }
}
