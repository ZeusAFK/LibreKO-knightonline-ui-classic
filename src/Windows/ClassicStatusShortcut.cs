using Godot;
using KnightOnlineUiClassic.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Windows;

public partial class ClassicStatusShortcut : BaseButton
{
    private readonly PluginGame _game;
    private readonly UiKit _kit;
    private readonly string _letter,_windowId,_title;
    private readonly string? _alert,_icon;
    private readonly Func<string>? _key;
    private readonly Font _countFont;
    private readonly Texture2D? _mailTexture;
    private static readonly Vector2I[] MailFrames = {
        new(7,115),new(40,115),new(7,138),new(41,138),
        new(41,138),new(7,138),new(40,115),new(7,115),
    };
    private int _pending=-1;
    private int _mailFrame;
    private bool _hovered,_lit;
    private double _refreshClock,_flashClock;
    /// <summary>The button's row identity (Q P A D G E R); the row draws icons, not letters.</summary>
    public string Letter => _letter;
    /// <summary>The <see cref="ClassicHudIcons"/> icon of a row button.</summary>
    public string? Icon => _icon;
    public string WindowId => _windowId;
    /// <summary>Whether the button flashes for the client's attention state (events, daily rank).</summary>
    public bool Lit => _pending>0;
    /// <summary>Whether this is a row button rather than the mail envelope.</summary>
    public bool HasBackground => _windowId!="mail";
    public Rect2 MailIconRect => new(0,1,28,20);

    /// <param name="press">Replaces the window toggle (Q opens a Character window page).</param>
    /// <param name="key">The client's current key binding for the tooltip, or an empty string.</param>
    /// <param name="alert">For an attention state instead of a count: no badge, this tooltip line while lit.</param>
    /// <param name="icon">The row icon (<see cref="ClassicHudIcons.Order"/>); null for the mail envelope.</param>
    public ClassicStatusShortcut(UiKit kit,LayoutNode source,string letter,string windowId,string title,
        Action? press=null,Func<string>? key=null,string? alert=null,string? icon=null)
    {
        _kit=kit;
        _game=kit.Game;
        _letter=letter;
        _windowId=windowId;
        _title=title;
        _key=key;
        _alert=alert;
        _icon=icon;
        if(windowId=="mail") _mailTexture=kit.Texture("classic_mail_chaos.png");
        _countFont=kit.Bold;
        Name="btn_"+windowId;
        Size=new Vector2(source.W,source.H);
        CustomMinimumSize=Size;
        FocusMode=FocusModeEnum.None;
        MouseFilter=MouseFilterEnum.Stop;
        TextureFilter=TextureFilterEnum.Nearest;
        TooltipText=title;
        Pressed+=press ?? (()=>_game.Windows.Toggle(_windowId));
        MouseEntered+=()=> { _hovered=true; QueueRedraw(); };
        MouseExited+=()=> { _hovered=false; QueueRedraw(); };
        ButtonDown+=QueueRedraw;
        ButtonUp+=QueueRedraw;
    }

    public override void _Ready() => SetPending(_game.Windows.NotificationCount(_windowId));

    public override string _GetTooltip(Vector2 atPosition)
    {
        string key=_key?.Invoke() ?? "";
        return key.Length==0 ? TooltipText : TooltipText.Replace(_title,$"{_title} ({key})");
    }

    public override void _Process(double delta)
    {
        _refreshClock+=delta;
        if(_refreshClock>=0.25)
        {
            _refreshClock%=0.25;
            SetPending(_game.Windows.NotificationCount(_windowId));
        }
        if(_pending<=0) return;
        _flashClock+=delta;
        if(_windowId=="mail")
        {
            const double frameTime=0.1;
            if(_flashClock<frameTime) return;
            int steps=(int)(_flashClock/frameTime);
            _flashClock%=frameTime;
            _mailFrame=(_mailFrame+steps)%MailFrames.Length;
            QueueRedraw();
            return;
        }
        // A gentle glow pulse instead of the old plate flash.
        _flashClock%=ClassicHudIcons.PulsePeriod;
        QueueRedraw();
    }

    private void SetPending(int count)
    {
        count=Math.Max(0,count);
        if(_pending==count) return;
        _pending=count;
        _flashClock=0;
        _mailFrame=0;
        _lit=count>0;
        TooltipText=count==0 ? _title:_alert!=null ? $"{_title} — {_alert}":_windowId=="mail"
            ? $"Mail — {count} unread"
            : $"{_title} — {count} reward{(count==1 ? "":"s")} to claim";
        QueueRedraw();
    }

    public override void _Draw()
    {
        if(_windowId=="mail")
        {
            DrawMailIcon();
            if(_pending>0) DrawBadge();
            return;
        }
        if(_icon!=null)
            ClassicHudIcons.DrawButton(this,_icon,ClassicHudIcons.StateOf(this,_hovered),
                _lit ? ClassicHudIcons.Pulse(_flashClock) : null);
        if(_pending<=0 || _alert!=null) return;
        DrawBadge();
    }

    private void DrawMailIcon()
    {
        if(_mailTexture==null) return;
        // Draw the complete native envelope at its original pixel size.
        var iconSize=MailIconRect.Size;
        var position=MailIconRect.Position;
        if(IsPressed()) position+=Vector2.One;
        var frame=MailFrames[_pending>0 ? _mailFrame : 0];
        var tint=Disabled ? new Color(0.55f,0.55f,0.55f) : Colors.White;
        DrawTextureRectRegion(_mailTexture,new Rect2(position,iconSize),
            new Rect2(frame.X,frame.Y,28,20),tint);
    }

    private void DrawBadge()
    {
        string text=_pending>9 ? "9+":_pending.ToString();
        var box=new Rect2(14,_windowId=="mail"?16:29,14,12);
        var edge=new Color(_kit.Nation==1 ? "c2b8b0":"b9a36f");
        DrawRect(box,new Color(0.04f,0.045f,0.05f,0.95f));
        DrawRect(box.Grow(-0.5f),edge,false,1);
        const int fontSize=9;
        float width=_countFont.GetStringSize(text,fontSize:fontSize).X;
        var pen=new Vector2(Mathf.Round(box.Position.X+(box.Size.X-width)/2),box.Position.Y+9);
        DrawString(_countFont,pen,text,fontSize:fontSize,modulate:new Color("fff1c8"));
    }
}
