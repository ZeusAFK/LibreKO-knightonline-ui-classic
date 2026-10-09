using Godot;
using KnightOnlineUiClassic.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Windows;

public partial class MessageBox : Control
{
    private const string OkLayout = "{nation}_messagebox_us";
    private const string OkCancelLayout = "{nation}_messagebox_us";
    private const string MessageId = "Text_Message";
    private static readonly Color Dim = new(0, 0, 0, 0.45f);

    private readonly DialogRequest _request;
    private readonly LayoutView _view;

    public MessageBox(DialogRequest request)
    {
        _request = request;
        var kit = Plugin.Kit;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var blocker = new ColorRect { Color = Dim, MouseFilter = MouseFilterEnum.Stop };
        blocker.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(blocker);

        _view = new LayoutView(kit, kit.Layout(request.HasCancel ? OkCancelLayout : OkLayout));
        AddChild(_view);
        _view.Hide("btn_yes","btn_no");
        _view.SetText("Text_title",request.Title);
        if (!request.HasCancel) _view.Hide("btn_cancel");
        foreach(var id in new[]{"btn_ok","btn_cancel"})
        {
            if(_view.Get(id) is not Control button) continue;
            foreach(var label in button.GetChildren().OfType<Label>()) label.Visible=false;
            if(request.HasCancel) button.Position=new Vector2(id=="btn_ok" ? 57:173,button.Position.Y);
            _view.Caption(id,id=="btn_ok" ? request.ConfirmText : request.CancelText ?? "Cancel",12,Colors.White);
        }
        _view.SetText(MessageId, request.Message);
        if (_view.Get(MessageId) is Label msg)
        {
            msg.HorizontalAlignment = HorizontalAlignment.Center;
            msg.VerticalAlignment = VerticalAlignment.Center;
            msg.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }
        _view.OnPressed("btn_ok", () => { if (request.HasCancel) request.Confirm(); else request.Dismiss(); });
        _view.OnPressed("btn_cancel", request.Cancel);
        if (!request.Dismissable && _view.Get("btn_ok") is { } ok) ok.Visible = false;
        request.MessageChanged += UpdateMessage;
        FitMessage();
    }

    public override void _EnterTree()
    {
        GetViewport().SizeChanged += Center;
        Center();
    }

    public override void _ExitTree() { GetViewport().SizeChanged -= Center; _request.MessageChanged-=UpdateMessage; }
    private void UpdateMessage(string text) { _view.SetText(MessageId,text); FitMessage(); if(IsInsideTree()) Center(); }

    // A long message lengthens the original frame through its middle band (the rows the
    // native art already stretches) instead of running over the title or the buttons.
    private readonly Dictionary<Control,(Vector2 Position,Vector2 Size)> _declared=new();
    private void FitMessage()
    {
        if(_view.Get(MessageId) is not Label message) return;
        var root=_view.Root;
        var field=root.Find(MessageId)!;
        if(_declared.Count==0)
            foreach(var node in root.Children)
                if(_view.ControlOf(node) is {} control) _declared[control]=(control.Position,control.Size);
        message.Size=new Vector2(field.W,field.H);
        float extra=Mathf.Max(0,Mathf.Ceil(message.GetMinimumSize().Y-field.H));
        foreach(var node in root.Children)
        {
            if(_view.ControlOf(node) is not {} control || !_declared.TryGetValue(control,out var declared)) continue;
            control.Position=declared.Position;control.Size=declared.Size;
            if(node.Type=="image" && node.Y==MiddleBandY) control.Size+=new Vector2(0,extra);
            else if(node.Y>MiddleBandY) control.Position+=new Vector2(0,extra);
        }
        message.Size=new Vector2(field.W,field.H+extra);
        _view.CustomMinimumSize=_view.Size=root.SizeVec+new Vector2(0,extra);
    }
    private const int MiddleBandY=64;

    private void Center()
    {
        var room = GetViewport().GetVisibleRect().Size;
        _view.Position = ((room - _view.Size) * 0.5f).Round();
    }

    public override void _Input(InputEvent ev)
    {
        if (!_request.Dismissable || ev is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode is Key.Enter or Key.KpEnter)
        {
            GetViewport().SetInputAsHandled();
            if (_request.HasCancel) _request.Confirm(); else _request.Dismiss();
        }
        else if (k.Keycode == Key.Escape)
        {
            GetViewport().SetInputAsHandled();
            if (_request.HasCancel) _request.Cancel(); else _request.Dismiss();
        }
    }
}
