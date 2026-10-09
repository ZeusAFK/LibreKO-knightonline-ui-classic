using Godot;
using KnightOnlineUiClassic.Layout;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Windows;

public partial class TargetFrame : Control
{
    private const string LayoutName = "co_targetbar_us";
    private const string HpBar = "pro_target";
    private const string NameText = "text_target";
    private const float TopMargin = 8f;

    private readonly LayoutView _view;
    private readonly PluginGame _game;

    public TargetFrame()
    {
        var kit = Plugin.Kit;
        _game = kit.Game;
        _view = new LayoutView(kit, kit.Layout(LayoutName));
        Size = _view.Size;
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_view);
        _view.Hide("Progress_HP_slow", "Progress_HP_drop", "Progress_HP_lasting", "Button_Board");
        Visible = false;
    }

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
