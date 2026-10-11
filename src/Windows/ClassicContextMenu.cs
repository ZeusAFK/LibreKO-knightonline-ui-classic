using Godot;
using KnightOnlineUiClassic.Layout;
using Spec = KnightOnlineUiClassic.Layout.ClassicContextMenuLayout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>One row of a Classic context menu. A row without a caption and action is a separator.</summary>
public readonly record struct ClassicMenuRow(string Key, string Caption, Action? Run, bool Enabled = true)
{
    public static ClassicMenuRow Separator(string key) => new(key, "", null, false);
    public bool IsSeparator => Run == null && Caption.Length == 0;
}

/// <summary>
/// The one Classic context menu every right-click menu uses: the Classic menu panel (black, the gold line), an
/// optional title row (a player's name in the heading gold, an item's name in its grade colour) over a gold rule,
/// then one 22 px Arial Bold row per action with the selected-row hover; disabled rows are muted. It opens at the
/// cursor and stays on screen; a click outside or Escape closes it. Nodes are named <c>{prefix}_panel</c>,
/// <c>{prefix}_name</c>, <c>{prefix}_rule</c> and <c>{prefix}_{row key}</c>.
/// </summary>
public partial class ClassicContextMenu : Control
{
    /// <summary>Name of the canvas layer that holds an open menu.</summary>
    public const string LayerName = "classic_context_menu";
    /// <summary>Meta on a mirrored client menu: the title the next popup shows (set by the plugin before it opens the menu).</summary>
    public const string TitleMeta = "classic_menu_title";
    private const string MirroredMeta = "classic_menu_mirror";

    private readonly Action _close = null!;
    private readonly Vector2 _cursor;
    private readonly PanelContainer _panel = null!;

    public ClassicContextMenu() { }

    public ClassicContextMenu(string prefix, string? title, Color? titleInk, Vector2 cursor, IReadOnlyList<ClassicMenuRow> rows, Action close)
    {
        _close = close;
        _cursor = cursor;
        Name = prefix.Length > 0 ? "classic_" + prefix : "classic_context_menu";
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        GuiInput += ev => { if (ev is InputEventMouseButton { Pressed: true }) { _close(); AcceptEvent(); } };
        _panel = new PanelContainer { Name = prefix + "_panel", MouseFilter = MouseFilterEnum.Stop };
        _panel.AddThemeStyleboxOverride("panel", PanelBox());
        AddChild(_panel);
        var column = new VBoxContainer { Name = prefix + "_rows" };
        column.AddThemeConstantOverride("separation", 0);
        _panel.AddChild(column);

        float widest = 0;
        if (!string.IsNullOrWhiteSpace(title)) widest = TextWidth(title!, Spec.TitleSize);
        foreach (var row in rows) if (!row.IsSeparator) widest = Mathf.Max(widest, TextWidth(row.Caption, Spec.FontSize));
        float width = Spec.Width(widest);

        if (!string.IsNullOrWhiteSpace(title))
        {
            var name = new Label { Name = prefix + "_name", Text = title, MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(width, Spec.Row) };
            name.VerticalAlignment = VerticalAlignment.Center; name.ClipText = true; name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            ClassicSocialStyle.Font(name, true, Spec.TitleSize);
            name.AddThemeColorOverride("font_color", titleInk ?? ClassicDarkFrame.Heading);
            name.AddThemeStyleboxOverride("normal", new StyleBoxEmpty { ContentMarginLeft = Spec.Inset, ContentMarginRight = Spec.Inset });
            column.AddChild(name);
            column.AddChild(Rule(prefix + "_rule", Spec.RuleHeight));
        }
        foreach (var row in rows)
        {
            if (row.IsSeparator) { column.AddChild(Rule(prefix + "_" + row.Key, Spec.SeparatorHeight)); continue; }
            var button = new Button { Name = prefix + "_" + row.Key, Text = row.Caption, FocusMode = FocusModeEnum.None, Alignment = HorizontalAlignment.Left };
            button.CustomMinimumSize = new Vector2(width, Spec.Row);
            button.ClipText = true; button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            StyleRow(button);
            button.Disabled = !row.Enabled || row.Run == null;
            var run = row.Run;
            if (run != null) button.Pressed += () => run();
            column.AddChild(button);
        }
    }

    public override void _Ready() => Callable.From(Place).CallDeferred();

    /// <summary>Opens at the cursor and stays on screen, as the client's menus do.</summary>
    private void Place()
    {
        if (!IsInsideTree()) return;
        var size = _panel.GetCombinedMinimumSize();
        _panel.Position = Spec.Place(_cursor, size, GetViewportRect().Size);
        _panel.Size = size;
    }

    public override void _UnhandledKeyInput(InputEvent ev)
    {
        if (ev is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        _close();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>The Classic menu panel: opaque black (no text from the window below shows through), the gold line, 4 px padding.</summary>
    public static StyleBoxFlat PanelBox()
    {
        var box = ClassicDarkFrame.InputBox();
        box.BgColor = Colors.Black;
        box.SetContentMarginAll(Spec.Padding);
        return box;
    }

    /// <summary>A menu row: Arial Bold 12, light ink, the selected-row hover with heading ink, muted when disabled.</summary>
    public static void StyleRow(Button button)
    {
        ClassicSocialStyle.Font(button, true, Spec.FontSize);
        var normal = new StyleBoxEmpty { ContentMarginLeft = Spec.Inset, ContentMarginRight = Spec.Inset };
        var hover = new StyleBoxFlat { BgColor = ClassicDarkFrame.SelectedRow, ContentMarginLeft = Spec.Inset, ContentMarginRight = Spec.Inset };
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("disabled", normal);
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        foreach (var state in new[] { "hover", "pressed", "hover_pressed" }) button.AddThemeStyleboxOverride(state, hover);
        button.AddThemeColorOverride("font_color", ClassicDesign.Text);
        foreach (var state in new[] { "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            button.AddThemeColorOverride(state, ClassicDarkFrame.Heading);
        button.AddThemeColorOverride("font_disabled_color", ClassicDesign.Muted);
    }

    private static HSeparator Rule(string name, float height)
    {
        var rule = new HSeparator { Name = name, MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, height) };
        rule.AddThemeConstantOverride("separation", (int)height);
        rule.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = ClassicSocialStyle.Rule, Thickness = 1 });
        return rule;
    }

    private static float TextWidth(string text, int size) => Plugin.Kit.Bold.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;

    // ---- Opening ---------------------------------------------------------------------------------

    private static WeakReference<CanvasLayer>? _open;

    /// <summary>The layer of the menu that is open now, if any.</summary>
    public static CanvasLayer? OpenLayer => _open != null && _open.TryGetTarget(out var layer) && GodotObject.IsInstanceValid(layer) && !layer.IsQueuedForDeletion() ? layer : null;

    /// <summary>Opens a menu on its own canvas layer under <paramref name="host"/>; an open menu closes first.</summary>
    public static ClassicContextMenu Open(Node host, string prefix, string? title, Color? titleInk, Vector2 cursor, IReadOnlyList<ClassicMenuRow> rows)
    {
        Close();
        var layer = new CanvasLayer { Name = LayerName, Layer = Spec.Layer };
        void CloseThis() { if (GodotObject.IsInstanceValid(layer) && !layer.IsQueuedForDeletion()) layer.QueueFree(); }
        // A row closes the menu before it runs its action, as the client's popup hides on selection.
        var wrapped = rows.Select(r => r.Run is { } run ? r with { Run = () => { CloseThis(); run(); } } : r).ToList();
        var menu = new ClassicContextMenu(prefix, title, titleInk, cursor, wrapped, CloseThis);
        layer.AddChild(menu);
        host.AddChild(layer);
        _open = new WeakReference<CanvasLayer>(layer);
        return menu;
    }

    /// <summary>Closes the open menu, if any.</summary>
    public static void Close()
    {
        if (OpenLayer is { } layer) layer.QueueFree();
        _open = null;
    }

    /// <summary>
    /// Shows a client popup menu as the Classic context menu: when the client opens it, the Classic menu opens at the
    /// cursor with the client's rows (captions, disabled rows, separators) and the client's popup is hidden. A row
    /// emits the client's own id_pressed and index_pressed, so the client's handler runs unchanged.
    /// <paramref name="title"/> supplies the title row; <paramref name="skip"/> leaves out client rows by index.
    /// Safe to call repeatedly.
    /// </summary>
    public static void Mirror(PopupMenu? native, string prefix, Func<(string Text, Color? Ink)?>? title = null, Func<PopupMenu, int, bool>? skip = null)
    {
        if (native == null || !GodotObject.IsInstanceValid(native) || native.HasMeta(MirroredMeta)) return;
        native.SetMeta(MirroredMeta, true);
        native.AboutToPopup += () =>
        {
            if (!Plugin.Active || !GodotObject.IsInstanceValid(native) || native.GetParent() is not { } host || !host.IsInsideTree()) return;
            var rows = new List<ClassicMenuRow>();
            for (int i = 0; i < native.ItemCount; i++)
            {
                if (skip?.Invoke(native, i) == true) continue;
                if (native.IsItemSeparator(i)) { if (rows.Count > 0) rows.Add(ClassicMenuRow.Separator("separator_" + i)); continue; }
                int index = i; long id = native.GetItemId(i);
                rows.Add(new ClassicMenuRow("item_" + id, native.GetItemText(i), () =>
                {
                    if (!GodotObject.IsInstanceValid(native)) return;
                    native.EmitSignal(PopupMenu.SignalName.IdPressed, id);
                    native.EmitSignal(PopupMenu.SignalName.IndexPressed, index);
                }, !native.IsItemDisabled(i)));
            }
            while (rows.Count > 0 && rows[^1].IsSeparator) rows.RemoveAt(rows.Count - 1);
            (string Text, Color? Ink)? heading = title?.Invoke();
            if (heading == null && native.HasMeta(TitleMeta)) heading = (native.GetMeta(TitleMeta).AsString(), null);
            Open(host, prefix, heading?.Text, heading?.Ink, ClassicPointer.At(host), rows);
            // The client shows its popup right after this signal; hide it before the frame is drawn.
            Callable.From(() => { if (GodotObject.IsInstanceValid(native)) native.Hide(); }).CallDeferred();
        };
    }
}

/// <summary>
/// The cursor at the last mouse event of the game viewport; context menus open there. In the game this is where the
/// viewport's mouse query puts it too; a scripted check that pushes mouse events gets the same placement.
/// </summary>
public partial class ClassicPointer : Node
{
    private static Vector2? _last;

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventMouse mouse) _last = mouse.Position;
    }

    public static Vector2 At(Node host) => _last ?? host.GetViewport().GetMousePosition();

    /// <summary>Adds the tracker under <paramref name="host"/> once.</summary>
    public static void Track(Node host)
    {
        if (host.GetNodeOrNull("classic_pointer") == null) host.AddChild(new ClassicPointer { Name = "classic_pointer", ProcessMode = ProcessModeEnum.Always });
    }
}
