using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;
using System.Text.RegularExpressions;
namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The quest NPC dialogue on the MYKO reference client's own windows, when the player has installed them as local
/// assets (<see cref="MykoQuestLayout"/>): the talk menu (<c>re_quest_npc_menu</c>: title, scrolling talk text, one
/// stone row per client choice, the window cut to the choice count) and the quest viewer (<c>re_quest_viewer</c>:
/// quest title, scrolling note, two-cell objective and reward rows, pager, two action buttons). It stands in for
/// <see cref="ClassicNpcDialogue"/> and binds the same native controls: every row, cell and button presses or
/// forwards to the client's own control, so menus, accept, reward choice, confirm, abandon and close stay native.
/// </summary>
public partial class MykoQuestDialogue : Control
{
    internal static readonly Regex Number = new(@"^\d+\.\s*"), StateTag = new(@"\[(In progress|Ready|Completed|Available)\]\s*", RegexOptions.IgnoreCase);
    private readonly HudWindow _window;
    private readonly ClassicNpcDialogue _classic;
    private readonly ClassicNpcOpeningChoice _opening;
    private readonly Label _title;
    private readonly ScrollContainer _textScroll = new() { Name = "myko_text_scroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever, ClipContents = true };
    private readonly RichTextLabel _text = new() { Name = "myko_text", BbcodeEnabled = true, FitContent = true, ScrollActive = false,
        AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Pass, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Control _body = new() { Name = "myko_body", MouseFilter = MouseFilterEnum.Ignore };
    private readonly List<(LayoutNode Image, Rect2 Rect)> _pieces = new();
    private readonly List<Button> _topics = new(), _actions = new();
    private readonly List<List<Block>> _pages = new();
    private MykoScrollBar? _rail;
    private MykoArtButton? _close;
    private MykoQuestLayout? _layout;
    private object? _signature;
    private Vector2 _viewport;
    private int _page, _settling, _perPage;
    private bool _active, _offer, _centre = true;
    private (string Text, Color Ink)? _status;
    private Rect2 _textRect;
    /// <summary>Where the original's centred "Collect" caption starts inside its box.</summary>
    public const int CaptionIndent = 11;

    public MykoQuestLayout? Layout => _active ? _layout : null;
    public bool Offer => _offer;
    public int Page => _page;
    public int PageCount => _offer ? Math.Max(1, _pages.Count) : _perPage <= 0 ? 1 : Math.Max(1, (_topics.Count + _perPage - 1) / _perPage);
    public Label TitleLabel => _title;
    public RichTextLabel Text => _text;
    public ScrollContainer TextScroll => _textScroll;
    public MykoScrollBar? Rail => _rail;
    public MykoArtButton? CloseButton => _close;
    public Label? Status { get; private set; }
    public MykoArtButton? Previous { get; private set; }
    public MykoArtButton? Next { get; private set; }
    public Label? PageLabel { get; private set; }
    public List<MykoQuestOption> Options { get; } = new();
    public List<MykoQuestCell> Cells { get; } = new();
    public List<Label> Captions { get; } = new();
    public List<MykoQuestAction> Actions { get; } = new();
    public IReadOnlyList<(LayoutNode Image, Rect2 Rect)> Pieces => _pieces;

    private enum BlockKind { Caption, Cells, Text }
    private sealed record Block(BlockKind Kind, int Height, string Heading, string Text, (Control Row, bool Hunt, bool Choice)[] Cells, bool First);

    public MykoQuestDialogue(HudWindow window, ClassicNpcDialogue classic, ClassicNpcOpeningChoice opening)
    {
        Name = "myko_quest_dialogue"; _window = window; _classic = classic; _opening = opening; Visible = false;
        MouseFilter = MouseFilterEnum.Stop; TextureFilter = TextureFilterEnum.Nearest;
        _title = new Label { Name = "myko_title", MouseFilter = MouseFilterEnum.Ignore, ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _title.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        _textScroll.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _text.AddThemeStyleboxOverride("normal", new StyleBoxEmpty()); _text.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        _text.AddThemeConstantOverride("line_separation", 0); _text.AddThemeConstantOverride("outline_size", 0);
        _textScroll.AddChild(_text);
        AddChild(_body); AddChild(_textScroll); AddChild(_title);
        window.VisibilityChanged += () => { if (window.Visible) { _signature = null; _centre = true; } };
    }

    public override void _Process(double delta)
    {
        var layout = MykoQuestLayout.Current;
        bool want = layout != null && !_opening.Active;
        if (want != _active) SetActive(want);
        if (!_active || !_window.IsVisibleInTree()) return;
        if (layout != _layout) { _layout = layout; BuildChrome(); _signature = null; }
        var npcTitle = _classic.NativeTitle.Text;
        object signature = (npcTitle, _classic.NativeSpeech.Text, _classic.NativeQuest.Visible, FirstId(_classic.QuestSource),
            FirstId(_classic.MenuSource), _classic.MenuSource.GetChildCount());
        var viewport = GetViewportRect().Size;
        if (!signature.Equals(_signature) || viewport != _viewport)
        {
            if (!signature.Equals(_signature)) { _page = 0; _centre = true; }
            _signature = signature; _viewport = viewport;
            Rebuild();
            _settling = 2; Modulate = Colors.Transparent;
        }
        // The text box keeps the previous window's minimum width until its child reports the new one.
        if (_textScroll.GetRect() != _textRect) ClassicNpcLayout.Place(_textScroll, _textRect);
        if (_settling > 0 && --_settling == 0) { Modulate = Colors.White; _centre = false; }
        if (_centre) _window.Position = ((viewport - _window.Size) / 2).Floor();
    }

    private static ulong FirstId(Node parent) => parent.GetChildCount() == 0 ? 0 : parent.GetChild(0).GetInstanceId();

    /// <summary>Takes over from (or hands back to) the bundled dialogue; the client's title bar moves along as the drag handle.</summary>
    private void SetActive(bool active)
    {
        _active = active; Visible = active; _signature = null;
        var header = _classic.Header;
        if (active)
        {
            _classic.Visible = false;
            header.Reparent(this); header.Modulate = Colors.Transparent; MoveChild(header, _body.GetIndex() + 1);
        }
        else
        {
            header.Reparent(_classic); header.Modulate = Colors.White; ClassicNpcLayout.Place(header, ClassicNpcLayout.TitleText);
            if (!_opening.Active) _classic.Visible = true;
            CustomMinimumSize = Vector2.Zero; _window.ResetSize();
        }
    }

    private void BuildChrome()
    {
        _rail?.QueueFree(); _close?.QueueFree(); _rail = null; _close = null;
        if (_layout == null) return;
        _close = new MykoArtButton(_layout.MenuClose) { Name = "myko_close", TooltipText = "Close" };
        _close.Pressed += () => { if (IsInstanceValid(_classic.NativeClose)) _classic.NativeClose!.EmitSignal(BaseButton.SignalName.Pressed); };
        AddChild(_close);
    }

    /// <summary>The scroll bar of the menu's talk box or the viewer's note box (their arrows sit at different offsets).</summary>
    private void UseRail(LayoutNode node)
    {
        if (_rail?.Source != node)
        {
            if (_rail != null) { RemoveChild(_rail); _rail.QueueFree(); }
            _rail = new MykoScrollBar(_textScroll, node) { Name = "myko_text_rail" };
            AddChild(_rail);
        }
        Place(_rail, node);
    }

    /// <summary>The client's (invisible) title bar over the title band keeps the window draggable.</summary>
    private void Handle(Rect2 title)
    {
        var header = _classic.Header;
        var size = new Vector2(Math.Min(title.Size.X, ClassicNpcLayout.TitleText.Size.X), Math.Min(title.Size.Y, ClassicNpcLayout.TitleText.Size.Y));
        ClassicNpcLayout.Place(header, new Rect2((title.GetCenter() - size / 2).Floor(), size));
    }

    private static void Clear(Control owner)
    {
        foreach (var child in owner.GetChildren()) { owner.RemoveChild(child); child.QueueFree(); }
    }

    private void Rebuild()
    {
        if (_layout == null) return;
        _topics.Clear(); _actions.Clear();
        foreach (var source in _classic.MenuSource.GetChildren().OfType<Control>())
        {
            if (source is Button button) _topics.Add(button);
            else if (source is HBoxContainer actions) _actions.AddRange(actions.GetChildren().OfType<Button>());
        }
        _offer = _classic.NativeQuest.Visible;
        if (_offer) BuildViewer(_layout); else BuildMenu(_layout);
    }

    // ---- Talk menu (re_quest_npc_menu) ------------------------------------------------------------------------

    private void BuildMenu(MykoQuestLayout layout)
    {
        var title = layout.MenuTitle; var talk = layout.MenuTalk;
        MykoQuestLayout.Ink(_title, title); _title.Text = _classic.NativeTitle.Text; _title.TooltipText = _title.Text;
        ClassicNpcLayout.Place(_title, new Rect2(title.X + 36, title.Y, title.W - 72, title.H)); Handle(_title.GetRect());
        UseRail(layout.MenuScroll); ShowText(talk, _classic.NativeSpeech.Text);
        Place(_close!, layout.MenuClose);
        int room = (int)_viewport.Y - 24 - layout.FirstRow - layout.MenuBottom.H;
        int fit = Math.Clamp(room / layout.RowPitch, 1, layout.MaxRows);
        bool pager = _topics.Count > fit;
        if (pager) fit = Math.Clamp((room - layout.PagerStrip.H) / layout.RowPitch, 1, layout.MaxRows);
        _perPage = fit; _page = Math.Clamp(_page, 0, PageCount - 1);
        ArrangeMenu(layout, pager);
    }

    private void ArrangeMenu(MykoQuestLayout layout, bool pager)
    {
        Clear(_body); Options.Clear(); Cells.Clear(); Captions.Clear(); Actions.Clear(); _pieces.Clear();
        Status = null; Previous = Next = null; PageLabel = null;
        int rows = pager ? _perPage : _topics.Count;
        foreach (var image in layout.MenuHead) _pieces.Add((image, new Rect2(image.X, image.Y, image.W, image.H)));
        int first = _page * _perPage;
        for (int row = 0; row < rows; row++)
        {
            _pieces.Add((layout.RowStrip, layout.RowPart(layout.RowStrip, row)));
            int index = first + row;
            if (index >= _topics.Count) continue;
            _pieces.Add((layout.RowPlate, layout.RowPart(layout.RowPlate, row)));
            var option = new MykoQuestOption(_topics[index], layout) { Name = "myko_option_" + index };
            _body.AddChild(option); ClassicNpcLayout.Place(option, layout.RowPart(layout.RowButton, row)); Options.Add(option);
        }
        int y = layout.FirstRow + rows * layout.RowPitch;
        if (pager)
        {
            _pieces.Add((layout.RowStrip, new Rect2(layout.RowStrip.X, y, layout.RowStrip.W, layout.PagerStrip.H)));
            Pager(layout, y, (layout.MenuWidth - layout.ViewerWidth) / 2, () => ArrangeMenu(layout, true));
            y += layout.PagerStrip.H;
        }
        _pieces.Add((layout.MenuBottom, new Rect2(layout.MenuBottom.X, y, layout.MenuBottom.W, layout.MenuBottom.H)));
        Fit(new Vector2(layout.MenuWidth, layout.MenuHeight(rows, pager)));
    }

    // ---- Quest viewer (re_quest_viewer) -----------------------------------------------------------------------

    private void BuildViewer(MykoQuestLayout layout)
    {
        var source = _classic.QuestSource;
        string title = source.GetChildren().OfType<HBoxContainer>().SelectMany(h => h.GetChildren().OfType<Label>()).FirstOrDefault()?.Text ?? _classic.NativeTitle.Text;
        MykoQuestLayout.Ink(_title, layout.ViewerTitle); _title.Text = title; _title.TooltipText = title;
        var t = layout.ViewerTitle; ClassicNpcLayout.Place(_title, new Rect2(t.X + 30, t.Y, t.W - 60, t.H)); Handle(_title.GetRect());
        var paragraphs = source.GetChildren().OfType<RichTextLabel>().Select(p => p.Text.Trim()).Where(p => p.Length > 0).ToArray();
        UseRail(layout.ViewerScroll); ShowText(layout.ViewerNote, string.Join("\n\n", paragraphs));
        Place(_close!, layout.ViewerClose);
        var status = source.GetChildren().OfType<Label>().FirstOrDefault(l => l.HasMeta("quest_status"));
        // Sections: the client's heading label, then its panel of rows.
        var sections = new List<(string Heading, List<object> Units)>();
        string heading = "";
        foreach (var child in source.GetChildren().OfType<Control>())
        {
            if (child is Label label) { if (!label.HasMeta("quest_status")) heading = label.Text; continue; }
            if (child is not PanelContainer panel || panel.GetChildCount() == 0) continue;
            var units = new List<object>(); var pending = new List<(Control, bool, bool)>();
            void Flush() { for (int i = 0; i < pending.Count; i += 2) units.Add(pending.Skip(i).Take(2).ToArray()); pending.Clear(); }
            bool hunt = heading == "Hunt";
            foreach (var row in panel.GetChild<Control>(0).GetChildren().OfType<Control>())
            {
                if (row is HBoxContainer || row is VBoxContainer && row.GetChildren().OfType<HBoxContainer>().Any()) pending.Add((row, hunt, row.HasMeta("quest_reward_selected")));
                else if (row is Label hint) { Flush(); units.Add(hint.Text); }
                else if (row is RichTextLabel text) { Flush(); units.Add(text.GetParsedText()); }
            }
            Flush();
            sections.Add((heading == "Choose one" ? "Choose one reward" : heading, units));
        }
        int room = Math.Min(layout.MaxContent, (int)_viewport.Y - 24 - layout.ContentTop - layout.ViewerFooter);
        room = Math.Max(room, layout.CaptionHeight(true) + layout.CellRowHeight);
        Paginate(layout, sections, room);
        _page = Math.Clamp(_page, 0, PageCount - 1);
        _status = status == null ? null : (status.Text, ClassicQuestStatus.Ink((LibreKO.Network.QuestViewState)status.GetMeta("quest_status").AsInt32()));
        ArrangeViewer(layout);
    }

    private int TextHeight(LayoutNode ink, string text, float width)
    {
        var font = ink.Bold ? Plugin.Kit.Bold : Plugin.Kit.Regular; int size = UiKit.FontSize(ink);
        var measured = font.GetMultilineStringSize(text, HorizontalAlignment.Left, width, size);
        return (int)Mathf.Ceil(Math.Max(measured.Y, font.GetHeight(size))) + 6;
    }

    private void Paginate(MykoQuestLayout layout, List<(string Heading, List<object> Units)> sections, int room)
    {
        _pages.Clear();
        var page = new List<Block>(); int used = 0;
        float textWidth = layout.ViewerWidth - 2 * layout.FirstCaption.Strings.First().X;
        foreach (var (heading, units) in sections)
        {
            bool needCaption = true;
            void Caption() { int h = layout.CaptionHeight(page.Count == 0); page.Add(new Block(BlockKind.Caption, h, heading, "", Array.Empty<(Control, bool, bool)>(), page.Count == 0)); used += h; needCaption = false; }
            if (units.Count == 0) { if (used + layout.CaptionHeight(page.Count == 0) > room && page.Count > 0) { _pages.Add(page); page = new(); used = 0; } Caption(); continue; }
            foreach (var unit in units)
            {
                var block = unit is string text
                    ? new Block(BlockKind.Text, Math.Min(TextHeight(layout.CellFirst, text, textWidth), room - layout.CaptionHeight(true)), heading, text, Array.Empty<(Control, bool, bool)>(), false)
                    : new Block(BlockKind.Cells, layout.CellRowHeight, heading, "", ((Control, bool, bool)[])unit, false);
                int caption = needCaption ? layout.CaptionHeight(page.Count == 0) : 0;
                if (page.Count > 0 && used + caption + block.Height > room)
                {
                    _pages.Add(page); page = new(); used = 0; needCaption = true;
                }
                if (needCaption) Caption();
                page.Add(block); used += block.Height;
            }
        }
        if (page.Count > 0 || _pages.Count == 0) _pages.Add(page);
    }

    private void ArrangeViewer(MykoQuestLayout layout)
    {
        Clear(_body); Options.Clear(); Cells.Clear(); Captions.Clear(); Actions.Clear(); _pieces.Clear();
        Status = null; Previous = Next = null; PageLabel = null;
        foreach (var image in layout.ViewerHead) _pieces.Add((image, new Rect2(image.X, image.Y, image.W, image.H)));
        int content = _pages.Max(p => p.Sum(b => b.Height));
        var captionArt = layout.Caption.Images.First(); var firstArt = layout.FirstCaption.Images.First(); var rowArt = layout.CellRow.Images.First();
        int y = layout.ContentTop; bool statusPlaced = false;
        foreach (var block in _pages[_page])
        {
            switch (block.Kind)
            {
                case BlockKind.Caption:
                {
                    var group = block.First ? layout.FirstCaption : layout.Caption;
                    var art = block.First ? firstArt : captionArt;
                    _pieces.Add((art, new Rect2(art.X, y, art.W, block.Height)));
                    var ink = group.Strings.First();
                    var caption = MykoQuestLayout.Text(ink, block.Heading); caption.Name = "myko_caption";
                    // The original centres a short caption in a 75 px box; the client's headings are longer, so every
                    // caption starts where the original's "Collect" does and they stay aligned with each other.
                    float width = caption.GetThemeFont("font").GetStringSize(block.Heading, HorizontalAlignment.Left, -1, caption.GetThemeFontSize("font_size")).X + 2;
                    caption.HorizontalAlignment = HorizontalAlignment.Left;
                    _body.AddChild(caption); ClassicNpcLayout.Place(caption, new Rect2(ink.X + CaptionIndent, y + ink.Y - group.Y, width, ink.H)); Captions.Add(caption);
                    if (_status is { } state && !statusPlaced)
                    {
                        statusPlaced = true; float x = ink.X + CaptionIndent + width + 8;
                        Status = MykoQuestLayout.Text(ink, state.Text, state.Ink); Status.Name = "myko_quest_status"; Status.HorizontalAlignment = HorizontalAlignment.Right;
                        _body.AddChild(Status); ClassicNpcLayout.Place(Status, new Rect2(x, y + ink.Y - group.Y, layout.ViewerWidth - ink.X - x, ink.H));
                    }
                    break;
                }
                case BlockKind.Cells:
                    _pieces.Add((rowArt, new Rect2(rowArt.X, y, rowArt.W, block.Height)));
                    for (int i = 0; i < block.Cells.Length; i++)
                    {
                        var (row, hunt, choice) = block.Cells[i];
                        var anchor = i == 0 ? layout.LeftCell : layout.RightCell;
                        var cell = new MykoQuestCell(row, hunt, choice, layout) { Name = hunt ? "myko_hunt_cell" : "myko_item_cell" };
                        _body.AddChild(cell); ClassicNpcLayout.Place(cell, new Rect2(anchor.X, y + anchor.Y - layout.CellRow.Y, anchor.W, anchor.H)); Cells.Add(cell);
                    }
                    break;
                case BlockKind.Text:
                {
                    _pieces.Add((rowArt, new Rect2(rowArt.X, y, rowArt.W, block.Height)));
                    var ink = layout.CellFirst; var x = layout.FirstCaption.Strings.First().X;
                    var text = MykoQuestLayout.Text(ink, block.Text); text.Name = "myko_section_text";
                    text.AutowrapMode = TextServer.AutowrapMode.WordSmart; text.VerticalAlignment = VerticalAlignment.Top; text.ClipText = true;
                    _body.AddChild(text); ClassicNpcLayout.Place(text, new Rect2(x, y + 3, layout.ViewerWidth - 2 * x, block.Height - 6));
                    break;
                }
            }
            y += block.Height;
        }
        int end = layout.ContentTop + content;
        if (y < end) _pieces.Add((rowArt, new Rect2(rowArt.X, y, rowArt.W, end - y)));
        // Footer: pager strip, bottom art, action frames and the client's actions.
        _pieces.Add((layout.PagerStrip, new Rect2(layout.PagerStrip.X, end, layout.PagerStrip.W, layout.PagerStrip.H)));
        var bottom = layout.InFooter(layout.ViewerBottom); _pieces.Add((layout.ViewerBottom, new Rect2(bottom.Position + new Vector2(0, end), bottom.Size)));
        Pager(layout, end, 0, () => ArrangeViewer(layout));
        var slots = Slots(layout, _actions.Count);
        for (int i = 0; i < _actions.Count; i++)
        {
            var (frame, button) = slots[i];
            _pieces.Add((layout.ActionFrames[0], new Rect2(frame.Position + new Vector2(0, end), frame.Size)));
            // Every action is the same stone button with the neutral caption ink (MykoQuestLayout.ActionInk).
            var action = new MykoQuestAction(_actions[i], layout, layout.LeftAction) { Name = "myko_action_" + i };
            _body.AddChild(action); ClassicNpcLayout.Place(action, new Rect2(button.Position + new Vector2(0, end), button.Size)); Actions.Add(action);
        }
        Fit(new Vector2(layout.ViewerWidth, layout.ViewerHeight(content)));
    }

    /// <summary>Frame and button rectangles (relative to the pager strip) for <paramref name="count"/> actions: the original two slots, one centred slot, or evenly spaced slots.</summary>
    private static (Rect2 Frame, Rect2 Button)[] Slots(MykoQuestLayout layout, int count)
    {
        var frame = layout.InFooter(layout.ActionFrames[0]); var button = layout.InFooter(layout.LeftAction);
        var inset = button.Position - frame.Position;
        if (count == 2) return new[] { (frame, button), (layout.InFooter(layout.ActionFrames[^1]), layout.InFooter(layout.RightAction)) };
        var result = new (Rect2, Rect2)[count];
        float pitch = layout.ViewerWidth / (float)Math.Max(1, count);
        for (int i = 0; i < count; i++)
        {
            float width = Math.Min(frame.Size.X, pitch - 8), scale = width / frame.Size.X;
            var f = new Rect2(Mathf.Floor(pitch * i + (pitch - width) / 2), frame.Position.Y, width, frame.Size.Y);
            result[i] = (f, new Rect2(f.Position.X + Mathf.Round(inset.X * scale), button.Position.Y, Mathf.Round(button.Size.X * scale), button.Size.Y));
        }
        return result;
    }

    // ---- Shared parts -----------------------------------------------------------------------------------------

    private void ShowText(LayoutNode ink, string text)
    {
        foreach (string face in new[] { "normal", "bold", "italics", "bold_italics", "mono" })
            _text.AddThemeFontOverride(face + "_font", face.StartsWith("bold") || ink.Bold ? Plugin.Kit.Bold : Plugin.Kit.Regular);
        foreach (string face in new[] { "normal", "bold", "italics", "bold_italics", "mono" }) _text.AddThemeFontSizeOverride(face + "_font_size", UiKit.FontSize(ink));
        _text.AddThemeColorOverride("default_color", new Color(ink.Color, 1));
        _text.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .8f));
        _text.AddThemeConstantOverride("shadow_offset_x", 1); _text.AddThemeConstantOverride("shadow_offset_y", 1);
        _text.CustomMinimumSize = new Vector2(ink.W, 0); _text.Size = new Vector2(ink.W, 0);
        // Whole lines only: the box shows as many full lines as fit the original rectangle and scrolls by lines.
        float line = (ink.Bold ? Plugin.Kit.Bold : Plugin.Kit.Regular).GetHeight(UiKit.FontSize(ink));
        _textRect = new Rect2(ink.X, ink.Y, ink.W, Math.Max(line, Mathf.Floor(ink.H / line) * line)); ClassicNpcLayout.Place(_textScroll, _textRect);
        _text.Text = text; _textScroll.ScrollVertical = 0;
        if (_rail != null) _rail.Step = line;
    }

    private void Pager(MykoQuestLayout layout, int y, int x, Action rearrange)
    {
        MykoArtButton Arrow(LayoutNode node, bool next)
        {
            var arrow = new MykoArtButton(node) { Name = next ? "myko_next_page" : "myko_previous_page", TooltipText = next ? "Next page" : "Previous page",
                Disabled = next ? _page >= PageCount - 1 : _page == 0, DisabledTint = new Color(.75f, .75f, .75f) };
            arrow.Pressed += () => { _page = Math.Clamp(_page + (next ? 1 : -1), 0, PageCount - 1); rearrange(); };
            _body.AddChild(arrow); ClassicNpcLayout.Place(arrow, new Rect2(layout.InFooter(node).Position + new Vector2(x, y), layout.InFooter(node).Size));
            return arrow;
        }
        Previous = Arrow(layout.Prev, false); Next = Arrow(layout.Next, true);
        PageLabel = MykoQuestLayout.Text(layout.PageText, $"{_page + 1}/{PageCount}"); PageLabel.Name = "myko_page"; PageLabel.ClipText = false;
        _body.AddChild(PageLabel); ClassicNpcLayout.Place(PageLabel, new Rect2(layout.InFooter(layout.PageText).Position + new Vector2(x, y), layout.InFooter(layout.PageText).Size));
    }

    private static void Place(Control control, LayoutNode node) => ClassicNpcLayout.Place(control, new Rect2(node.X, node.Y, node.W, node.H));

    private void Fit(Vector2 size)
    {
        _body.Size = size;
        if (size != Size || CustomMinimumSize != size) { CustomMinimumSize = size; Size = size; _window.ResetSize(); }
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var (image, rect) in _pieces) MykoQuestLayout.Draw(this, image, rect);
    }
}

/// <summary>An original button drawn from its layout state images (0 normal, 1 pressed, 2 hover, 3 disabled).</summary>
public partial class MykoArtButton : Button
{
    public LayoutNode Art { get; }
    /// <summary>How the disabled art is darkened (the original's disabled images repeat the normal ones).</summary>
    public Color DisabledTint { get; set; } = new(.55f, .55f, .55f);
    private DrawMode _drawn = (DrawMode)(-1);

    public MykoArtButton(LayoutNode art)
    {
        Art = art; FocusMode = FocusModeEnum.None; TextureFilter = TextureFilterEnum.Nearest; ClipText = true;
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        // Option and action rows mirror their client buttons in _Process; all of it only matters while shown.
        ShownOnly.Gate(this, () => _Process(0));
    }

    public int ArtState => GetDrawMode() switch
    {
        DrawMode.Disabled => 3,
        DrawMode.Pressed or DrawMode.HoverPressed => 1,
        DrawMode.Hover => 2,
        _ => 0,
    };

    public override void _Process(double delta)
    {
        if (GetDrawMode() != _drawn) QueueRedraw();
    }

    public override void _Draw()
    {
        _drawn = GetDrawMode();
        var image = MykoQuestLayout.State(Art, ArtState);
        if (image == null) return;
        var tint = Disabled ? DisabledTint : Colors.White;
        MykoQuestLayout.Draw(this, image, new Rect2(Vector2.Zero, Size), tint);
    }
}

/// <summary>One client choice as an original stone option row: caption centred, the client's quest state at the right.</summary>
public partial class MykoQuestOption : MykoArtButton
{
    public Button Source { get; }
    public Label Caption { get; }
    public Label State { get; }
    private readonly Color _ink;
    private string? _text;
    private bool? _disabled;

    public MykoQuestOption(Button source, MykoQuestLayout layout) : base(layout.RowButton)
    {
        Source = source; _ink = new Color(layout.RowCaption.Color, 1);
        Caption = MykoQuestLayout.Text(layout.RowCaption); Caption.Name = "myko_option_caption"; AddChild(Caption);
        State = MykoQuestLayout.Text(layout.RowCaption); State.Name = "myko_option_state"; State.HorizontalAlignment = HorizontalAlignment.Right; State.ClipText = false; AddChild(State);
        Pressed += () => { if (IsInstanceValid(Source) && !Source.Disabled) Source.EmitSignal(BaseButton.SignalName.Pressed); };
        Resized += () => _text = null;
        Mirror();
    }

    public override void _Process(double delta) { Mirror(); base._Process(delta); }

    private void Mirror()
    {
        if (!IsInstanceValid(Source)) { Disabled = true; return; }
        if (Source.Text == _text && Source.Disabled == _disabled) return;
        _text = Source.Text; _disabled = Source.Disabled; Disabled = Source.Disabled;
        // The client numbers its talk choices ("1.   …"); the original rows show the caption alone.
        string? state = ClassicNpcAction.StateOf(Source);
        string caption = MykoQuestDialogue.StateTag.Replace(MykoQuestDialogue.Number.Replace(Source.Text, ""), "").Trim();
        if (state is not null && state.Equals("Available", StringComparison.OrdinalIgnoreCase)) state = null;
        State.Visible = state != null; State.Text = state ?? "";
        float inset = 8;
        if (state != null)
        {
            var quest = state.ToLowerInvariant() switch { "in progress" => LibreKO.Network.QuestViewState.InProgress, "ready" => LibreKO.Network.QuestViewState.Claimable,
                _ => LibreKO.Network.QuestViewState.Completed };
            State.AddThemeColorOverride("font_color", Disabled ? ClassicNpcOpeningLayout.Disabled : ClassicQuestStatus.Ink(quest));
            float width = State.GetThemeFont("font").GetStringSize(state, HorizontalAlignment.Left, -1, State.GetThemeFontSize("font_size")).X;
            ClassicNpcLayout.Place(State, new Rect2(Size.X - width - 8, 0, width, Size.Y));
            inset = width + 16;
        }
        Caption.Text = caption;
        Caption.AddThemeColorOverride("font_color", Disabled ? ClassicNpcOpeningLayout.Disabled : _ink);
        ClassicNpcLayout.Place(Caption, new Rect2(inset, 0, Math.Max(1, Size.X - 2 * inset), Size.Y));
        var font = Caption.GetThemeFont("font"); int size = Caption.GetThemeFontSize("font_size");
        // The client repeats each caption as its tooltip; a row only needs it when the caption is cut.
        TooltipText = Size.X <= 1 || font.GetStringSize(caption, HorizontalAlignment.Left, -1, size).X <= Caption.Size.X ? "" : caption;
    }
}

/// <summary>One of the client's quest actions (Accept, Confirm, Abandon, Reject, Close) as an original stone button.</summary>
public partial class MykoQuestAction : MykoArtButton
{
    public Button Source { get; }
    public Label Caption { get; }
    private readonly Color _ink;
    private string? _text, _tooltip;
    private bool? _disabled;

    public MykoQuestAction(Button source, MykoQuestLayout layout, LayoutNode style) : base(style)
    {
        Source = source;
        var ink = style.Strings.First(); _ink = layout.ActionInk;
        Caption = MykoQuestLayout.Text(ink, "", _ink); Caption.Name = "myko_action_caption"; Caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(Caption);
        Pressed += () => { if (IsInstanceValid(Source) && !Source.Disabled) Source.EmitSignal(BaseButton.SignalName.Pressed); };
        Mirror();
    }

    public override void _Process(double delta) { Mirror(); base._Process(delta); }

    private void Mirror()
    {
        if (!IsInstanceValid(Source)) { Disabled = true; return; }
        if (Source.Text == _text && Source.Disabled == _disabled && Source.TooltipText == _tooltip) return;
        _text = Source.Text; _disabled = Source.Disabled; _tooltip = Source.TooltipText;
        Disabled = Source.Disabled; Caption.Text = Source.Text; TooltipText = Source.TooltipText;
        Caption.AddThemeColorOverride("font_color", Disabled ? ClassicNpcOpeningLayout.Disabled : _ink);
    }
}

/// <summary>A quest objective or reward as an original viewer cell bound to the client's row (tooltip, target and reward-choice input).</summary>
public partial class MykoQuestCell : Control
{
    public Control Source { get; }
    public bool Choice { get; }
    public Label First { get; }
    public Label Second { get; }
    public TextureRect Icon { get; }
    private readonly MykoQuestLayout _layout;
    private readonly Label? _sourceName, _sourceValue;
    private readonly Color _secondInk;
    private string _lastName = "", _lastValue = "";
    private bool _selected, _done;

    public bool Selected => _selected;

    public MykoQuestCell(Control source, bool hunt, bool choice, MykoQuestLayout layout)
    {
        Source = source; Choice = choice; _layout = layout; MouseFilter = MouseFilterEnum.Stop; TextureFilter = TextureFilterEnum.Nearest;
        var values = source as HBoxContainer ?? source.GetChildren().OfType<HBoxContainer>().FirstOrDefault() ?? source;
        _sourceName = values.GetChildren().OfType<Label>().FirstOrDefault(); _sourceValue = values.GetChildren().OfType<Label>().LastOrDefault();
        var icon = layout.InCell(layout.CellIcon);
        Icon = new TextureRect { Name = "myko_cell_icon", Texture = hunt ? Plugin.Kit.Texture("npc_monster_hunt_32.png") : KnightOnlineUiClassic.Native.NativeQuestLog.RewardIcon(source),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(Icon); ClassicNpcLayout.Place(Icon, icon.Grow(-2));
        First = MykoQuestLayout.Text(layout.CellFirst); First.Name = "myko_cell_first"; AddChild(First); ClassicNpcLayout.Place(First, layout.InCell(layout.CellFirst));
        _secondInk = new Color(layout.CellSecond.Color, 1);
        Second = MykoQuestLayout.Text(layout.CellSecond); Second.Name = "myko_cell_second"; AddChild(Second); ClassicNpcLayout.Place(Second, layout.InCell(layout.CellSecond));
        GuiInput += input => { if (IsInstanceValid(Source)) Source.EmitSignal(Control.SignalName.GuiInput, input); };
        MouseEntered += () => { if (IsInstanceValid(Source)) Source.EmitSignal(Control.SignalName.MouseEntered); };
        MouseExited += () => { if (IsInstanceValid(Source)) Source.EmitSignal(Control.SignalName.MouseExited); };
        Mirror();
        ShownOnly.Gate(this, Mirror);
    }

    public override void _Process(double delta) => Mirror();

    private void Mirror()
    {
        if (!IsInstanceValid(Source)) return;
        string name = _sourceName?.Text ?? "", value = (_sourceValue?.Text ?? "").TrimEnd('○', '●', ' ');
        bool selected = Choice && Source.HasMeta("quest_reward_selected") && Source.GetMeta("quest_reward_selected").AsBool();
        var ink = _sourceValue?.GetThemeColor("font_color") ?? Colors.White;
        bool done = ink.G > ink.R * 1.1f && ink.G > ink.B * 1.1f;
        if (name == _lastName && value == _lastValue && selected == _selected && done == _done) return;
        _lastName = name; _lastValue = value; _done = done;
        var upgrade = Regex.Match(name, @"^(.*?)\s*\(\+(\d+)\)$");
        First.Text = upgrade.Success ? upgrade.Groups[1].Value.Trim() : name;
        // A name one pixel size too long for the cell steps down once before it is cut (the tooltip has it whole).
        int size = UiKit.FontSize(_layout.CellFirst);
        if (First.GetThemeFont("font").GetStringSize(First.Text, HorizontalAlignment.Left, -1, size).X > First.Size.X) size--;
        First.AddThemeFontSizeOverride("font_size", size);
        Second.Text = upgrade.Success ? $"+{upgrade.Groups[2].Value} · {value}" : value;
        Second.AddThemeColorOverride("font_color", done ? ClassicQuestStatus.Ink(LibreKO.Network.QuestViewState.Claimable) : _secondInk);
        TooltipText = name + (Source.TooltipText.Length > 0 ? "\n" + Source.TooltipText : "");
        if (selected != _selected) { _selected = selected; QueueRedraw(); }
    }

    public override void _Draw()
    {
        var cell = MykoQuestLayout.State(_layout.LeftCell, _selected ? 1 : 0);
        if (cell != null) MykoQuestLayout.Draw(this, cell, new Rect2(Vector2.Zero, Size));
        // The original's selected art differs from the normal one by a shade; the chosen reward also gets the Classic gold frame.
        if (_selected) DrawRect(new Rect2(Vector2.Zero, Size).Grow(-1.5f), new Color("e4c174"), false, 1);
    }
}

/// <summary>The original quest scroll bar (arrows, track, thumb) bound to a ScrollContainer's range.</summary>
public partial class MykoScrollBar : Control
{
    private readonly ScrollContainer _scroll;
    private readonly LayoutNode _trackArt, _thumbArt;
    private Rect2 _track;
    private bool _dragging;
    private float _grab;
    private double _drawnValue = -1, _drawnMax = -1;
    public LayoutNode Source { get; }
    public MykoArtButton Up { get; }
    public MykoArtButton Down { get; }
    public float Step { get; set; } = 16;
    public VScrollBar Bar => _scroll.GetVScrollBar();
    private double Span => Math.Max(0, Bar.MaxValue - Bar.Page - Bar.MinValue);
    private float ThumbHeight => _thumbArt.H;
    public float ThumbTop => _track.Position.Y + (Span <= 0 ? 0 : (float)((Bar.Value - Bar.MinValue) / Span) * Math.Max(0, _track.Size.Y - ThumbHeight));

    public MykoScrollBar(ScrollContainer scroll, LayoutNode node)
    {
        _scroll = scroll; Source = node; MouseFilter = MouseFilterEnum.Stop; TextureFilter = TextureFilterEnum.Nearest;
        var arrows = node.Children.Where(n => n.IsButton).OrderBy(n => n.Y).ToArray();
        var track = node.Children.First(n => n.Type == "trackbar");
        _trackArt = track.Images.First(n => n.Tag == 0); _thumbArt = track.Images.First(n => n.Tag == 1);
        Up = new MykoArtButton(arrows[0]) { Name = "myko_scroll_up" }; Down = new MykoArtButton(arrows[^1]) { Name = "myko_scroll_down" };
        Up.Pressed += () => Bar.Value -= Step; Down.Pressed += () => Bar.Value += Step;
        AddChild(Up); AddChild(Down);
        ClassicNpcLayout.Place(Up, new Rect2(arrows[0].X - node.X, arrows[0].Y - node.Y, arrows[0].W, arrows[0].H));
        ClassicNpcLayout.Place(Down, new Rect2(arrows[^1].X - node.X, arrows[^1].Y - node.Y, arrows[^1].W, arrows[^1].H));
        _track = new Rect2(track.X - node.X, track.Y - node.Y, track.W, track.H);
        _scroll.GuiInput += input =>
        {
            if (input is not InputEventMouseButton { Pressed: true } wheel || wheel.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown)) return;
            Bar.Value += (wheel.ButtonIndex == MouseButton.WheelUp ? -3 : 3) * Step; _scroll.AcceptEvent();
        };
    }

    public override void _Ready() => ShownOnly.Gate(this, () => _Process(0));

    public override void _Process(double delta)
    {
        if (Bar.Value != _drawnValue || Bar.MaxValue != _drawnMax) QueueRedraw();
    }

    public override void _Draw()
    {
        _drawnValue = Bar.Value; _drawnMax = Bar.MaxValue;
        MykoQuestLayout.Draw(this, _trackArt, _track);
        MykoQuestLayout.Draw(this, _thumbArt, new Rect2(_track.Position.X, Mathf.Round(ThumbTop), _track.Size.X, ThumbHeight));
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { Pressed: true } press)
        {
            if (press.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown) { Bar.Value += (press.ButtonIndex == MouseButton.WheelUp ? -3 : 3) * Step; AcceptEvent(); return; }
            if (press.ButtonIndex != MouseButton.Left || !_track.HasPoint(press.Position)) return;
            if (press.Position.Y >= ThumbTop && press.Position.Y < ThumbTop + ThumbHeight) { _dragging = true; _grab = press.Position.Y - ThumbTop; }
            else Bar.Value += press.Position.Y < ThumbTop ? -Bar.Page : Bar.Page;
            AcceptEvent();
        }
        else if (input is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left }) _dragging = false;
        else if (input is InputEventMouseMotion motion && _dragging && Span > 0)
        {
            float travel = Math.Max(1, _track.Size.Y - ThumbHeight);
            double value = Bar.MinValue + Mathf.Clamp((motion.Position.Y - _grab - _track.Position.Y) / travel, 0, 1) * Span;
            Bar.Value = Step > 0 && value < Bar.MaxValue - Bar.Page ? Math.Round(value / Step) * Step : value; AcceptEvent();
        }
    }
}
