using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Keeps a native scrolled list to whole rows, as the other Classic lists do: the viewport is trimmed to a
/// whole number of rows (at most <c>limit</c>), scrolling steps and snaps by one row, and the rows end
/// <c>gap</c> pixels before the scroll bar (the native bar is drawn over the list's full width).
/// </summary>
public partial class ClassicRowScroll : Node
{
    private readonly ScrollContainer _scroll;
    private readonly float _limit, _fallback;
    private readonly int _gap;
    private MarginContainer? _gutter;
    public float Pitch { get; private set; }

    public ClassicRowScroll(ScrollContainer scroll, float limit, float pitch, int gap = 6)
    {
        _scroll = scroll; _limit = limit; _fallback = Pitch = pitch; _gap = gap; Name = "classic_row_scroll";
    }

    private BoxContainer? Rows()
    {
        if (_gutter == null && _scroll.GetChildCount() > 0 && _scroll.GetChild(0) is BoxContainer native)
        {
            _gutter = new MarginContainer { Name = "classic_row_gutter", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Pass };
            _scroll.AddChild(_gutter); native.Reparent(_gutter, false);
        }
        return _gutter?.GetChildCount() > 0 ? _gutter.GetChild(0) as BoxContainer : null;
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_scroll)) return;
        var rows = Rows();
        var bar = _scroll.GetVScrollBar();
        if (_gutter != null)
        {
            int margin = (bar.Visible ? Mathf.CeilToInt(bar.Size.X) : 0) + _gap;
            if (_gutter.GetThemeConstant("margin_right") != margin) _gutter.AddThemeConstantOverride("margin_right", margin);
        }
        float gap = rows?.GetThemeConstant("separation") ?? 0;
        var first = rows?.GetChildren().OfType<Control>().FirstOrDefault(c => c.Visible && c is not Label && c.Size.Y > 0);
        Pitch = first != null ? first.Size.Y + gap : _fallback;
        float height = Mathf.Max(Pitch - gap, Mathf.Floor((_limit + gap) / Pitch) * Pitch - gap);
        if (Math.Abs(_scroll.CustomMinimumSize.Y - height) > .5f) _scroll.CustomMinimumSize = new Vector2(_scroll.CustomMinimumSize.X, height);
        bar.CustomStep = Pitch;
        double snapped = Math.Round(bar.Value / Pitch) * Pitch;
        if (!Input.IsMouseButtonPressed(MouseButton.Left) && Math.Abs(bar.Value - snapped) > .01) bar.Value = snapped;
    }
}

/// <summary>
/// Keeps a native <see cref="ItemList"/> to whole rows: the list is as tall as a whole number of rows (the most
/// that fit in its native height, or exactly its rows when <c>fitRows</c> is set), and its scroll position
/// snaps to rows, so no row is ever cut at the bottom or the top.
/// </summary>
public partial class ClassicListRows : Node
{
    private readonly ItemList _list;
    private readonly bool _fit;
    private float _native = -1;

    public ClassicListRows() { _list = null!; }

    public ClassicListRows(ItemList list, bool fitRows = false) { _list = list; _fit = fitRows; Name = "classic_list_rows"; }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_list) || _list.ItemCount == 0) return;
        if (_native < 0) _native = _list.CustomMinimumSize.Y;
        float gap = _list.GetThemeConstant("v_separation");
        var first = _list.GetItemRect(0, false);
        float pitch = _list.ItemCount > 1 ? _list.GetItemRect(1, false).Position.Y - first.Position.Y : first.Size.Y + gap;
        if (pitch <= 1) return;
        float frame = _list.GetThemeStylebox("panel").GetMinimumSize().Y;
        // The list counts the row gap under every row, the last one included.
        int rows = _fit ? _list.ItemCount : Math.Max(1, (int)Math.Floor((_native - frame) / pitch));
        float height = rows * pitch + frame;
        if (Math.Abs(_list.CustomMinimumSize.Y - height) > .5f) _list.CustomMinimumSize = new Vector2(_list.CustomMinimumSize.X, height);
        var bar = _list.GetVScrollBar();
        double snapped = Math.Round(bar.Value / pitch) * pitch;
        if (!Input.IsMouseButtonPressed(MouseButton.Left) && Math.Abs(bar.Value - snapped) > .01) bar.Value = snapped;
    }
}
