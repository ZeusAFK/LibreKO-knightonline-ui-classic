using Godot;
using LibreKO;
using LibreKO.Plugins;
using KnightOnlineUiClassic.Layout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The Classic party list in place of the client's party HUD: the original member cells driven by the
/// plugin party API, left click picks (and targets) a member, right click opens the client's member menu,
/// and the party actions (invite by name, leave or disband, party board, war board, row meter) call the
/// client's own handlers.
/// </summary>
public partial class ClassicPartyHud : Control
{
    private readonly IGameParty _party = null!;
    private readonly Func<World?> _world = null!;
    private readonly ClassicPartyHudRow[] _rows = new ClassicPartyHudRow[8];
    private readonly Control _actions = new() { Name = "party_actions", MouseFilter = MouseFilterEnum.Ignore };
    private readonly Button _leave = null!, _meter = null!;
    private readonly Control _grip = null!;
    private bool _queued;

    public ClassicPartyHud() { }

    public ClassicPartyHud(IGameParty party, Func<World?> world)
    {
        _party = party; _world = world;
        Name = "classic_party_hud"; MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.Nearest;
        var heading = Plugin.Kit.Layout(ClassicPartyHudLayout.Art).Strings.First();
        ClassicPartySkin.Caption(this, "Party", new Rect2(heading.Position, heading.SizeVec), heading.Color, heading.Size).Name = "party_caption";
        _grip = new Control { Name = "party_drag", Position = ClassicPartyHudLayout.Drag.Position, Size = ClassicPartyHudLayout.Drag.Size, MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.Move };
        AddChild(_grip);
        for (int i = 0; i < _rows.Length; i++)
        {
            var row = new ClassicPartyHudRow(this) { Name = "party_member_" + i, Visible = false };
            var rect = ClassicPartyHudLayout.Row(i); row.Position = rect.Position; row.Size = row.CustomMinimumSize = rect.Size;
            _rows[i] = row; AddChild(row);
        }
        AddChild(_actions);
        Action("Invite", 0, () => NativeParty.InviteByName(_world())).TooltipText = "Invite a player by name";
        _leave = Action("Leave", 1, () => _party.Leave());
        Action("Seek Party", 2, () => NativeParty.ToggleBoard(_world())).TooltipText = "Party board";
        Action("War Board", 3, () => NativeParty.ToggleWarBoard(_world())).TooltipText = "Party War Board";
        _meter = Action("Meter", -1, () => { NativeParty.ToggleMeter(_world()); QueueRefresh(); });
        _meter.TooltipText = "Party meter under each member: off, damage dealt or healing";
    }

    private Button Action(string text, int index, Action pressed)
    {
        var button = new Button { Text = text, Name = "party_" + text.ToLowerInvariant().Replace(' ', '_'), FocusMode = FocusModeEnum.None, ClipText = true };
        ClassicChatControls.SkinButton(button, Plugin.Kit.Layout(ClassicPartyHudLayout.ButtonArt).Find("btn_party")!);
        button.AddThemeFontOverride("font", Plugin.Kit.Bold); button.AddThemeFontSizeOverride("font_size", 10);
        foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color" }) button.AddThemeColorOverride(state, Plugin.Kit.Nation == 1 ? Colors.Black : new Color("ffff80"));
        ClassicPartySkin.KarusButtonContrast(button);
        var rect = index < 0 ? ClassicPartyHudLayout.MeterAction : ClassicPartyHudLayout.Action(index);
        _actions.AddChild(button); button.Position = rect.Position; button.Size = rect.Size;
        button.SetMeta("party_expected_rect", rect);
        button.Pressed += pressed;
        return button;
    }

    public IGameParty Party => _party;
    public World? World => _world();

    public override void _Ready()
    {
        _party.Changed += QueueRefresh;
        HudLayout.Attach(this, ClassicPartyHudLayout.LayoutId, _grip, () => ClassicPartyHudLayout.DefaultPosition);
        Refresh();
    }

    public override void _ExitTree() => _party.Changed -= QueueRefresh;

    private void QueueRefresh()
    {
        if (_queued) return;
        _queued = true;
        Callable.From(Refresh).CallDeferred();
    }

    public void Refresh()
    {
        _queued = false;
        if (!IsInsideTree()) return;
        var members = _party.InParty ? _party.Members : Array.Empty<GamePartyMember>();
        Visible = members.Count > 0;
        int shown = Math.Min(members.Count, _rows.Length);
        for (int i = 0; i < _rows.Length; i++)
        {
            _rows[i].Visible = i < shown;
            if (i < shown) _rows[i].Bind(members[i]);
        }
        _actions.Position = new Vector2(0, ClassicPartyHudLayout.RowsTop + shown * ClassicPartyHudLayout.RowPitch);
        _actions.Size = new Vector2(ClassicPartyHudLayout.Width, ClassicPartyHudLayout.ActionsHeight);
        // Theme sizes settle in the tree; keep every action at its art size.
        foreach (var button in _actions.GetChildren().OfType<Button>())
        {
            var rect = button.GetMeta("party_expected_rect").AsRect2();
            if (button.GetRect() != rect) { button.Position = rect.Position; button.Size = rect.Size; }
        }
        _leave.Text = _party.IsLeader ? "Disband" : "Leave";
        _leave.TooltipText = _party.IsLeader ? "Disband the party" : "Leave the party";
        _meter.Text = NativeParty.MeterMode switch { 1 => "Meter: Damage", 2 => "Meter: Healing", _ => "Meter: Off" };
        CustomMinimumSize = Size = new Vector2(ClassicPartyHudLayout.Width, ClassicPartyHudLayout.Height(shown));
    }
}

/// <summary>One original party member cell: HP bar with the status look, MP bar, name, meter and selection.</summary>
public partial class ClassicPartyHudRow : Control
{
    private readonly ClassicPartyHud _hud = null!;
    private readonly LayoutNode _layout = null!;
    private readonly Label _name = null!;
    private GamePartyMember _member;
    private bool _bound;
    private float _share;

    public ClassicPartyHudRow() { }

    public ClassicPartyHudRow(ClassicPartyHud hud)
    {
        _hud = hud; _layout = Plugin.Kit.Layout(ClassicPartyHudLayout.Art);
        MouseFilter = MouseFilterEnum.Stop;
        _name = ClassicPartySkin.Caption(this, "", ClassicPartyHudLayout.Name, new Color("8080ff"), 10);
        _name.Name = "party_member_name"; _name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        GuiInput += OnInput;
    }

    public GamePartyMember Member => _member;

    public void Bind(GamePartyMember member)
    {
        _member = member; _bound = true;
        _name.Text = member.Name;
        // Original party colours: the leader in the caption yellow, the commander green, members blue.
        _name.AddThemeColorOverride("font_color", member.Leader ? new Color("ffff80") : NativeParty.IsCommander(member.Id) ? new Color("00ff00") : new Color("8080ff"));
        TooltipText = member.Tooltip;
        _share = NativeParty.MeterShare(_hud.World, member.Id);
        QueueRedraw();
    }

    private void OnInput(InputEvent ev)
    {
        if (!_bound || ev is not InputEventMouseButton { Pressed: true } click) return;
        if (click.ButtonIndex == MouseButton.Left) { _hud.Party.Pick(_member.Id); AcceptEvent(); }
        else if (click.ButtonIndex == MouseButton.Right)
        {
            NativeParty.StyleMemberMenu(_hud.World, ClassicSocialStyle.Menu);
            _hud.Party.ShowMemberMenu(_member.Id);
            AcceptEvent();
        }
    }

    /// <summary>The client's HP look (normal, lasting, drop, slow, undead) on the matching original bar art.</summary>
    private string HpArt()
    {
        var hp = _member.HpColour;
        if (hp == Color.Color8(184, 152, 18)) return "progress_hp_0_lasting";
        if (hp == Color.Color8(161, 81, 107)) return "progress_hp_0_drop";
        if (hp == Color.Color8(98, 136, 27)) return "progress_hp_0_slow";
        return "progress_hp_0";
    }

    private void Bar(string id, Rect2 rect, float value, Color? tint = null)
    {
        var source = _layout.Find(id)!;
        foreach (var art in source.Images.OrderBy(a => a.Tag))
        {
            var dest = rect; var src = new Rect2(art.SrcX, art.SrcY, art.SrcW, art.SrcH);
            if (art.Tag == 1) { dest.Size *= new Vector2(value, 1); src.Size *= new Vector2(value, 1); }
            if (dest.Size.X > 0) DrawTextureRectRegion(Plugin.Kit.Texture(art.Texture!), dest, src, art.Tag == 1 ? tint : null);
        }
    }

    public override void _Draw()
    {
        if (!_bound) return;
        bool undead = _member.HpColour == Color.Color8(105, 105, 105);
        if (_member.MaxHp > 0) Bar(HpArt(), ClassicPartyHudLayout.Hp, Fill(_member.Hp, _member.MaxHp), undead ? new Color(.55f, .55f, .55f) : null);
        if (_member.MaxMp > 0) Bar("progress_mp_0_curse", ClassicPartyHudLayout.Mp, Fill(_member.Mp, _member.MaxMp));
        byte mode = NativeParty.MeterMode;
        if (mode != 0)
        {
            var meter = ClassicPartyHudLayout.Meter;
            DrawRect(meter, new Color(0, 0, 0, .9f));
            DrawRect(new Rect2(meter.Position, new Vector2(meter.Size.X * _share, meter.Size.Y)), mode == 2 ? ClassicPartyHudLayout.MeterHealing : ClassicPartyHudLayout.MeterDamage);
        }
        if (_member.Picked) DrawRect(ClassicPartyHudLayout.Selection, new Color("00ff00"), false, 1);
    }

    private static float Fill(int value, int max) => max <= 0 ? 0 : Mathf.Clamp(value / (float)max, 0, 1);
}
