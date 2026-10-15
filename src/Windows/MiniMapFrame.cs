using Godot;
using KnightOnlineUiClassic.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Windows;

public partial class MiniMapFrame : Control
{
    public const float FrameWidth = 255f;
    public const float FrameHeight = 240f;
    private const string LayoutName = "co_minimap_us";
    private const string MapArea = "Img_MiniMap";
    private const string BandId = "Img_StateBar";
    private const string ZoneText = "str_zoneid";
    private const string PositionText = "Text_Position";
    private const string MinimiseButton = "btn_minimize";
    private const string MaximiseButton = "btn_maximize";
    private const string LayoutId = "theme_minimap";
    private const float ScreenMargin = 8f;
    private static readonly string[] HiddenIds =
    {
        "Group_WarMap", "Group_Battle", "Group_Toggle", "Group_Mark", "Group_Note", "Group_NpcList", "scroll_Alpah_line",
        "btn_battle_p", "btn_battle_n", "Btn_WarMap", "btn_NpcListopen", "area_note",
    };
    private static readonly string[] BandIds = { BandId, ZoneText, PositionText, MinimiseButton, MaximiseButton };

    private readonly LayoutView _view;
    private readonly LayoutNode _layout;
    private readonly PluginGame _game;
    private readonly MapCanvas _canvas;
    private HudLayout? _hudLayout;
    private bool _collapsed;

    public MiniMapFrame()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _layout = kit.Layout(LayoutName);
        _view = new LayoutView(kit, _layout);
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(FrameWidth, FrameHeight);
        AddChild(_view);
        _view.Hide(HiddenIds);

        var area = _view.Get(MapArea);
        _canvas = new MapCanvas(_game) { Position = area?.Position ?? Vector2.Zero, Size = area?.Size ?? new Vector2(203, 199) };
        (area?.GetParent() ?? (Node)_view).AddChild(_canvas);
        if (area != null) _canvas.GetParent().MoveChild(_canvas, area.GetIndex() + 1);

        _view.OnPressed("Btn_ZoomIn", () => _canvas.Zoom(-1));
        _view.OnPressed("Btn_ZoomOut", () => _canvas.Zoom(1));
        _view.OnPressed("btn_globalmap", () => _game.Windows.Toggle("globalmap"));
        _view.OnPressed("btn_battle_a", _canvas.ToggleBlips);
        _view.OnPressed(MinimiseButton, () => SetCollapsed(true));
        _view.OnPressed(MaximiseButton, () => SetCollapsed(false));
        if (_view.Get(BandId) is { } band) band.MouseFilter = MouseFilterEnum.Stop;
        SetCollapsed(false);
        Refresh();
    }

    public override void _EnterTree()
    {
        _game.Map.Changed += Refresh;
        _game.BecameAvailable += Refresh;
        _hudLayout ??= HudLayout.Attach(this, LayoutId, _view.Get(BandId), DefaultPosition);
        Refresh();
    }

    public override void _ExitTree()
    {
        _game.Map.Changed -= Refresh;
        _game.BecameAvailable -= Refresh;
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return new Vector2(room.X - FrameWidth - ScreenMargin, ScreenMargin);
    }

    private void SetCollapsed(bool collapsed)
    {
        _collapsed = collapsed;
        foreach (var child in _layout.Children)
        {
            if (_view.ControlOf(child) is not { } control) continue;
            control.Visible = BandIds.Contains(child.Id) || !collapsed;
        }
        _canvas.Visible = !collapsed;
        if (!collapsed) _view.Hide(HiddenIds);
        if (_view.Get(MinimiseButton) is { } minimise) minimise.Visible = !collapsed;
        if (_view.Get(MaximiseButton) is { } maximise) maximise.Visible = collapsed;
    }

    private void Refresh()
    {
        var m = _game.Map;
        _view.SetText(ZoneText, m.ZoneName);
        _view.SetText(PositionText, $"{m.X:0}, {m.Z:0}");
        _canvas.QueueRedraw();
    }
}

public partial class MapCanvas : Control
{
    private static readonly float[] ZoomRadii = { 90f, 140f, 220f, 340f };
    private const int DefaultZoom = 1;
    private static readonly Color Background = new(0.03f, 0.035f, 0.045f);
    private static readonly Color PlayerColor = new(1f, 0.95f, 0.6f);
    private const float PlayerArrow = 6f;
    /// <summary>NPC-finder spots (3277df5 <c>Blip.Diamond</c>) stay on the map edge when out of range, this far in (the client's 7 px).</summary>
    public const float DiamondInset = 7f;
    private static readonly Color DiamondEdge = new(0f, 0f, 0f, 0.8f);
    private Control? _markers;

    /// <summary>
    /// Where the NPC-finder diamonds are drawn instead of the map: a control over the frame art (the status bar's
    /// parchment rim covers the map's edge, where out-of-range spots wait).
    /// </summary>
    public Control? Markers
    {
        get => _markers;
        set
        {
            _markers = value;
            if (value != null) value.Draw += () => DrawDiamonds(value);
        }
    }

    private readonly PluginGame _game;
    private readonly MapArrow _arrow;
    private int _zoom = DefaultZoom;
    private bool _showBlips = true;

    public MapCanvas(PluginGame game)
    {
        _game = game;
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
        // The player arrow draws on its own item over the map, so turning the camera redraws only the arrow.
        _arrow = new MapArrow(game) { Name = "map_arrow", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_arrow);
        _arrow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public void ToggleBlips()
    {
        _showBlips = !_showBlips;
        QueueRedraw();
    }

    public void Zoom(int dir)
    {
        _zoom = Mathf.Clamp(_zoom + dir, 0, ZoomRadii.Length - 1);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (IsVisibleInTree()) _arrow.Follow();
    }

    /// <summary>
    /// One filled disc with the geometry <c>RenderingServer.canvas_item_add_circle</c> builds for <c>DrawCircle</c>: 65 rim
    /// points at i * TAU / 64 and the centre, triangles (centre, i, i + 1), unit radius and white. Each blip draws it
    /// scaled and moved, tinted with the blip colour, instead of building new GPU buffers per circle per redraw.
    /// </summary>
    private static ArrayMesh? _disc;
    private static ArrayMesh Disc()
    {
        if (_disc != null && GodotObject.IsInstanceValid(_disc)) return _disc;
        const int segments = 64;
        var points = new Vector2[segments + 2];
        points[segments + 1] = Vector2.Zero;
        float step = Mathf.Tau / segments;
        for (int i = 0; i < segments + 1; i++)
        {
            float angle = i * step;
            points[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
        var indices = new int[segments * 3];
        for (int i = 0; i < segments; i++) { indices[i * 3] = segments + 1; indices[i * 3 + 1] = i; indices[i * 3 + 2] = i + 1; }
        var colors = new Color[points.Length];
        Array.Fill(colors, Colors.White);
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = points;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        _disc = new ArrayMesh();
        _disc.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return _disc;
    }

    public override void _Draw()
    {
        var m = _game.Map;
        DrawRect(new Rect2(Vector2.Zero, Size), Background);
        float radius = ZoomRadii[_zoom];
        var tex = m.MapTexture;
        if (tex != null && m.WorldExtent > 0f)
        {
            float texPerWorld = tex.GetWidth() / m.WorldExtent;
            var centre = new Vector2(m.X * texPerWorld, (m.WorldExtent - m.Z) * texPerWorld);
            var srcSize = new Vector2(2f * radius * texPerWorld, 2f * radius * texPerWorld * (Size.Y / Size.X));
            var src = new Rect2(centre - srcSize * 0.5f, srcSize);
            DrawTextureRectRegion(tex, new Rect2(Vector2.Zero, Size), src);
        }
        float pxPerWorld = Size.X * 0.5f / radius;
        var mid = Size * 0.5f;
        if (_markers != null && GodotObject.IsInstanceValid(_markers)) _markers.QueueRedraw();
        else DrawDiamonds(this);
        ArrayMesh? disc = null;
        foreach (var b in _showBlips ? m.Blips : Array.Empty<MiniMap.Blip>())
        {
            if (b.Diamond) continue;
            var p = mid + new Vector2((b.X - m.X) * pxPerWorld, -(b.Z - m.Z) * pxPerWorld);
            if (p.X < 0 || p.Y < 0 || p.X > Size.X || p.Y > Size.Y) continue;
            if (b.Hollow) DrawArc(p, b.Radius, 0f, Mathf.Tau, 16, b.Color, 1.5f, true);
            else DrawMesh(disc ??= Disc(), null, new Transform2D(0f, new Vector2(b.Radius, b.Radius), 0f, p), b.Color);
        }
    }

    /// <summary>The player arrow, drawn over the map at its centre.</summary>
    private sealed partial class MapArrow : Control
    {
        private readonly PluginGame _game;
        private float _drawnHeading = float.NaN;
        public MapArrow() { _game = null!; }
        public MapArrow(PluginGame game) => _game = game;

        public void Follow()
        {
            if (!Mathf.IsEqualApprox(_drawnHeading, _game.Map.HeadingDegrees)) QueueRedraw();
        }

        public override void _Draw()
        {
            var mid = Size * 0.5f;
            _drawnHeading = _game.Map.HeadingDegrees;
            float heading = Mathf.DegToRad(_drawnHeading);
            var dir = new Vector2(Mathf.Sin(heading), -Mathf.Cos(heading));
            var side = new Vector2(-dir.Y, dir.X);
            DrawColoredPolygon(new[]
            {
                mid + dir * PlayerArrow,
                mid - dir * PlayerArrow * 0.7f + side * PlayerArrow * 0.6f,
                mid - dir * PlayerArrow * 0.7f - side * PlayerArrow * 0.6f,
            }, PlayerColor);
        }
    }

    /// <summary>
    /// The picked NPC kind's spots as gold diamonds, kept on the edge while out of range (the client's rule), drawn on
    /// <paramref name="canvas"/> (this map, or the <see cref="Markers"/> control laid over it at the same rectangle).
    /// </summary>
    private void DrawDiamonds(CanvasItem canvas)
    {
        var m = _game.Map;
        float pxPerWorld = Size.X * 0.5f / ZoomRadii[_zoom];
        var mid = Size * 0.5f;
        var low = new Vector2(DiamondInset, DiamondInset);
        foreach (var b in m.Blips)
        {
            if (!b.Diamond) continue;
            var at = (mid + new Vector2((b.X - m.X) * pxPerWorld, -(b.Z - m.Z) * pxPerWorld)).Clamp(low, Size - low);
            var points = new[] { at + new Vector2(0f, -b.Radius), at + new Vector2(b.Radius, 0f), at + new Vector2(0f, b.Radius), at + new Vector2(-b.Radius, 0f) };
            canvas.DrawColoredPolygon(points, b.Color);
            canvas.DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] }, DiamondEdge, 1f, true);
        }
    }
}
