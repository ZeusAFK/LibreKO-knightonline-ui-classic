using Godot;
using KnightOnlineUiClassic.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The original target bar (co_targetbar_us) with the client's quest-item rows under it (3277df5
/// <c>_targetDropRows</c>): one row per quest item the target drops, a 20 px item icon and "name: have/need" in the
/// HUD's white Arial Bold, centred under the bar like the client's own rows. The rows come from
/// <see cref="IGameTarget.QuestDrops"/> and follow every target change; the frame grows to hold them.
/// </summary>
public partial class TargetFrame : Control
{
    private const string LayoutName = "co_targetbar_us";
    private const string HpBar = "pro_target";
    private const string NameText = "text_target";
    private const float TopMargin = 8f;
    /// <summary>The quest-item rows column under the bar art.</summary>
    public const string DropRows = "quest_drops";
    /// <summary>The client's row metrics: a 20 px icon, 12 px text, 2 px between rows, 6 px between icon and text.</summary>
    public const int DropIcon = 20, DropFont = 12, DropGap = 2, DropIconGap = 6;
    /// <summary>Gap between the bar art and the first row.</summary>
    public const int DropsTop = 2;

    private readonly LayoutView _view;
    private readonly PluginGame _game;
    private readonly VBoxContainer _drops;
    private readonly Vector2 _artSize;

    public TargetFrame()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _view = new LayoutView(kit, kit.Layout(LayoutName));
        _artSize = _view.Size;
        Size = _artSize;
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_view);
        _view.Hide("Progress_HP_slow", "Progress_HP_drop", "Progress_HP_lasting", "Button_Board");
        _drops = new VBoxContainer
        {
            Name = DropRows, Position = new Vector2(0, _artSize.Y + DropsTop), Size = new Vector2(_artSize.X, 0),
            MouseFilter = MouseFilterEnum.Ignore, Visible = false,
        };
        _drops.AddThemeConstantOverride("separation", DropGap);
        AddChild(_drops);
        Visible = false;
    }

    /// <summary>The height of the bar art alone (the frame without quest rows).</summary>
    public float ArtHeight => _artSize.Y;

    public override void _EnterTree()
    {
        _game.Target.Changed += Refresh;
        _game.BecameAvailable += Refresh;
        _game.BecameUnavailable += Refresh;
        GetViewport().SizeChanged += Place;
        Place();
        Refresh();
    }

    public override void _ExitTree()
    {
        _game.Target.Changed -= Refresh;
        _game.BecameAvailable -= Refresh;
        _game.BecameUnavailable -= Refresh;
        GetViewport().SizeChanged -= Place;
    }

    private void Place()
    {
        float width = GetViewport().GetVisibleRect().Size.X;
        Position = new Vector2(Mathf.Round((width - Size.X) * 0.5f), TopMargin);
    }

    private void Refresh()
    {
        var t = _game.Target;
        Visible = _game.Available && t.Has;
        if (!Visible) return;
        string level = t.Level > 0 ? $"  Lv.{t.Level}" : "";
        if (_view.Get(NameText) is Label label)
        {
            label.Text = FitName(label, t.Name, level);
            label.TooltipText = t.Name + level;
        }
        _view.SetProgress(HpBar, t.MaxHp > 0 ? t.Hp / (float)t.MaxHp : 1f);
        RefreshDrops(t.QuestDrops);
    }

    /// <summary>Rebuilds the quest-item rows (the client rebuilds its own the same way) and sizes the frame around them.</summary>
    private void RefreshDrops(IReadOnlyList<GameTargetDrop> drops)
    {
        foreach (var child in _drops.GetChildren()) { _drops.RemoveChild(child); child.QueueFree(); }
        foreach (var drop in drops) _drops.AddChild(DropRow(drop, _artSize.X));
        _drops.Visible = drops.Count > 0;
        float rows = drops.Count > 0 ? DropsTop + drops.Count * (DropIcon + DropGap) - DropGap : 0;
        _drops.Size = new Vector2(_artSize.X, rows);
        Size = new Vector2(_artSize.X, _artSize.Y + rows);
    }

    private static HBoxContainer DropRow(GameTargetDrop drop, float width)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, DropIcon) };
        row.AddThemeConstantOverride("separation", DropIconGap);
        row.AddChild(new TextureRect
        {
            Name = "icon", Texture = drop.Icon, CustomMinimumSize = new Vector2(DropIcon, DropIcon), MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        });
        var line = new Label { Name = "line", Text = drop.Line, MouseFilter = MouseFilterEnum.Ignore, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        ClassicEventPanel.Bold(line, Colors.White, DropFont);
        line.AddThemeConstantOverride("outline_size", 1);
        line.AddThemeColorOverride("font_outline_color", Colors.Black);
        line.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        // The text keeps its own width so the pair stays centred; a long name is trimmed inside the bar's width.
        float text = line.GetThemeFont("font").GetStringSize(drop.Line, HorizontalAlignment.Left, -1, DropFont).X + 2;
        line.CustomMinimumSize = new Vector2(Mathf.Min(text, width - DropIcon - DropIconGap), DropIcon);
        row.AddChild(line);
        return row;
    }

    // Long names are shortened inside the frame; the level always stays visible.
    private static string FitName(Label label, string name, string level)
    {
        var font = label.GetThemeFont("font");
        int size = label.GetThemeFontSize("font_size");
        float room = label.Size.X - 8;
        bool Fits(string text) => font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X <= room;
        if (Fits(name + level)) return name + level;
        string shown = name;
        while (shown.Length > 1 && !Fits(shown.TrimEnd() + "…" + level)) shown = shown[..^1];
        return shown.TrimEnd() + "…" + level;
    }
}
