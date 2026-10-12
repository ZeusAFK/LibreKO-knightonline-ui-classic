using Godot;
using LibreKO.Domain;
using LibreKO.Plugins;
using KnightOnlineUiClassic.Layout;
using Spec = KnightOnlineUiClassic.Layout.ClassicUserInfoLayout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The Classic character card (<c>userinfo</c>), drawn from the plugin user-card API (<see cref="IGameUserInfo"/>).
/// The client's own card body stays in the window, hidden, so its reply handling, clan mark refresh and Confirm
/// button keep working; the Confirm button stays under the Classic card.
/// </summary>
public partial class ClassicUserInfoCard : Control
{
    private const string HolderName = "native_user_info";
    private const string MedalArt = "res://assets/ui/daily_rank/medal_{0}.png";
    private const int FontSize = 13, PlaceSize = 18;
    private readonly IGameUserInfo _info = null!;
    private bool _queued;

    public ClassicUserInfoCard() { }

    public ClassicUserInfoCard(IGameUserInfo info)
    {
        _info = info;
        Name = "classic_user_info"; MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.Nearest;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        // The shell's restyling and width constraints leave this card alone; it builds its lines in the tree.
        SetMeta("classic_detail_control", true);
    }

    /// <summary>
    /// Puts the Classic card in the native body: the client's card parts move into a hidden holder (the client
    /// keeps filling them), the footer with its Confirm button stays below the card.
    /// </summary>
    public static ClassicUserInfoCard Compose(Control body, IGameUserInfo info)
    {
        var holder = new Control { Name = HolderName, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        holder.SetMeta("classic_detail_control", true);
        var footer = body.GetChildren().OfType<HBoxContainer>().LastOrDefault(row => row.GetChildren().OfType<Button>().Any());
        foreach (var part in body.GetChildren().OfType<Control>().Where(c => c != footer).ToArray()) part.Reparent(holder);
        body.AddChild(holder);
        var card = new ClassicUserInfoCard(info);
        body.AddChild(card); body.MoveChild(card, 0);
        if (footer != null) footer.Name = "user_info_footer";
        return card;
    }

    public override void _Ready()
    {
        _info.Changed += QueueRefresh;
        Build();
    }
    public override void _ExitTree() => _info.Changed -= QueueRefresh;

    private void QueueRefresh()
    {
        if (_queued) return;
        _queued = true;
        Callable.From(() => { _queued = false; if (IsInstanceValid(this)) Build(); }).CallDeferred();
    }

    /// <summary>The card as the client last reported it; rebuilt whenever the client raises a change.</summary>
    public void Build()
    {
        foreach (var child in GetChildren().ToArray()) { RemoveChild(child); child.QueueFree(); }
        if (_info.Card is not { } card) { CustomMinimumSize = new Vector2(Spec.Width, 0); return; }
        var facts = new (string Key, string Value, Color? Ink)[]
        {
            ("Character name", card.Name, card.NameColour),
            ("Level", card.Level, null),
            ("Job", card.Job, null),
            ("Cont/Ladder", $"{card.Loyalty} / {card.MonthlyLoyalty}", null),
            ("Status", card.State, null),
            ("Relation", Relation(card.Relation), null),
        };
        for (int row = 0; row < facts.Length; row++)
        {
            Text("fact_key_" + row, facts[row].Key, Spec.FactKeyRect(row), ClassicReportDesign.Value);
            var value = Text("fact_value_" + row, facts[row].Value, Spec.FactValueRect(row), facts[row].Ink ?? ClassicReportDesign.Value);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            Add(new ClassicReportSurface(true) { Name = "fact_rule_" + row }, Spec.FactRule(row));
        }
        float top = facts.Length * Spec.FactRow + Spec.SectionGap;
        Add(new ClassicReportSection { Name = "clan_box" }, Spec.ClanBox(top));
        if (card.InClan)
        {
            Texture2D? medal = null;
            string place = "";
            if (card.ClanRanking is >= 1 and <= UserInfoCard.PodiumPlaces)
            {
                string path = string.Format(MedalArt, card.ClanRanking);
                medal = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
                if (medal == null) place = card.ClanRanking.ToString();
            }
            else if (card.ClanRanking > UserInfoCard.PodiumPlaces) place = card.ClanRanking.ToString();
            Add(new ClassicUserInfoCell(medal, place) { Name = "clan_podium", TooltipText = card.ClanRanking > 0 ? $"Clan ranking {card.ClanRanking}" : "" }, Spec.Podium(top));
            Add(new ClassicUserInfoCell(card.ClanMark, "") { Name = "clan_mark" }, Spec.Mark(top));
            var lines = new[] { ("Clan", card.ClanName), ("Grade", card.ClanGrade), ("Leader", card.ClanChief) };
            for (int line = 0; line < lines.Length; line++)
            {
                Text("clan_key_" + line, lines[line].Item1, Spec.ClanKey(top, line), ClassicReportDesign.Caption, bold: true);
                Text("clan_value_" + line, lines[line].Item2, Spec.ClanValue(top, line), ClassicReportDesign.Value);
            }
        }
        else Text("clan_none", "Not in a clan", Spec.ClanEmpty(top), ClassicDesign.Muted).HorizontalAlignment = HorizontalAlignment.Center;
        top += Spec.ClanHeight + Spec.SectionGap;
        for (int i = 0; i < card.Summary.Count; i++)
            Add(new ClassicRankRibbon(card.Summary[i]) { Name = "summary_" + i }, Spec.SummaryCell(top, i));
        if (card.Summary.Count > 0) top += Spec.SummaryHeight(card.Summary.Count) + Spec.SectionGap;
        if (card.Rows.Count > 0)
        {
            Text("rank_heading", "Ranking", Spec.RankTitle(top), ClassicReportDesign.Caption, bold: true);
            Text("rank_place_heading", "Place", Spec.RankPlace(top), ClassicReportDesign.Caption, bold: true).HorizontalAlignment = HorizontalAlignment.Center;
            var marks = Text("rank_marks_heading", "Marks", new Rect2(Spec.TiersLeft, top, 3 * Spec.Ribbon, Spec.RankRow - 1), ClassicReportDesign.Caption, bold: true);
            marks.HorizontalAlignment = HorizontalAlignment.Center;
            Add(new ColorRect { Name = "rank_heading_rule", Color = ClassicDarkFrame.Line, MouseFilter = MouseFilterEnum.Ignore }, Spec.RankRule(top));
            top += Spec.RankRow;
            for (int r = 0; r < card.Rows.Count; r++)
            {
                var row = card.Rows[r];
                Text("rank_title_" + r, row.Title, Spec.RankTitle(top), ClassicReportDesign.Value);
                Text("rank_place_" + r, row.Place, Spec.RankPlace(top), row.PlaceColour, bold: true).HorizontalAlignment = HorizontalAlignment.Center;
                for (int t = 0; t < row.Tiers.Count && t < 3; t++)
                    Add(new ClassicRankRibbon(row.Tiers[t]) { Name = $"rank_{r}_tier_{t}" }, Spec.RankTier(top, t));
                Add(new ColorRect { Name = "rank_rule_" + r, Color = ClassicSocialStyle.RowRule, MouseFilter = MouseFilterEnum.Ignore }, Spec.RankRule(top));
                top += Spec.RankRow;
            }
        }
        else top -= Spec.SectionGap;
        CustomMinimumSize = new Vector2(Spec.Width, Mathf.Ceil(top));
    }

    private static string Relation(UserRelation relation) => relation switch
    {
        UserRelation.Friend => "Friend",
        UserRelation.Clan => "Clan",
        UserRelation.Both => "Friend / Clan",
        _ => "-",
    };

    private T Add<T>(T control, Rect2 rect) where T : Control
    {
        AddChild(control);
        control.Position = rect.Position; control.Size = rect.Size;
        control.SetMeta("user_info_rect", rect);
        return control;
    }

    private Label Text(string name, string text, Rect2 rect, Color ink, bool bold = false)
    {
        var label = new Label
        {
            Name = name, Text = text, TooltipText = text, ClipText = true, VerticalAlignment = VerticalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseFilter = MouseFilterEnum.Pass,
        };
        // Marked as styled so the detail shell keeps this card's own fonts and inks.
        label.SetMeta("classic_detail_control", true);
        label.AddThemeFontOverride("font", bold ? Plugin.Kit.Bold : Plugin.Kit.Regular);
        label.AddThemeFontSizeOverride("font_size", FontSize);
        label.AddThemeColorOverride("font_color", ink);
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        label.AddThemeConstantOverride("shadow_offset_x", 1); label.AddThemeConstantOverride("shadow_offset_y", 1);
        return Add(label, rect);
    }

    /// <summary>The clan podium and mark in the original inventory cell; the place number when there is no medal.</summary>
    private partial class ClassicUserInfoCell : Control
    {
        private readonly Texture2D? _texture;
        private readonly string _place;

        public ClassicUserInfoCell() { _place = ""; }

        public ClassicUserInfoCell(Texture2D? texture, string place)
        {
            _texture = texture; _place = place;
            MouseFilter = MouseFilterEnum.Pass; TextureFilter = TextureFilterEnum.Linear;
            SetMeta("classic_detail_control", true);
        }

        public bool HasArt => _texture != null;
        public string Place => _place;

        public override void _Draw()
        {
            var bounds = new Rect2(Vector2.Zero, Size);
            DrawStyleBox(ClassicSkin.PanelStyle(), bounds);
            var inner = bounds.Grow(-6);
            if (_texture != null)
            {
                var size = _texture.GetSize();
                float scale = Math.Min(inner.Size.X / size.X, inner.Size.Y / size.Y);
                var drawn = size * scale;
                DrawTextureRect(_texture, new Rect2(inner.Position + (inner.Size - drawn) / 2, drawn), false);
            }
            if (_place.Length == 0) return;
            var font = Plugin.Kit.Bold;
            var text = font.GetStringSize(_place, HorizontalAlignment.Left, -1, PlaceSize);
            var at = new Vector2((Size.X - text.X) / 2, (Size.Y - font.GetHeight(PlaceSize)) / 2 + font.GetAscent(PlaceSize));
            DrawString(font, at + Vector2.One, _place, HorizontalAlignment.Left, -1, PlaceSize, Colors.Black);
            DrawString(font, at, _place, HorizontalAlignment.Left, -1, PlaceSize, ClassicDesign.FrameTitle);
        }
    }
}

/// <summary>One rank ribbon of the client's art with its count over it; an empty tier draws nothing.</summary>
public partial class ClassicRankRibbon : Control
{
    private readonly GameRankRibbon _ribbon;

    public ClassicRankRibbon() { }

    public ClassicRankRibbon(GameRankRibbon ribbon)
    {
        _ribbon = ribbon;
        TooltipText = ribbon.Tooltip; MouseFilter = MouseFilterEnum.Pass;
        SetMeta("classic_detail_control", true);
    }

    public GameRankRibbon Ribbon => _ribbon;

    public override void _Draw()
    {
        if (string.IsNullOrEmpty(_ribbon.Count) && _ribbon.Ribbon == null) return;
        if (_ribbon.Ribbon != null) DrawTextureRect(_ribbon.Ribbon, new Rect2(Vector2.Zero, Size), false);
        else
        {
            // Without the client's ribbon art the count stays readable on a plain row cell.
            DrawRect(new Rect2(Vector2.Zero, Size).Grow(-1), new Color(0, 0, 0, .6f));
            DrawRect(new Rect2(Vector2.Zero, Size).Grow(-1), ClassicSocialStyle.RowRule, false, 1);
        }
        if (string.IsNullOrEmpty(_ribbon.Count)) return;
        var font = Plugin.Kit.Bold; const int size = 9;
        float width = font.GetStringSize(_ribbon.Count, HorizontalAlignment.Left, -1, size).X;
        var at = new Vector2(Mathf.Round((Size.X - width) / 2), Mathf.Round((Size.Y - font.GetHeight(size)) / 2 + font.GetAscent(size)));
        DrawStringOutline(font, at, _ribbon.Count, HorizontalAlignment.Left, -1, size, 3, new Color(0, 0, 0, .85f));
        DrawString(font, at, _ribbon.Count, HorizontalAlignment.Left, -1, size, ClassicDesign.FrameTitle);
    }
}
