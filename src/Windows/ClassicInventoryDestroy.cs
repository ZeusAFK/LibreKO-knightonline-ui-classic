using Godot;
using KnightOnlineUiClassic.Layout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The client's bulk destroy queue as the Classic prompt drives it: the queued items, adding a dropped
/// item (through the client's checks), keeping one, and confirming or cancelling the whole queue.
/// </summary>
public sealed record ClassicDestroyQueue(
    Func<IReadOnlyList<int>> Slots,
    Func<int, bool> Add,
    Action<int> Keep,
    Action Confirm,
    Action Cancel,
    Action Tick);

/// <summary>
/// The original inventory destruction artwork with declarative controls and native validation. With the
/// client's destroy queue it stays open while more items are dropped on it: two or more queued items show
/// in a strip of the same message art above the question, and a click on one keeps it.
/// </summary>
public partial class ClassicInventoryDestroy : CanvasLayer
{
    public const int StripCells = 6;
    public const float StripCell = 32, StripGap = 2, StripPad = 4, StripLift = 3;
    private const string SingleText = "Would you like to destroy this item?";

    private readonly LayoutView _view;
    private readonly Action _confirm;
    private readonly ClassicDestroyQueue? _queue;
    private readonly Control? _strip;
    private readonly HBoxContainer? _stripCells;
    private readonly Vector2 _band;
    private int[] _shown = Array.Empty<int>();
    private bool _finished;

    public ClassicInventoryDestroy(LayoutView inventory, Action confirm) : this(inventory, confirm, null) { }

    public ClassicInventoryDestroy(LayoutView inventory, ClassicDestroyQueue queue) : this(inventory, queue.Confirm, queue) { }

    private ClassicInventoryDestroy(LayoutView inventory, Action confirm, ClassicDestroyQueue? queue)
    {
        Layer=221;_confirm=confirm;_queue=queue;
        // A single destruction is modal; the queue stays open so more items can be dropped on it.
        if (queue == null)
        {
            var blocker=new ColorRect {Color=Colors.Transparent,MouseFilter=Control.MouseFilterEnum.Stop};
            blocker.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);AddChild(blocker);
        }
        var source=Plugin.Kit.Layout("{nation}_inventory_us");
        var original=source.Find("area_samma")!;
        var frame=original.Find("img_Destroy")!;
        var layout=new LayoutNode {Type="base",Id="inventory_destroy",W=frame.W,H=frame.H};
        foreach(var child in original.Children)
        {
            var copy=CharacterLayout.CopyImage(child,-frame.X,-frame.Y);
            if(copy.Type=="string") {copy.Id="string_Destroy";copy.Text=child.Text.Trim();copy.Color=child.Color;copy.Font=child.Font;copy.Size=child.Size;copy.Bold=child.Bold;
                // The question is one line in both nations; Karus' narrower rectangle must not wrap it.
                copy.Style=child.Style|TextStyle.SingleLine|TextStyle.AlignVCenter;}
            layout.Children.Add(copy);
        }
        // Karus Cancel artwork sits 2 px above OK; share the OK baseline.
        var ok=layout.Children.FirstOrDefault(n=>n.Id=="btn_Destroy_ok");
        var cancel=layout.Children.FirstOrDefault(n=>n.Id=="btn_Destroy_cancel");
        if(ok!=null && cancel!=null && cancel.Y!=ok.Y)
        {
            int dy=ok.Y-cancel.Y;
            foreach(var node in cancel.All()) node.Y+=dy;
        }
        _view=new LayoutView(Plugin.Kit,layout);AddChild(_view);
        _view.Position=inventory.GlobalPosition+new Vector2(frame.X-source.X,frame.Y-source.Y);
        _band=new Vector2(frame.W,frame.H);
        _view.OnPressed("btn_Destroy_ok",()=>Finish(true));
        _view.OnPressed("btn_Destroy_cancel",()=>Finish(false));
        if (queue == null) return;
        _view.AddChild(new DropTarget {Name="destroy_drop",Size=_band,MouseFilter=Control.MouseFilterEnum.Pass,OnDrop=slot=>queue.Add(slot)});
        _view.MoveChild(_view.GetChild(_view.GetChildCount()-1),0);
        // The strip reuses the question's own message art, so both read as one prompt.
        _strip=new Control {Name="destroy_strip",Visible=false,MouseFilter=Control.MouseFilterEnum.Ignore};
        var art=layout.Children.First(n=>n.Id=="img_Destroy");
        var fill=new TextureRect {Texture=_view.Atlas(art),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=Control.MouseFilterEnum.Ignore};
        fill.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);_strip.AddChild(fill);
        _strip.AddChild(new DropTarget {Name="destroy_strip_drop",MouseFilter=Control.MouseFilterEnum.Pass,OnDrop=slot=>queue.Add(slot)});
        ((Control)_strip.GetChild(1)).SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _stripCells=new HBoxContainer {Name="destroy_strip_cells",Position=new Vector2(StripPad,StripPad),MouseFilter=Control.MouseFilterEnum.Ignore};
        _stripCells.AddThemeConstantOverride("separation",(int)StripGap);_strip.AddChild(_stripCells);
        _view.AddChild(_strip);
    }

    public override void _Ready()
    {
        _view.ApplyDeclaredBounds();
        if (_queue != null) Sync();
    }

    public override void _Process(double delta)
    {
        if (_queue == null || _finished) return;
        _queue.Tick();
        Sync();
    }

    /// <summary>Follows the client's queue: the question's wording and the strip of queued items.</summary>
    private void Sync()
    {
        var slots=_queue!.Slots();
        if (slots.Count == 0) { Close(); return; }
        if (slots.SequenceEqual(_shown)) return;
        _shown=slots.ToArray();
        _view.SetText("string_Destroy",slots.Count==1?SingleText:$"Would you like to destroy {slots.Count} items?");
        _strip!.Visible=slots.Count>1;
        foreach (var child in _stripCells!.GetChildren()) { _stripCells.RemoveChild(child); child.QueueFree(); }
        int shown=Math.Min(slots.Count,StripCells);
        for (int i=0;i<shown;i++)
        {
            int extra=i==StripCells-1 ? slots.Count-StripCells : 0;
            _stripCells.AddChild(Cell(slots[i],extra));
        }
        float width=shown*StripCell+(shown-1)*StripGap+StripPad*2, height=StripCell+StripPad*2;
        _strip.Size=new Vector2(width,height);
        _strip.Position=new Vector2(Mathf.Round((_band.X-width)/2),-height-StripLift);
    }

    private Control Cell(int slot,int extra)
    {
        var item=Plugin.Kit.Game.Inventory.At(slot);
        var cell=new Control {Name="destroy_item_"+slot,CustomMinimumSize=new Vector2(StripCell,StripCell),MouseFilter=Control.MouseFilterEnum.Stop,
            TooltipText=extra>0?$"{extra} more":"",MouseDefaultCursorShape=Control.CursorShape.PointingHand};
        var icon=new TextureRect {Texture=item.Icon,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode=TextureRect.StretchModeEnum.Scale,MouseFilter=Control.MouseFilterEnum.Ignore};
        icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);cell.AddChild(icon);
        string count=extra>0?$"+{extra}":item.Count>1?item.Count.ToString():"";
        if (count.Length>0)
        {
            var label=new Label {Text=count};cell.AddChild(label);
            ItemCountStyle.Apply(label,cell,Plugin.Kit.Bold);
        }
        cell.MouseEntered+=()=>{ if (extra==0) Plugin.Kit.Game.Inventory.ShowTooltip(slot); };
        cell.MouseExited+=()=>Plugin.Kit.Game.Inventory.HideTooltip();
        // A click keeps the item: it leaves the queue.
        cell.GuiInput+=ev=>
        {
            if (ev is not InputEventMouseButton {Pressed:true,ButtonIndex:MouseButton.Left} || extra>0) return;
            Plugin.Kit.Game.Inventory.HideTooltip();
            _queue!.Keep(slot);
            cell.AcceptEvent();
        };
        return cell;
    }

    /// <summary>Adds a further item, as dropping it on the prompt does.</summary>
    public bool Add(int slot) => _queue?.Add(slot) == true;

    private void Finish(bool accept)
    {
        if(_finished) return;_finished=true;Visible=false;
        Plugin.Kit.Game.Inventory.HideTooltip();
        if(accept) _confirm();
        else _queue?.Cancel();
        QueueFree();
    }

    private void Close()
    {
        if (_finished) return;
        _finished=true;Visible=false;QueueFree();
    }

    public override void _Input(InputEvent ev)
    {
        if(!Visible || ev is not InputEventKey {Pressed:true,Echo:false} key) return;
        if(key.Keycode is Key.Enter or Key.KpEnter) Finish(true);
        else if(key.Keycode==Key.Escape) Finish(false);
        else return;
        GetViewport().SetInputAsHandled();
    }
}
