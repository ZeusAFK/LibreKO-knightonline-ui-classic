using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Native;
using L = KnightOnlineUiClassic.Layout.ClassicMykoHudLayout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The client's Events window on the original "Daily Event Schedule" art (<c>re_*_scheduler</c>, read through LocalAssets).
/// The window stays the client's: it opens from E, the clock and the client's own paths, requests the schedule and
/// closes and moves as before. Its rows are shown in the art's ten-row table with the art's pager: first what runs
/// now (with the client's Join or Open as a row click), then the starts of the next 24 hours.
/// The client knows no server wall clock (the server's time packet carries the game's day cycle), and it writes
/// event starts in the player's local time, so the header shows the local time under that name.
/// </summary>
public static class ClassicMykoScheduler
{
    public const string PanelName = "classic_scheduler";

    public static bool Available => ClassicMykoArt.Layout(L.Scheduler) != null;

    /// <summary>Puts the Events window on the scheduler art; false (and nothing changed) without the local art.</summary>
    public static bool Apply(HudWindow window)
    {
        if (ClassicMykoArt.Layout(L.Scheduler) is not { } art) return false;
        window.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        foreach (var margin in window.GetChildren().OfType<MarginContainer>()) margin.Visible = false;
        window.AddChild(new ClassicSchedulerPanel(window, art));
        window.ResetSize();
        return true;
    }
}

public partial class ClassicSchedulerPanel : Control
{
    public const string Empty = "schedule_empty", Time = "txt_server_time", Page = "text_page", Previous = "btn_previously", Next = "btn_next", Close = "btn_close";
    private readonly HudWindow _window;
    private readonly LayoutView _view;
    private readonly List<(Control Base, TextureButton Button, Label Name, Label Time)> _rows = new();
    private readonly TextureButton _previous, _next, _close;
    private readonly Label _page, _time, _empty;
    private readonly ButtonGroup _group = new() { AllowUnpress = true };
    private List<NativeEventSchedule.Row> _shown = new();
    private object? _schedule;
    private string? _selected;
    private int _pageIndex;
    private double _tick;

    public ClassicSchedulerPanel() { _window = null!; _view = null!; _previous = _next = _close = null!; _page = _time = _empty = null!; }

    public ClassicSchedulerPanel(HudWindow window, LayoutNode art)
    {
        _window = window;
        Name = ClassicMykoScheduler.PanelName;
        CustomMinimumSize = art.SizeVec; Size = art.SizeVec;
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.Nearest;
        _view = ClassicMykoArt.View(art);
        AddChild(_view);

        // Header: the client knows the local time only (see the class summary).
        foreach (var (_, control) in _view.Where(n => n.IsString && n.Text == "Server Time :"))
            ((Label)control).Text = L.TimeCaption;
        _time = _view.Get<Label>(Time)!;
        _page = _view.Get<Label>(Page)!;
        _previous = _view.Get<TextureButton>(Previous)!;
        _next = _view.Get<TextureButton>(Next)!;
        _close = _view.Get<TextureButton>(Close)!;
        _close.TooltipText = "Close";
        foreach (var button in new[] { _previous, _next, _close }) button.MouseDefaultCursorShape = CursorShape.PointingHand;
        _previous.TooltipText = "Previous page"; _next.TooltipText = "Next page";
        _previous.Pressed += () => Turn(-1);
        _next.Pressed += () => Turn(1);
        _close.Pressed += CloseWindow;

        for (int i = 1; i <= L.ScheduleRows; i++)
        {
            var scope = art.Find("base_event" + i)!;
            var row = _view.ControlOf(scope)!;
            var button = _view.Get<TextureButton>(scope, "btn_event")!;
            var name = _view.Get<Label>(scope, "txt_event_name")!;
            var time = _view.Get<Label>(scope, "txt_event_time")!;
            // The row art lies under its texts (the original draws some rows' button last).
            row.MoveChild(button, 0);
            button.ToggleMode = true; button.ButtonGroup = _group;
            foreach (var label in new[] { name, time })
            {
                label.ClipText = true; label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                label.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            }
            name.AddThemeStyleboxOverride("normal", new StyleBoxEmpty { ContentMarginLeft = 3 });
            int index = i - 1;
            button.Pressed += () => Pick(index);
            _rows.Add((row, button, name, time));
        }

        var list = L.ScheduleList;
        _empty = new Label { Name = Empty, Position = list.Position, Size = list.Size, MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _view.Style(_empty, art.Find("base_event1")!.Find("txt_event_name")!);
        _empty.AddThemeColorOverride("font_color", L.Muted);
        AddChild(_empty);

        // The title strip moves the window through the client's own header handle.
        if (window.Header?.GetChildren().OfType<HBoxContainer>().FirstOrDefault() is { } bar)
            ClassicPartySkin.Drag(this, bar, L.SchedulerDrag);
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        _tick += delta;
        var world = NativeEventSchedule.WorldOf(_window);
        object? schedule = world == null ? null : NativeEventSchedule.Schedule(world);
        if (_tick < 1 && ReferenceEquals(schedule, _schedule)) return;
        _tick = 0;
        Refresh();
    }

    public IReadOnlyList<NativeEventSchedule.Row> Shown => _shown;
    public int PageIndex => _pageIndex;
    public int PageCount => Math.Max(1, (_shown.Count + L.ScheduleRows - 1) / L.ScheduleRows);

    public void Refresh()
    {
        _time.Text = DateTime.Now.ToString("HH:mm");
        var world = NativeEventSchedule.WorldOf(_window);
        var schedule = world == null ? null : NativeEventSchedule.Schedule(world);
        _schedule = schedule;
        _shown = schedule == null ? new() : NativeEventSchedule.Rows(schedule, DateTime.UtcNow);
        _pageIndex = Math.Clamp(_pageIndex, 0, PageCount - 1);
        _empty.Text = schedule == null ? "Loading the schedule..." : "No events in the next 24 hours.";
        _empty.Visible = _shown.Count == 0;
        int level = Plugin.Kit.Game.Available ? Plugin.Kit.Game.Character.Level : 0;
        for (int i = 0; i < _rows.Count; i++)
        {
            var (row, button, name, time) = _rows[i];
            int at = _pageIndex * L.ScheduleRows + i;
            row.Visible = at < _shown.Count;
            if (!row.Visible) continue;
            var entry = _shown[at];
            bool fits = level <= 0 || LibreKO.Domain.EventSchedule.Fits(level, entry.MinLevel, entry.MaxLevel);
            var ink = fits ? Colors.White : L.Muted;
            name.Text = entry.Name;
            name.AddThemeColorOverride("font_color", ink);
            time.Text = entry.Phase == null ? NativeEventSchedule.StartText(entry.StartUtc!.Value) : entry.Action ?? "Now";
            time.AddThemeColorOverride("font_color", entry.Phase == null ? ink : entry.Action != null ? L.Action : L.Running);
            string when = entry.Phase is { } phase
                ? $"{NativeEventSchedule.PhaseText(phase)}, {NativeEventSchedule.LeftText(entry.SecondsLeft)} left"
                : "Starts " + NativeEventSchedule.StartText(entry.StartUtc!.Value);
            button.TooltipText = $"{entry.Name}\n{NativeEventSchedule.LevelBand(entry.MinLevel, entry.MaxLevel)}\n{when}"
                + (entry.Action == "Join" ? "\nClick to join." : entry.Action == "Open" ? "\nClick to open." : "");
            button.MouseDefaultCursorShape = entry.Action != null ? CursorShape.PointingHand : CursorShape.Arrow;
            button.SetPressedNoSignal(Key(entry) == _selected);
        }
        _page.Text = $"{_pageIndex + 1} / {PageCount}";
        foreach (var (button, enabled) in new[] { (_previous, _pageIndex > 0), (_next, _pageIndex < PageCount - 1) })
        {
            button.Disabled = !enabled;
            button.SelfModulate = enabled ? Colors.White : new Color(0.55f, 0.55f, 0.55f);
        }
    }

    private static string Key(NativeEventSchedule.Row row) => row.Name + "|" + row.StartUtc + "|" + row.Phase;

    private void Turn(int step)
    {
        _pageIndex = Math.Clamp(_pageIndex + step, 0, PageCount - 1);
        Refresh();
    }

    /// <summary>Selects a row; a running entry with a client action (Join, Open) runs it like the client's button.</summary>
    private void Pick(int index)
    {
        int at = _pageIndex * L.ScheduleRows + index;
        if (at >= _shown.Count) return;
        var entry = _shown[at];
        _selected = _rows[index].Button.ButtonPressed ? Key(entry) : null;
        if (entry.Now is { } now && entry.Action != null && NativeEventSchedule.WorldOf(_window) is { } world) NativeEventSchedule.Run(world, now);
    }

    /// <summary>Closes through the client's own close button (the window keeps its close behaviour).</summary>
    private void CloseWindow()
    {
        var native = _window.Header?.GetChildren().OfType<HBoxContainer>().FirstOrDefault()?.GetChildren().OfType<Button>().FirstOrDefault(b => b.TooltipText == "Close");
        if (native != null) native.EmitSignal(BaseButton.SignalName.Pressed);
        else _window.Visible = false;
    }
}
