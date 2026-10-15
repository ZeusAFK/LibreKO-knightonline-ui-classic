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

    public override void _Ready()
    {
        // The gutter is set up at once, as before; the row keeping then follows the list while it is shown.
        if (IsInstanceValid(_scroll)) Rows();
        ShownOnly.Gate(this, _scroll, Sync);
    }

    public override void _Process(double delta) => Sync();

    private void Sync()
    {
        if (!IsInstanceValid(_scroll)) return;
        var rows = Rows();
        var bar = _scroll.GetVScrollBar();
        if (_gutter != null)
        {
            int margin = (bar.Visible ? Mathf.CeilToInt(bar.Size.X) : 0) + _gap;
            if (_gutter.GetThemeConstant(HotNames.MarginRight) != margin) _gutter.AddThemeConstantOverride(HotNames.MarginRight, margin);
        }
        float gap = rows?.GetThemeConstant(HotNames.Separation) ?? 0;
        var first = rows?.GetChildren().OfType<Control>().FirstOrDefault(c => c.Visible && c is not Label && c.Size.Y > 0);
        Pitch = first != null ? first.Size.Y + gap : _fallback;
        float height = Mathf.Max(Pitch - gap, Mathf.Floor((_limit + gap) / Pitch) * Pitch - gap);
        if (Math.Abs(_scroll.CustomMinimumSize.Y - height) > .5f) _scroll.CustomMinimumSize = new Vector2(_scroll.CustomMinimumSize.X, height);
        bar.CustomStep = Pitch;
        double snapped = Math.Round(bar.Value / Pitch) * Pitch;
        if (!Input.IsMouseButtonPressed(MouseButton.Left) && Math.Abs(bar.Value - snapped) > .01) bar.Value = snapped;
    }
}

/// <summary>Godot names used every frame in this file, created once (a literal creates a finalizable wrapper per call).</summary>
file static class HotNames
{
    internal static readonly StringName MarginRight = "margin_right";
    internal static readonly StringName Separation = "separation";
}
