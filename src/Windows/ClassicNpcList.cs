using Godot;
using LibreKO.Domain;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The minimap's NPC finder (3277df5 <c>MiniMap.NpcList</c>) in the Classic look: a small "N" button on the Classic
/// button art in the map's lower left corner (where the client keeps its own) and the list of the twelve NPC kinds
/// laid over the map (the Classic minimap sits under the status bar with the hotbar beside it, so the list stays
/// inside the map instead of opening beside it): the quest tracker's plain frame over a translucent ground, "NPC
/// List" in the frame title gold, two columns of Classic rows. Kinds the zone lacks are muted; the picked kind is the
/// selected row and its spots are the gold diamonds on the map. Picking a kind closes the list so the diamonds show;
/// picking it again clears it (the client's rule). The client keeps the spot data, the selection and the blips
/// (<see cref="IGameMap.NpcKind"/>, <see cref="IGameMap.NpcKindPresent"/>, <see cref="IGameMap.PickNpcKind"/>).
/// </summary>
public partial class ClassicNpcList : Control
{
    public const string ButtonName = "btn_npc_list", PanelName = "npc_list", RowPrefix = "npc_kind_";
    public const string Title = "NPC List", Glyph = "N", ButtonTip = "NPC List";
    public const int Columns = 2, PadX = 7, PadY = 7, TitleHeight = 16, TitleGap = 3, MaxRowHeight = 20, RowInset = 2, RowFont = 11, TitleFont = 12;
    public static readonly Vector2 ButtonSize = new(18, 18);
    /// <summary>Insets inside the map area: the list from its edges, the button from the lower left corner.</summary>
    public const int ListInset = 2, ButtonInset = 8, ButtonGap = 4;

    private readonly PluginGame _game;
    private readonly Button[] _rows = new Button[NpcFinder.KindCount];
    private readonly GridContainer _grid;
    private readonly Label _title;
    private readonly ClassicFrame _frame;

    /// <summary>The "N" toggle, placed on the map by <see cref="Place"/>.</summary>
    public Button Button { get; }

    public ClassicNpcList() { _game = null!; Button = null!; _grid = null!; _title = null!; _frame = null!; }

    public ClassicNpcList(PluginGame game)
    {
        _game = game;
        Name = PanelName;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _frame = new ClassicFrame { Name = "classic_frame", BackgroundColor = Colors.Black, BackgroundAlpha = .9f };
        AddChild(_frame);
        _title = new Label
        {
            Name = "npc_list_title", Text = Title, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        ClassicEventPanel.Bold(_title, ClassicDesign.FrameTitle, TitleFont);
        _title.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        AddChild(_title);
        _grid = new GridContainer { Name = "npc_list_rows", Columns = Columns, MouseFilter = MouseFilterEnum.Ignore };
        _grid.AddThemeConstantOverride("h_separation", 0);
        _grid.AddThemeConstantOverride("v_separation", 0);
        AddChild(_grid);
        // Kinds run down the first column, then the second (the client's order, top to bottom).
        int perColumn = (NpcFinder.KindCount + Columns - 1) / Columns;
        for (int i = 0; i < NpcFinder.KindCount; i++)
        {
            int kind = i + 1;
            var row = new Button
            {
                Name = RowPrefix + kind, Text = NpcFinder.KindCaptions[i], TooltipText = NpcFinder.KindCaptions[i], ToggleMode = true,
                FocusMode = FocusModeEnum.None, Alignment = HorizontalAlignment.Left, ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseDefaultCursorShape = CursorShape.PointingHand,
            };
            StyleRow(row);
            row.Pressed += () => Pick(kind);
            _rows[i] = row;
        }
        for (int r = 0; r < perColumn; r++)
            for (int c = 0; c < Columns; c++)
                if (c * perColumn + r < _rows.Length) _grid.AddChild(_rows[c * perColumn + r]);
        Button = new Button { Name = ButtonName, TooltipText = ButtonTip, FocusMode = FocusModeEnum.None, Size = ButtonSize, CustomMinimumSize = ButtonSize, MouseDefaultCursorShape = CursorShape.PointingHand };
        StyleToggle(Button);
        Button.Pressed += () => { Visible = !Visible; Refresh(); };
        Refresh();
    }

    /// <summary>Lays the list over the map rectangle (in the parent's coordinates) and puts the button in its lower left corner.</summary>
    public void Place(Rect2 map)
    {
        Button.Position = new Vector2(map.Position.X + ButtonInset, map.End.Y - ButtonInset - ButtonSize.Y);
        var size = new Vector2(map.Size.X - ListInset * 2, map.Size.Y - ListInset - ButtonInset - ButtonSize.Y - ButtonGap);
        Position = map.Position + new Vector2(ListInset, ListInset);
        Size = CustomMinimumSize = size;
        _frame.Size = size;
        _title.Position = new Vector2(PadX, PadY);
        _title.Size = new Vector2(size.X - PadX * 2, TitleHeight);
        int perColumn = (NpcFinder.KindCount + Columns - 1) / Columns;
        float top = PadY + TitleHeight + TitleGap;
        int rowHeight = Math.Min(MaxRowHeight, (int)((size.Y - top - PadY) / perColumn));
        float columnWidth = Mathf.Floor((size.X - PadX * 2) / Columns);
        foreach (var row in _rows) row.CustomMinimumSize = new Vector2(columnWidth, rowHeight);
        _grid.Position = new Vector2(PadX, top);
        _grid.Size = new Vector2(columnWidth * Columns, rowHeight * perColumn);
    }

    /// <summary>The row of a kind (1..12).</summary>
    public Button Row(int kind) => _rows[kind - 1];

    private void Pick(int kind)
    {
        _game.Map.PickNpcKind(kind);
        Visible = false;
        Refresh();
    }

    /// <summary>Mutes the kinds the zone lacks and selects the picked kind, from the client's finder state.</summary>
    public void Refresh()
    {
        if (_game == null) return;
        var map = _game.Map;
        for (int i = 0; i < _rows.Length; i++)
        {
            int kind = i + 1;
            _rows[i].Disabled = !_game.Available || !map.NpcKindPresent(kind);
            _rows[i].SetPressedNoSignal(map.NpcKind == kind);
        }
        Button.SetPressedNoSignal(false);
        Button.TooltipText = map.NpcKind > 0 && map.NpcKind <= NpcFinder.KindCount ? $"{ButtonTip}: {NpcFinder.KindCaptions[map.NpcKind - 1]}" : ButtonTip;
    }

    /// <summary>A Classic list row: plain, lit while hovered, the selection colour while picked, muted ink when absent.</summary>
    private static void StyleRow(Button row)
    {
        StyleBox Box(Color? fill)
        {
            StyleBox box = fill is { } colour ? new StyleBoxFlat { BgColor = colour } : new StyleBoxEmpty();
            box.ContentMarginLeft = RowInset; box.ContentMarginRight = RowInset; box.ContentMarginTop = 0; box.ContentMarginBottom = 0;
            return box;
        }
        row.AddThemeStyleboxOverride("normal", Box(null));
        row.AddThemeStyleboxOverride("disabled", Box(null));
        row.AddThemeStyleboxOverride("hover", Box(new Color(ClassicDarkFrame.SelectedRow, .6f)));
        row.AddThemeStyleboxOverride("pressed", Box(ClassicDesign.Selection));
        row.AddThemeStyleboxOverride("hover_pressed", Box(ClassicDesign.Selection));
        row.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        row.AddThemeFontOverride("font", Plugin.Kit.Regular);
        row.AddThemeFontSizeOverride("font_size", RowFont);
        row.AddThemeConstantOverride("outline_size", 0);
        row.AddThemeColorOverride("font_shadow_color", Colors.Black);
        row.AddThemeConstantOverride("shadow_offset_x", 1);
        row.AddThemeConstantOverride("shadow_offset_y", 1);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_focus_color" }) row.AddThemeColorOverride(state, ClassicDesign.Text);
        foreach (var state in new[] { "font_pressed_color", "font_hover_pressed_color" }) row.AddThemeColorOverride(state, ClassicDarkFrame.Value);
        row.AddThemeColorOverride("font_disabled_color", new Color(ClassicDesign.Muted, .6f));
    }

    /// <summary>The "N" button on the Classic button art in its 18 px cell; the glyph is a centred caption in the button ink.</summary>
    private static void StyleToggle(Button button)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
        {
            var box = ClassicDesign.ButtonBox(state);
            box.SetContentMarginAll(0);
            button.AddThemeStyleboxOverride(state, box);
        }
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        var glyph = new Label
        {
            Name = "glyph", Text = Glyph, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore, Size = ButtonSize,
        };
        // Dark ink on the light Karus plate, the button gold on the dark El Morad plate.
        ClassicEventPanel.Bold(glyph, ClassicDesign.Karus ? Colors.Black : ClassicDesign.Heading, 11);
        if (ClassicDesign.Karus) glyph.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        glyph.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddChild(glyph);
        glyph.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }
}
