using Godot;
using KnightOnlineUiClassic.Layout;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The Info (combat) log: the client's lines in the Classic chat look, with the client's log tabs (3277df5
/// <c>TabbedLog</c>: All, Status, Item, System) as Classic chat tabs on the footer row before the "Info" plate. A
/// tab shows the lines of its kind from the client's history; the client keeps every line and its ink.
/// </summary>
public partial class LogWindow : Control
{
    private static readonly Vector2 DefaultSize = new(420, 205);
    private static readonly Vector2 Minimum = new(290, 135);
    /// <summary>The client's log tabs: All, then one per <see cref="GameLogKind"/>.</summary>
    public static readonly (string Name, GameLogKind? Kind)[] Tabs =
        { ("All", null), ("Status", GameLogKind.Status), ("Item", GameLogKind.Item), ("System", GameLogKind.System) };
    /// <summary>Tab width: up to <see cref="TabWidth"/>, narrower when the window is resized down (the Info plate keeps its 64 px).</summary>
    public const int TabWidth = 60, TitleWidth = 64;
    private readonly PluginGame _game = Plugin.Kit.Game;
    private readonly LogText _log;
    private readonly ClassicScrollRail _rail;
    private readonly Panel _background;
    private readonly Panel _title;
    private readonly Panel _footer;
    private readonly List<Panel> _tabs = new();
    private GameLogKind? _shown;
    private readonly Control _surface = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly ClassicChatTransparency _transparency;
    private HudLayout? _layout;

    public LogWindow()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Nearest;
        Size = DefaultSize;
        CustomMinimumSize = Minimum;
        AddChild(_surface);
        _surface.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _background = ClassicChatControls.Backdrop();
        _surface.AddChild(_background);
        _log = new LogText(13, Colors.White, Plugin.Kit.ChatFont);
        _surface.AddChild(_log);
        _rail = new ClassicScrollRail(_log);
        _surface.AddChild(_rail);
        _footer = ClassicChatControls.Frame();
        _surface.AddChild(_footer);
        _title = ClassicChatControls.Title("Info");
        _surface.AddChild(_title);
        var group = new ButtonGroup();
        foreach (var (name, kind) in Tabs)
        {
            var tab = ClassicChatControls.Tab(name);
            tab.Name = "tab_" + name.ToLowerInvariant();
            tab.ToggleMode = true;
            tab.ButtonGroup = group;
            tab.ButtonPressed = kind == null;
            tab.TooltipText = kind == null ? "All lines" : name + " lines";
            tab.Pressed += () => Show(kind);
            var frame = ClassicChatControls.TabFrame(tab);
            _tabs.Add(frame);
            _surface.AddChild(frame);
        }
        _transparency = new ClassicChatTransparency(_surface, _title, "log.transparency_step");
        ClassicChatControls.SnapToPixels(this);
        Resized += Arrange;
        Arrange();
    }

    public override void _EnterTree()
    {
        _game.Log.LineAdded += OnLine;
        _game.BecameAvailable += Reload;
        Config.EffectsChanged += ApplyVisibility;
        _layout ??= NativeLayout.Attach(this, "classic_log", _title, DefaultPosition,
            resizable: true, defaultSize: DefaultSize, minimumSize: Minimum, resizeCorner: HudLayout.Corner.TopLeft,
            backgroundOpacityChanged: _ => _transparency.Cycle(),legacyResizeGrip:true, resizeSnapPeerId:"classic_chat");
        Reload();
        ApplyVisibility();
    }

    public override void _ExitTree()
    {
        _game.Log.LineAdded -= OnLine;
        _game.BecameAvailable -= Reload;
        Config.EffectsChanged -= ApplyVisibility;
    }

    private void ApplyVisibility() => Visible = Config.CombatLog;

    /// <summary>The shown tab's kind (null for All).</summary>
    public GameLogKind? Shown => _shown;

    /// <summary>Shows the lines of one kind, or every line.</summary>
    public void Show(GameLogKind? kind)
    {
        _shown = kind;
        Reload();
    }

    private bool Matches(GameLogLine line) => _shown == null || line.Kind == _shown;

    private void Arrange()
    {
        float footer = Mathf.Round(Size.Y) - 24;
        _background.Size = new Vector2(Size.X, footer);
        _footer.Position = new Vector2(0, footer);
        _footer.Size = new Vector2(Size.X, 24);
        float tab = Mathf.Min(TabWidth, Mathf.Floor((Size.X - TitleWidth) / _tabs.Count));
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Position = new Vector2(i * tab, footer);
            _tabs[i].Size = new Vector2(tab, 24);
        }
        _title.Position = new Vector2(Size.X - TitleWidth, footer);
        _title.Size = new Vector2(TitleWidth, 24);
        _log.Position = new Vector2(4, 4);
        _log.Size = new Vector2(Size.X - 28, footer - 8);
        _rail.Position = new Vector2(Size.X - 21, 4);
        _rail.Size = new Vector2(18, footer - 8);
    }

    private Vector2 DefaultPosition()
    {
        var room = GetViewport().GetVisibleRect().Size;
        return new Vector2(room.X - Size.X - 8, room.Y - Taskbar.BarHeight - Size.Y - 4);
    }

    private void Reload() => _log.Set(_game.Log.History.Where(Matches).Select(Format));
    private void OnLine(GameLogLine line) { if (Matches(line)) _log.Append(Format(line)); }
    private static string Format(GameLogLine line) =>
        $"[color=#{line.Color.ToHtml(false)}]{line.Text.Replace("[", "[lb]")}[/color]";
}
