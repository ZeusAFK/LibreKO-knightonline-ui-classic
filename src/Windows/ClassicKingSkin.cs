using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Native;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// King election and nation treasury windows on their original King UIF artwork. The native controls are
/// reparented into the original composition and keep their callbacks; the skin only adds selection for the
/// candidate list, the Organization column and character counts.
/// </summary>
public static class ClassicKingSkin
{
    public static readonly string[] WindowIds = NativeKing.WindowIds;
    public static void Extend(Control body) => body.Ready += () => Callable.From(() => Apply(body)).CallDeferred();
    public static ClassicKingPanel? Apply(Control body)
    {
        Node? root = body;
        while (root != null && root is not HudWindow) root = root.GetParent();
        if (root is not HudWindow window || window.HasMeta("classic_king")) return null;
        if (window.GetMeta("classic_king_controls", 0).AsInt32() != 1)
        {
            ClassicSkin.Apply(window);
            return null;
        }
        window.SetMeta("classic_king", true);
        var panel = new ClassicKingPanel(window, body); window.AddChild(panel); window.ResetSize(); return panel;
    }
}

public partial class ClassicKingPanel : Control
{
    private static readonly Color Cyan = new("80ffff"), Yellow = new("ffff00"), Parchment = new("e2d9c9");
    private static readonly Color Heading = new("d2d2d2"), Plain = new("c0c0c0"), Navy = new("000040");
    private static readonly Color Refused = new("ff6a5a"), Muted = new("8a8a8a"), Emboss = new(.88f, .88f, .88f, .55f);

    public HudWindow Window { get; }
    public int SelectedCandidate { get; private set; } = -1;
    private static readonly StringName PendingMeta = "king_pending", CandidateClanMeta = "king_candidate_clan", ExpectedRectMeta = "king_expected_rect";
    private static readonly string[] TaxOptions = { "nation_tax_treasury", "nation_tax_rate", "nation_tax_fund", "nation_tax_item", "nation_tax_intro" };

    private readonly LayoutNode _art;
    private readonly ControlIndex _native;
    private readonly Button _close;
    private readonly List<Button> _pendingButtons = new(), _artButtons = new();
    private readonly Button[] _choices = new Button[ClassicKingLayout.VoteRows];
    private readonly Label[] _clans = new Label[ClassicKingLayout.VoteRows];
    private Button? _escape, _vote;
    private Label? _count;
    private TextEdit? _counted;
    private int _limit;

    public ClassicKingPanel(HudWindow window, Control body)
    {
        Window = window; Name = "classic_king"; Size = CustomMinimumSize = ClassicKingLayout.Size(window.Id);
        TextureFilter = TextureFilterEnum.Nearest;
        _art = Plugin.Kit.Layout(ClassicKingLayout.Art(window.Id));
        _native = new ControlIndex(CharacterDetailsSkin.Tree(body).OfType<Control>().ToArray());
        _close = NativeKing.Close(window)!;
        var bar = window.Header!.GetChildren().OfType<HBoxContainer>().First();
        var title = bar.GetChildren().OfType<Label>().LastOrDefault();
        foreach (var child in window.GetChildren().OfType<Control>()) child.Visible = false;
        window.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _close.Reparent(this); _close.Visible = false;
        switch (window.Id)
        {
            case "kingelection": Election(bar); break;
            case "kingnominate": Nominate(bar); break;
            case "kingplan": Board(bar, title, "king_plan_text", "king_plan_accept", "king_plan_cancel", "OK", "Cancel"); break;
            case "nationintro": Board(bar, title, "nation_intro_text", "nation_intro_revision", "nation_intro_accept", "Revision", "OK"); break;
            case "kingvote": Vote(bar, title); break;
            case "kingballot": Ballot(bar); break;
            case "nationtax": Treasury(bar); break;
        }
        VisibilityChanged += () => { if (!IsVisibleInTree()) SelectedCandidate = -1; };
        ShownOnly.Gate(this, Sync);
    }

    private T Find<T>(string name) where T : Control => _native.Single<T>(name);

    private void Place(Control control, Rect2 rect)
    {
        bool visible = control.Visible;
        ClassicVendorSkin.Move(control, this, rect);
        control.Visible = visible;
        control.SetMeta(ExpectedRectMeta, rect);
    }

    private static void Ink(Control control, int size, bool bold, Color color)
    {
        control.AddThemeFontOverride("font", bold ? Plugin.Kit.Bold : Plugin.Kit.Regular);
        control.AddThemeFontSizeOverride("font_size", size);
        control.AddThemeConstantOverride("outline_size", 0);
        control.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        control.AddThemeColorOverride("font_color", color);
    }

    private void Text(Label label, Rect2 rect, int size, bool bold, Color color, HorizontalAlignment align = HorizontalAlignment.Left, bool wrap = false)
    {
        Place(label, rect); Ink(label, size, bold, color);
        label.HorizontalAlignment = align;
        label.VerticalAlignment = wrap ? VerticalAlignment.Top : VerticalAlignment.Center;
        label.AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
        label.TextOverrunBehavior = wrap ? TextServer.OverrunBehavior.NoTrimming : TextServer.OverrunBehavior.TrimEllipsis;
        label.ClipText = true; label.CustomMinimumSize = Vector2.Zero;
    }

    private Label NewLabel(string name, string text, Rect2 rect, int size, bool bold, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Name = name, Text = text, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(label); Text(label, rect, size, bold, color, align); return label;
    }

    /// <summary>A push button on original King artwork: dark embossed caption on the stone face, white on the lit blue face.</summary>
    private void ArtButton(Button button, LayoutNode source, Rect2 rect, string? text, int size = 13)
    {
        Place(button, rect);
        ClassicChatControls.SkinButton(button, source);
        button.Icon = null; button.Flat = false; button.FocusMode = FocusModeEnum.None; button.Shortcut = null;
        button.ClipText = true; button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        button.Alignment = HorizontalAlignment.Center;
        if (text != null) button.Text = text;
        button.AddThemeFontOverride("font", Plugin.Kit.Bold); button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeConstantOverride("outline_size", 0);
        button.AddThemeColorOverride("font_shadow_color", Emboss);
        button.AddThemeConstantOverride("shadow_offset_x", 1); button.AddThemeConstantOverride("shadow_offset_y", 1);
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
            if (button.HasThemeStyleboxOverride(state)) button.GetThemeStylebox(state).SetContentMarginAll(0);
        foreach (string state in new[] { "font_color", "font_focus_color" }) button.AddThemeColorOverride(state, Colors.Black);
        foreach (string state in new[] { "font_hover_color", "font_pressed_color", "font_hover_pressed_color" }) button.AddThemeColorOverride(state, Colors.White);
        button.AddThemeColorOverride("font_disabled_color", new Color("4a4a4a"));
        if (!_artButtons.Contains(button)) _artButtons.Add(button);
    }

    /// <summary>The original draws the stone caption over a light emboss and the lit caption over a black one.</summary>
    private void UpdateEmboss()
    {
        foreach (var button in _artButtons)
        {
            if (!GodotObject.IsInstanceValid(button)) continue;
            var mode = button.GetDrawMode();
            var shadow = !button.Disabled && mode is BaseButton.DrawMode.Hover or BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed ? Colors.Black : Emboss;
            if (button.GetThemeColor(ThemeNames.FontShadowColor) != shadow) button.AddThemeColorOverride(ThemeNames.FontShadowColor, shadow);
        }
    }

    private LayoutNode Node(string id) => _art.Find(id)!;

    private void CloseButton(Rect2 rect)
    {
        ArtButton(_close, Plugin.Kit.Layout(ClassicKingLayout.CloseArt).Find("btn_close")!, rect, "");
        _close.TooltipText = "Close"; _close.Visible = true; _escape = _close;
    }

    private void Drag(Control bar, Rect2 rect) => ClassicPartySkin.Drag(this, bar, rect);

    private static void Editor(Control edit)
    {
        Ink(edit, 13, false, new Color("e6e6e6"));
        var box = new StyleBoxEmpty(); box.SetContentMarginAll(4);
        foreach (string state in new[] { "normal", "focus", "read_only" }) edit.AddThemeStyleboxOverride(state, box);
        edit.AddThemeColorOverride("caret_color", Yellow);
        edit.AddThemeColorOverride("selection_color", new Color(.25f, .38f, .62f, .8f));
        if (edit is TextEdit text)
        {
            var bar = text.GetVScrollBar(); bar.CustomMinimumSize = new Vector2(10, 0);
            foreach (var state in new[] { "grabber", "grabber_highlight", "grabber_pressed" })
                bar.AddThemeStyleboxOverride(state, ClassicDesign.ButtonBox(state == "grabber_highlight" ? "hover" : state == "grabber_pressed" ? "pressed" : "normal"));
            bar.AddThemeStyleboxOverride("scroll", new StyleBoxFlat { BgColor = new Color("161616") });
        }
    }

    /// <summary>Escape inside a text field runs the window's cancel, as the header X would.</summary>
    private void FieldEscape(Control input) => input.GuiInput += ev =>
    {
        if (ev is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } || _escape == null) return;
        _escape.EmitSignal(BaseButton.SignalName.Pressed); input.AcceptEvent();
    };

    private void Election(Control bar)
    {
        Drag(bar, ClassicKingLayout.ElectionDrag);
        Text(Find<Label>("king_speech_upper"), ClassicKingLayout.ElectionSpeechFull, 13, false, Cyan, wrap: true);
        Text(Find<Label>("king_speech_lower"), ClassicKingLayout.ElectionSpeechLower, 13, true, Yellow, wrap: true);
        var senators = Find<ScrollContainer>("king_senators"); Place(senators, ClassicKingLayout.ElectionSenators);
        var names = Find<Label>("king_senators_text"); Ink(names, 13, true, Yellow);
        names.CustomMinimumSize = new Vector2(1, 0); names.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        StyleScroll(senators);
        for (int i = 0; i < NativeKing.ElectionRows; i++)
        {
            var option = Find<Button>("king_option_" + i);
            Place(option, ClassicKingLayout.ElectionOption(i));
            ClassicChatControls.SkinButton(option, Node(i == 0 ? "btn00" : "btn0" + i));
            option.Icon = null; option.FocusMode = FocusModeEnum.None; option.Alignment = HorizontalAlignment.Center;
            option.ClipText = true; option.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            Ink(option, 12, true, Navy);
            foreach (string state in new[] { "font_hover_color", "font_pressed_color", "font_hover_pressed_color" }) option.AddThemeColorOverride(state, Colors.White);
            option.AddThemeColorOverride("font_focus_color", Navy);
            option.AddThemeColorOverride("font_disabled_color", new Color("4a4a58"));
            _pendingButtons.Add(option);
        }
        CloseButton(ClassicKingLayout.ElectionClose);
    }

    private static void StyleScroll(ScrollContainer scroll)
    {
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        var bar = scroll.GetVScrollBar(); bar.CustomMinimumSize = new Vector2(10, 0);
        foreach (var state in new[] { "grabber", "grabber_highlight", "grabber_pressed" })
            bar.AddThemeStyleboxOverride(state, ClassicDesign.ButtonBox(state == "grabber_highlight" ? "hover" : state == "grabber_pressed" ? "pressed" : "normal"));
        bar.AddThemeStyleboxOverride("scroll", new StyleBoxFlat { BgColor = new Color("161616") });
    }

    private void Nominate(Control bar)
    {
        Drag(bar, ClassicKingLayout.NominateDrag);
        Text(Find<Label>("king_nominate_text"), ClassicKingLayout.NominateText, 13, false, Cyan, wrap: true);
        Text(Find<Label>("king_nominate_note"), ClassicKingLayout.NominateNote, 13, false, Yellow, wrap: true);
        var caption = Find<Label>("king_nominate_caption");
        Text(caption, ClassicKingLayout.NominateIdCaption, 15, true, Yellow, HorizontalAlignment.Center);
        var id = Find<LineEdit>("king_nominee"); Place(id, ClassicKingLayout.NominateId); Editor(id);
        id.AddThemeFontOverride("font", Plugin.Kit.Bold); id.AddThemeFontSizeOverride("font_size", 15); id.AddThemeColorOverride("font_color", Cyan);
        foreach (string state in new[] { "normal", "focus", "read_only" }) { id.GetThemeStylebox(state).SetContentMargin(Side.Top, 0); id.GetThemeStylebox(state).SetContentMargin(Side.Bottom, 0); }
        FieldEscape(id);
        ArtButton(Find<Button>("king_nominate_accept"), Node("btn_ok"), ClassicKingLayout.NominateAccept, "OK");
        _escape = Find<Button>("king_nominate_cancel");
        ArtButton(_escape, Node("btn_cancel"), ClassicKingLayout.NominateCancel, "Cancel");
    }

    private void Board(Control bar, Label? title, string text, string left, string right, string leftText, string rightText)
    {
        Drag(bar, ClassicKingLayout.BoardDrag);
        if (title != null) { Text(title, ClassicKingLayout.BoardTitle, 13, true, Heading); title.Visible = true; }
        var edit = Find<TextEdit>(text); Place(edit, ClassicKingLayout.BoardText); Editor(edit); FieldEscape(edit);
        _counted = edit;
        if (Window.Id == "kingplan")
        {
            _count = Find<Label>("king_plan_count"); _limit = LibreKO.Domain.KingElection.PlanLimit;
            Text(_count, ClassicKingLayout.BoardCount, 12, true, Plain, HorizontalAlignment.Right);
        }
        else
        {
            _limit = LibreKO.Domain.NationTreasury.IntroLimit;
            _count = NewLabel("nation_intro_count", "", ClassicKingLayout.BoardCount, 12, true, Plain, HorizontalAlignment.Right);
        }
        var accept = Find<Button>(left);
        ArtButton(accept, Node("btn_ok"), ClassicKingLayout.BoardLeft, leftText);
        ArtButton(Find<Button>(right), Node("btn_close"), ClassicKingLayout.BoardRight, rightText);
        if (Window.Id == "kingplan") { _pendingButtons.Add(accept); _escape = Find<Button>(right); }
        else CloseButton(ClassicKingLayout.BoardClose);
    }

    private void Vote(Control bar, Label? title)
    {
        Drag(bar, ClassicKingLayout.VoteDrag);
        if (title != null) { Text(title, ClassicKingLayout.VoteTitle, 13, true, Heading, HorizontalAlignment.Center); title.Visible = true; }
        NewLabel("king_vote_heading_name", "Nominee List", ClassicKingLayout.VoteNameHeading, 13, true, Heading, HorizontalAlignment.Center);
        NewLabel("king_vote_heading_clan", "Organization", ClassicKingLayout.VoteClanHeading, 13, true, Heading, HorizontalAlignment.Center);
        var small = Plugin.Kit.Layout(ClassicKingLayout.SmallButtonArt).Find("btn_write")!;
        for (int i = 0; i < ClassicKingLayout.VoteRows; i++)
        {
            int row = i;
            var choice = new Button { Name = "king_vote_select_" + i, ToggleMode = true, FocusMode = FocusModeEnum.None, TooltipText = "Select" };
            AddChild(choice); Place(choice, ClassicKingLayout.VoteSelect(i));
            ClassicChatControls.SkinButton(choice, Node("btn0" + i));
            // The selected candidate keeps the original lit check.
            choice.AddThemeStyleboxOverride("hover_pressed", choice.GetThemeStylebox("pressed"));
            choice.Pressed += () => SelectedCandidate = choice.ButtonPressed ? row : -1;
            _choices[i] = choice;
            Text(Find<Label>("king_vote_number_" + i), ClassicKingLayout.VoteNumber(i), 13, true, Yellow, HorizontalAlignment.Right);
            Text(Find<Label>("king_vote_name_" + i), ClassicKingLayout.VoteName(i), 13, true, Yellow);
            _clans[i] = NewLabel("king_vote_clan_" + i, "", ClassicKingLayout.VoteClan(i), 13, true, Yellow);
            var pledge = Find<Button>("king_vote_pledge_" + i);
            ArtButton(pledge, small, ClassicKingLayout.VotePledge(i), "Pledge", 10);
            pledge.TooltipText = "Read this candidate's pledge";
            _pendingButtons.Add(pledge);
        }
        _vote = new Button { Name = "king_vote_accept" }; AddChild(_vote);
        ArtButton(_vote, Node("btn_agree"), ClassicKingLayout.VoteAccept, "Vote");
        _vote.Pressed += () =>
        {
            // The selected row's own native Vote button asks the native confirmation.
            if (SelectedCandidate >= 0) Find<Button>("king_vote_" + SelectedCandidate).EmitSignal(BaseButton.SignalName.Pressed);
        };
        ArtButton(_close, Node("btn_close"), ClassicKingLayout.VoteCancel, "Cancel");
        _close.Visible = true; _escape = _close;
    }

    private void Ballot(Control bar)
    {
        Drag(bar, ClassicKingLayout.BallotDrag);
        var text = Find<Label>("king_ballot_text");
        Text(text, ClassicKingLayout.BallotText, 12, true, Cyan, HorizontalAlignment.Center, true);
        text.VerticalAlignment = VerticalAlignment.Center;
        ArtButton(Find<Button>("king_ballot_favour"), Node("btn_ok"), ClassicKingLayout.BallotFavour, null);
        ArtButton(Find<Button>("king_ballot_against"), Node("btn_opposition"), ClassicKingLayout.BallotAgainst, null);
        _escape = Find<Button>("king_ballot_cancel");
        ArtButton(_escape, Node("btn_cancel"), ClassicKingLayout.BallotCancel, null);
    }

    private void Treasury(Control bar)
    {
        Drag(bar, ClassicKingLayout.TreasuryDrag);
        Text(Find<Label>("nation_tax_heading"), ClassicKingLayout.TreasuryHeading, 13, true, Parchment, HorizontalAlignment.Center);
        Text(Find<Label>("nation_tax_notice"), ClassicKingLayout.TreasuryNotice, 13, true, Parchment, HorizontalAlignment.Center, true);
        string[] options = TaxOptions;
        for (int i = 0; i < options.Length; i++)
        {
            var option = Find<Button>(options[i]); TextOption(option, ClassicKingLayout.TreasuryOption(i));
            _pendingButtons.Add(option);
        }
        TextOption(_close, ClassicKingLayout.TreasuryOption(options.Length)); _close.Text = "Exit"; _close.Visible = true;
        _close.TooltipText = ""; _escape = _close;
    }

    /// <summary>The Grand Chamberlain's services are captions over transparent hit areas.</summary>
    private void TextOption(Button button, Rect2 rect)
    {
        Place(button, rect);
        var empty = new StyleBoxEmpty();
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" }) button.AddThemeStyleboxOverride(state, empty);
        button.Icon = null; button.Flat = true; button.FocusMode = FocusModeEnum.None; button.Alignment = HorizontalAlignment.Center;
        button.ClipText = true; button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        Ink(button, 13, true, Cyan);
        button.AddThemeColorOverride("font_hover_color", Yellow);
        button.AddThemeColorOverride("font_pressed_color", Colors.White); button.AddThemeColorOverride("font_hover_pressed_color", Colors.White);
        button.AddThemeColorOverride("font_focus_color", Cyan); button.AddThemeColorOverride("font_disabled_color", Muted);
        button.AddThemeColorOverride("font_shadow_color", Colors.Black);
        button.AddThemeConstantOverride("shadow_offset_x", 1); button.AddThemeConstantOverride("shadow_offset_y", 1);
    }

    private static void Move(Control control, Rect2 rect)
    {
        if (control.Position != rect.Position) control.Position = rect.Position;
        if (control.Size != rect.Size) control.Size = rect.Size;
        control.SetMeta(ExpectedRectMeta, rect);
    }

    public override void _Process(double delta) => Sync();

    /// <summary>Mirrors the native window into the Classic composition; runs every frame while shown.</summary>
    private void Sync()
    {
        bool pending = Window.GetMeta(PendingMeta, false).AsBool();
        foreach (var button in _pendingButtons) Disable(button, pending);
        switch (Window.Id)
        {
            case "kingelection":
            {
                for (int i = 0; i < NativeKing.ElectionRows; i++) FitOption(Find<Button>("king_option_" + i));
                var upper = Find<Label>("king_speech_upper");
                bool senators = Find<ScrollContainer>("king_senators").Visible, lower = Find<Label>("king_speech_lower").Visible;
                Move(upper, senators ? ClassicKingLayout.ElectionSenatorsCaption : lower ? ClassicKingLayout.ElectionSpeechUpper : ClassicKingLayout.ElectionSpeechFull);
                break;
            }
            case "kingvote":
                for (int i = 0; i < _choices.Length; i++)
                {
                    bool filled = Find<Label>("king_vote_number_" + i).Text.Length > 0;
                    if (!filled && SelectedCandidate == i) SelectedCandidate = -1;
                    _choices[i].Disabled = !filled;
                    _choices[i].SelfModulate = filled ? Colors.White : new Color(.55f, .55f, .55f);
                    _choices[i].SetPressedNoSignal(SelectedCandidate == i);
                    _clans[i].Text = filled ? Find<PanelContainer>("king_vote_row_" + i).GetMeta(CandidateClanMeta, "").AsString() : "";
                    var ink = SelectedCandidate == i ? Colors.White : Yellow;
                    foreach (var label in new[] { Find<Label>("king_vote_number_" + i), Find<Label>("king_vote_name_" + i), _clans[i] })
                    {
                        label.AddThemeColorOverride(HotNames.FontColor, ink);
                        // Long names and clans are trimmed to the original columns; the full text stays on hover.
                        label.TooltipText = label.Text; label.MouseFilter = label.Text.Length > 0 ? MouseFilterEnum.Pass : MouseFilterEnum.Ignore;
                    }
                }
                Disable(_vote!, pending || SelectedCandidate < 0);
                break;
            case "nationtax":
            {
                bool king = Find<VBoxContainer>("nation_tax_options").Visible;
                foreach (var name in TaxOptions) Find<Button>(name).Visible = king;
                Move(Find<Label>("nation_tax_notice"), king ? ClassicKingLayout.TreasuryNotice : ClassicKingLayout.TreasuryCitizenNotice);
                break;
            }
        }
        UpdateEmboss();
        if (_count != null && _counted != null)
        {
            int length = _counted.Text.Length;
            if (Window.Id == "nationintro") _count.Text = $"{length} / {_limit}";
            _count.AddThemeColorOverride(HotNames.FontColor, length > _limit ? Refused : Plain);
        }
    }

    /// <summary>Display captions for native option texts longer than the original button string.</summary>
    private static readonly Dictionary<string, string> ShortCaptions = new()
    {
        ["List of users in favor of impeachment"] = "Impeachment supporters",
    };

    /// <summary>Keeps an option caption inside the original 154 px button string; the full text stays as a tooltip.</summary>
    private static void FitOption(Button option)
    {
        if (!ShortCaptions.TryGetValue(option.Text, out var caption)) return;
        if (Plugin.Kit.Bold.GetStringSize(option.Text, HorizontalAlignment.Left, -1, 12).X <= ClassicKingLayout.ElectionCaption) return;
        option.TooltipText = option.Text;
        option.Text = caption;
    }

    private static void Disable(Button button, bool disabled)
    {
        button.Disabled = disabled;
        button.SelfModulate = disabled ? new Color(.55f, .55f, .55f) : Colors.White;
    }

    public override void _UnhandledKeyInput(InputEvent ev)
    {
        if (!Window.IsVisibleInTree() || _escape == null || ev is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
        _escape.EmitSignal(BaseButton.SignalName.Pressed);
        GetViewport().SetInputAsHandled();
    }

    public override void _Draw()
    {
        // List and board windows straighten the curls of their silver frame; NPC talk windows keep their art.
        var straight = ClassicStraightArt.For(ClassicKingLayout.Art(Window.Id));
        if (straight != null)
        {
            DrawTexture(ClassicStraightArt.Compose(ClassicKingLayout.Art(Window.Id), (Vector2I)Size, ClassicKingLayout.Backing(Window.Id), Pieces(), straight), Vector2.Zero);
            return;
        }
        DrawRect(ClassicKingLayout.Backing(Window.Id), Colors.Black);
        foreach (var (texture, rect, source) in Pieces()) DrawTextureRectRegion(texture, rect, source);
    }

    private IEnumerable<(Texture2D Texture, Rect2 Rect, Rect2 Source)> Pieces()
    {
        bool karus = Plugin.Kit.Nation == 1;
        foreach (var part in Images(_art))
        {
            if (part.Id == "image_karu" && !karus || part.Id == "image_elmo" && karus) continue;
            var rect = new Rect2(part.Position, part.SizeVec);
            if (Window.Id == "nationtax")
            {
                // The middle section is made of stretched filler strips; lengthen them and lower the base.
                if (part.Y >= ClassicKingLayout.TreasurySplit) rect.Position += new Vector2(0, ClassicKingLayout.TreasuryExtra);
                else if (part.Y + part.H == ClassicKingLayout.TreasurySplit && (part.W != part.SrcW || part.H != part.SrcH)) rect.Size += new Vector2(0, ClassicKingLayout.TreasuryExtra);
            }
            yield return (Plugin.Kit.Texture(part.Texture!)!, rect, new Rect2(part.SrcX, part.SrcY, part.SrcW, part.SrcH));
        }
    }

    /// <summary>Static artwork: window images and the frames of edit fields; buttons draw themselves.</summary>
    private static IEnumerable<LayoutNode> Images(LayoutNode root)
    {
        foreach (var child in root.Children)
        {
            if (child.IsImage && child.Texture != null) yield return child;
            else if (child.Type == "edit") foreach (var image in child.Images.Where(n => n.Texture != null)) yield return image;
        }
    }
}

/// <summary>Godot names used every frame in this file, created once (a literal creates a finalizable wrapper per call).</summary>
file static class HotNames
{
    internal static readonly StringName FontColor = "font_color";
}
