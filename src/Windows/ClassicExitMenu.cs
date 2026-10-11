using Godot;
using LibreKO;
using LibreKO.Plugins;
using KnightOnlineUiClassic.Layout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The original exit menu (co_exitmenu: Select Character, Exit, Option, Cancel) for the client's exit menu
/// seam. When temporary items are about to run out, a plain-frame list of them sits above the menu, as the
/// client lists them before the player leaves. Every action runs the client's own row.
/// </summary>
public partial class ClassicExitMenu : Control
{
    public const float TimedWidth = 320, TimedRowPitch = 48, TimedCell = 40, Gap = 6;
    public const int TimedRowsShown = 3;
    private static readonly (string Button, string Row)[] Rows =
        { ("btn_chr", "character_select"), ("btn_exit", "end_game"), ("btn_option", "option"), ("btn_cancel", "cancel") };

    private readonly ExitMenuRequest _request;
    private readonly LayoutView _menu;
    private readonly Control? _timed;

    public LayoutView Menu => _menu;
    public Control? Timed => _timed;

    public ClassicExitMenu(ExitMenuRequest request)
    {
        _request = request; Name = "classic_exit_menu"; MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var source = Plugin.Kit.Layout("co_exitmenu_us");
        var layout = new LayoutNode { Type = "base", Id = "exit_menu", X = 0, Y = 0, W = source.W, H = source.H };
        foreach (var child in source.Children)
        {
            var copy = CharacterLayout.CopyImage(child, -source.X, -source.Y);
            if (copy.IsString)
            {
                copy.Font = child.Font; copy.Size = child.Size; copy.Bold = child.Bold; copy.Color = child.Color;
                copy.Text = Caption(child) ?? child.Text.Trim();
            }
            layout.Children.Add(copy);
        }
        _menu = new LayoutView(Plugin.Kit, layout) { Name = "exit_menu" };
        AddChild(_menu);
        foreach (var (button, row) in Rows)
            if (_request.Rows.FirstOrDefault(r => r.Key == row) is { Key: not null } entry)
                _menu.OnPressed(button, entry.Run);
        if (_request.TimedItems.Count > 0) _timed = TimedList();
        Resized += Arrange;
    }

    /// <summary>The client's caption for the row whose button the original string labels.</summary>
    private string? Caption(LayoutNode text)
    {
        var source = Plugin.Kit.Layout("co_exitmenu_us");
        foreach (var (button, row) in Rows)
            if (source.Find(button) is { } node && text.Y + text.H / 2 >= node.Y && text.Y + text.H / 2 <= node.Y + node.H)
                return _request.Rows.FirstOrDefault(r => r.Key == row).Caption;
        return null;
    }

    private Control TimedList()
    {
        int shown = Math.Min(_request.TimedItems.Count, TimedRowsShown);
        float listHeight = shown * TimedRowPitch - (TimedRowPitch - TimedCell) + 7;
        var notice = new Label { Text = _request.Notice, AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore, Name = "exit_timed_notice" };
        Font(notice, ClassicReportDesign.Caption);
        float inner = TimedWidth - ClassicFrame.Inset * 2;
        notice.Position = new Vector2(ClassicFrame.Inset, ClassicFrame.Inset); notice.Size = new Vector2(inner, 34);
        var panel = new Control { Name = "exit_timed", MouseFilter = MouseFilterEnum.Stop };
        panel.Size = new Vector2(TimedWidth, ClassicFrame.Inset * 2 + 34 + 8 + listHeight);
        panel.AddChild(new ClassicFrame { Size = panel.Size, BackgroundAlpha = 1, BackgroundColor = Colors.Black });
        panel.AddChild(notice);
        var scroll = new ScrollContainer { Name = "exit_timed_scroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            Position = new Vector2(ClassicFrame.Inset, ClassicFrame.Inset + 34 + 8), Size = new Vector2(inner, listHeight) };
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", (int)(TimedRowPitch - TimedCell));
        // Inventory cell borders reach past their cells; keep them inside the clipping list.
        var inset = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inset.AddThemeConstantOverride("margin_left", 4); inset.AddThemeConstantOverride("margin_top", 3);
        inset.AddThemeConstantOverride("margin_bottom", 4);
        inset.AddChild(list); scroll.AddChild(inset); panel.AddChild(scroll);
        var track = new StyleBoxFlat { BgColor = new Color("171715"), BorderColor = new Color("bfa777") };
        track.SetBorderWidthAll(1); track.SetContentMarginAll(0);
        var bar = scroll.GetVScrollBar(); bar.AddThemeStyleboxOverride("scroll", track); bar.CustomMinimumSize = new Vector2(12, 0);
        foreach (var state in new[] { "grabber", "grabber_highlight", "grabber_pressed" })
        {
            var box = ClassicDesign.ButtonBox(state == "grabber_highlight" ? "hover" : state == "grabber_pressed" ? "pressed" : "normal");
            box.SetContentMarginAll(0); bar.AddThemeStyleboxOverride(state, box);
        }
        foreach (var item in _request.TimedItems) list.AddChild(Row(item));
        AddChild(panel);
        return panel;
    }

    private static Control Row(ExitMenuItem item)
    {
        var row = new Control { CustomMinimumSize = new Vector2(0, TimedCell), MouseFilter = MouseFilterEnum.Ignore, Name = "exit_timed_row" };
        bool karus = ClassicDesign.Karus;
        var frame = new Panel { Size = new Vector2(TimedCell, TimedCell), MouseFilter = MouseFilterEnum.Ignore };
        var box = new StyleBoxTexture { Texture = Plugin.Kit.Texture(karus ? "ui_ka_inven_us.png" : "ui_el_inven_us.png"),
            RegionRect = new Rect2(karus ? 159 : 157, karus ? 326 : 333, 51, 52) };
        foreach (var side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom }) { box.SetTextureMargin(side, 4); box.SetContentMargin(side, 0); }
        box.ExpandMarginLeft = 4; box.ExpandMarginTop = 3; box.ExpandMarginRight = 2; box.ExpandMarginBottom = 4;
        frame.AddThemeStyleboxOverride("panel", box); row.AddChild(frame);
        var icon = new TextureRect { Texture = item.Icon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            Position = new Vector2(ItemCountStyle.Inset, ItemCountStyle.Inset), Size = new Vector2(TimedCell - ItemCountStyle.Inset * 2, TimedCell - ItemCountStyle.Inset * 2),
            MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(icon);
        var name = new Label { Text = item.Name, Position = new Vector2(TimedCell + 10, 1), Size = new Vector2(TimedWidth - ClassicFrame.Inset * 2 - TimedCell - 22, 19),
            ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseFilter = MouseFilterEnum.Ignore, TooltipText = item.Name };
        Font(name, ClassicReportDesign.Value); row.AddChild(name);
        var left = new Label { Text = item.Place.Length > 0 ? $"{item.Place}  ·  {item.TimeLeft}" : item.TimeLeft, Position = new Vector2(TimedCell + 10, 21),
            Size = name.Size, ClipText = true, MouseFilter = MouseFilterEnum.Ignore };
        Font(left, item.Urgent ? new Color("dc143c") : ClassicReportDesign.Caption, 11); row.AddChild(left);
        return row;
    }

    private static void Font(Label label, Color color, int size = 12)
    {
        label.AddThemeFontOverride("font", Plugin.Kit.Bold); label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color); label.AddThemeConstantOverride("outline_size", 0);
    }

    public override void _Ready()
    {
        _menu.ApplyDeclaredBounds();
        Arrange();
    }

    /// <summary>Centres the menu, with the temporary item list above it, in the viewport.</summary>
    private void Arrange()
    {
        var view = GetViewportRect().Size;
        float height = _menu.Size.Y + (_timed != null ? _timed.Size.Y + Gap : 0);
        float top = Mathf.Round((view.Y - height) / 2);
        if (_timed != null)
        {
            _timed.Position = new Vector2(Mathf.Round((view.X - _timed.Size.X) / 2), top);
            top += _timed.Size.Y + Gap;
        }
        _menu.Position = new Vector2(Mathf.Round((view.X - _menu.Size.X) / 2), top);
    }

    public override void _UnhandledKeyInput(InputEvent ev)
    {
        if (ev is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        _request.Close();
        GetViewport().SetInputAsHandled();
    }
}
