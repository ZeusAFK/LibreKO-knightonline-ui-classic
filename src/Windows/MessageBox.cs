using Godot;
using KnightOnlineUiClassic.Layout;
using LibreKO.Plugins;
using Spec = KnightOnlineUiClassic.Layout.ClassicOverlayLayout.Message;

namespace KnightOnlineUiClassic.Windows;

public partial class MessageBox : Control
{
    private const string OkLayout = "{nation}_messagebox_us";
    private const string OkCancelLayout = "{nation}_messagebox_us";
    private const string MessageId = "Text_Message";
    private static readonly Color Dim = new(0, 0, 0, 0.45f);

    private readonly DialogRequest _request;
    private readonly LayoutView _view;
    private readonly ClassicMessageArt _art;

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
        // The frame is drawn at the fitted height; the layout keeps the title, message and button places.
        foreach (var (node, control) in _view.Where(n => n.IsImage && _view.Root.Children.Contains(n))) control.Visible = false;
        _art = new ClassicMessageArt { Name = "message_art", Size = _view.Root.SizeVec };
        _view.AddChild(_art); _view.MoveChild(_art, 0);
        _view.Hide("btn_yes","btn_no","Edit_Common");
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

    // Sizes set before the box entered the tree were clamped to stale minimums; fit again inside it.
    public override void _Ready() { FitMessage(); Center(); }

    public override void _EnterTree()
    {
        GetViewport().SizeChanged += Center;
        Center();
    }

    public override void _ExitTree() { GetViewport().SizeChanged -= Center; _request.MessageChanged-=UpdateMessage; }
    private void UpdateMessage(string text) { _view.SetText(MessageId,text); FitMessage(); if(IsInsideTree()) Center(); }

    // The box fits its content with the shared message rhythm (title band, message, buttons): a short notice
    // has no empty band, and a long one lengthens through the art's middle band instead of overlapping.
    private void FitMessage()
    {
        if(_view.Get(MessageId) is not Label message) return;
        var root=_view.Root;
        var field=root.Find(MessageId)!;
        float text=ClassicWorldOverlays.Wrapped(message,field.W);
        bool buttons=_view.Get("btn_ok") is {Visible:true} || _view.Get("btn_cancel") is {Visible:true};
        float height=Spec.HeightFor(text,buttons);
        message.Position=new Vector2(field.X,Spec.TextTop);message.Size=new Vector2(field.W,text);
        foreach(var id in new[]{"btn_ok","btn_cancel"})
            if(_view.Get(id) is {} button) button.Position=new Vector2(button.Position.X,height-Spec.ButtonBottom-Spec.Button.Y);
        _art.Size=new Vector2(root.W,height);
        // Minimum first: a smaller size would otherwise be clamped to the previous minimum.
        _view.CustomMinimumSize=new Vector2(root.W,height);_view.Size=_view.CustomMinimumSize;
    }

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
