using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Native;
using Spec = KnightOnlineUiClassic.Layout.ClassicQuestHudLayout.Tracker;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The client's active quest tracker in the Classic look: the plain thin frame over a translucent ground at the
/// width of the original quest notify box (co_quest_mini_tip_us), "Quests" in the frame title gold with the count,
/// the whisper window's hide/open button as the fold button, each tracked quest as a name row (hover lit; click opens the journal at
/// the quest) with its objectives and slim progress bars, and the Level Guide arrows around the page number.
/// The client keeps building every row, clicking, folding, paging and dragging; rows it rebuilds are styled as
/// they arrive.
/// </summary>
public static class ClassicQuestTracker
{
    public const string Skinned = "classic_quest_tracker", Row = "classic_tracker_row";

    public static void Register() => NativeQuestHud.Extend(NativeQuestHudPart.Tracker, node => Apply((PanelContainer)node));

    public static void Apply(PanelContainer panel)
    {
        if (panel.HasMeta(Skinned) || panel.FindChild(NativeQuestHud.TrackerRows, false, false) is not VBoxContainer rows) return;
        panel.SetMeta(Skinned, true);
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        panel.CustomMinimumSize = new Vector2(Spec.Width, 0);
        var frame = new ClassicFrame { Name = "classic_frame", BackgroundColor = Colors.Black, BackgroundAlpha = Spec.Alpha };
        panel.AddChild(frame);
        panel.MoveChild(frame, 0);
        // The client's row column moves into an even padding inside the frame; the client keeps filling it.
        var padding = new MarginContainer { Name = "classic_tracker_padding", MouseFilter = Control.MouseFilterEnum.Ignore };
        padding.AddThemeConstantOverride("margin_left", Spec.PadX); padding.AddThemeConstantOverride("margin_right", Spec.PadX);
        padding.AddThemeConstantOverride("margin_top", Spec.PadY); padding.AddThemeConstantOverride("margin_bottom", Spec.PadY);
        panel.AddChild(padding);
        rows.Reparent(padding);
        rows.CustomMinimumSize = new Vector2(Spec.ContentWidth, 0);
        rows.AddThemeConstantOverride("separation", Spec.RowGap);
        foreach (var child in rows.GetChildren()) Style(child);
        rows.ChildEnteredTree += Style;
        // The client refits the tracker after listing quests but not after folding it; the frame follows the rows either way.
        rows.ChildEnteredTree += _ => Fit(panel);
        rows.ChildExitingTree += _ => Fit(panel);
        panel.ResetSize();
    }

    private const string FitPending = "classic_tracker_fit";

    /// <summary>Refits the tracker that holds <paramref name="node"/> (the fold art changes the header after the rows do).</summary>
    internal static void FitAround(Node node)
    {
        for (var current = node; current != null; current = current.GetParent())
            if (current is PanelContainer panel && panel.HasMeta(Skinned)) { Fit(panel); return; }
    }

    private static void Fit(PanelContainer panel)
    {
        if (panel.HasMeta(FitPending)) return;
        panel.SetMeta(FitPending, true);
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(panel)) return;
            panel.RemoveMeta(FitPending);
            panel.ResetSize();
        }).CallDeferred();
    }

    private static void Style(Node node)
    {
        if (node is not Control control || control.HasMeta(Row)) return;
        control.SetMeta(Row, true);
        if (control.Name == NativeQuestHud.TrackerHeader && control is PanelContainer header) Header(header);
        else if (control is HBoxContainer pager && pager.GetChildren().OfType<Button>().Any()) Pager(pager);
        else if (control is VBoxContainer quest) Quest(quest);
    }

    private static void Header(PanelContainer header)
    {
        header.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        header.CustomMinimumSize = new Vector2(0, Spec.HeaderHeight);
        if (header.GetChildren().OfType<HBoxContainer>().FirstOrDefault() is not { } bar) return;
        bar.AddThemeConstantOverride("separation", 6);
        foreach (var label in bar.GetChildren().OfType<Label>())
        {
            bool count = label.Name == NativeQuestHud.TrackerCount;
            Text(label, count ? ClassicDesign.Muted : ClassicDesign.FrameTitle, count ? Spec.QuestFont : Spec.TitleFont);
            label.VerticalAlignment = VerticalAlignment.Center;
        }
        foreach (var fold in bar.GetChildren().OfType<Button>())
        {
            fold.CustomMinimumSize = Spec.Fold;
            fold.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            fold.FocusMode = Control.FocusModeEnum.None;
            fold.AddChild(new ClassicTrackerFold(fold) { Name = "classic_fold" });
        }
    }

    private static void Quest(VBoxContainer quest)
    {
        quest.CustomMinimumSize = new Vector2(1, 0);
        quest.AddThemeConstantOverride("separation", 2);
        foreach (var node in Native.Native.Descendants(quest).OfType<Control>())
        {
            // The client sizes every part to its wider tracker; the parts follow the frame's content width.
            if (node is Container fixedWidth && fixedWidth.CustomMinimumSize.X > 1) fixedWidth.CustomMinimumSize = new Vector2(1, fixedWidth.CustomMinimumSize.Y);
            switch (node)
            {
                case PanelContainer title:
                    // The name row: transparent, lit while hovered; the client's click opens the journal.
                    title.CustomMinimumSize = new Vector2(1, 0);
                    var plain = new StyleBoxEmpty { ContentMarginLeft = 2, ContentMarginRight = 2, ContentMarginTop = 1, ContentMarginBottom = 1 };
                    var lit = new StyleBoxFlat { BgColor = ClassicDarkFrame.SelectedRow, ContentMarginLeft = 2, ContentMarginRight = 2, ContentMarginTop = 1, ContentMarginBottom = 1 };
                    title.AddThemeStyleboxOverride("panel", plain);
                    title.MouseEntered += () => title.AddThemeStyleboxOverride("panel", lit);
                    title.MouseExited += () => title.AddThemeStyleboxOverride("panel", plain);
                    break;
                case HBoxContainer line:
                    line.AddThemeConstantOverride("separation", 0);
                    var labels = line.GetChildren().OfType<Label>().ToArray();
                    // The client's diamond and tick glyphs are not Classic; a ready quest shows in the ready ink.
                    if (labels.Length > 1) labels[0].Visible = false;
                    break;
                case Label label:
                    if (!label.Visible) break;
                    bool ready = label.GetThemeColor("font_color").IsEqualApprox(UiTheme.Good);
                    bool name = label.GetParent() is HBoxContainer;
                    Text(label, ready ? ClassicQuestStatus.Ink(LibreKO.Network.QuestViewState.Claimable) : name ? ClassicDarkFrame.Caption : ClassicDesign.Text,
                        name ? Spec.QuestFont : Spec.ObjectiveFont);
                    label.CustomMinimumSize = new Vector2(1, 0);
                    label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    break;
                case MarginContainer bar when bar.GetChildren().OfType<ColorRect>().Any():
                    bar.AddThemeConstantOverride("margin_left", Spec.BarIndent); bar.AddThemeConstantOverride("margin_right", Spec.BarIndent);
                    break;
                case ColorRect track when track.GetParent() is MarginContainer:
                    track.CustomMinimumSize = new Vector2(0, Spec.BarHeight);
                    track.Color = new Color("1d1b17");
                    foreach (var fill in track.GetChildren().OfType<ColorRect>())
                        fill.Color = fill.Color.IsEqualApprox(UiTheme.Good) ? ClassicQuestStatus.Ink(LibreKO.Network.QuestViewState.Claimable) : ClassicDarkFrame.Caption;
                    break;
            }
        }
    }

    private static void Pager(HBoxContainer pager)
    {
        pager.CustomMinimumSize = new Vector2(1, Spec.PagerHeight);
        pager.AddThemeConstantOverride("separation", 0);
        foreach (var button in pager.GetChildren().OfType<Button>())
            ClassicQuestNotice.Arrow(button, button.TooltipText.StartsWith("Next"), Spec.Arrow);
        foreach (var page in pager.GetChildren().OfType<Label>())
        {
            Text(page, ClassicQuestHudLayout.PageInk, Spec.PageFont);
            page.HorizontalAlignment = HorizontalAlignment.Center; page.VerticalAlignment = VerticalAlignment.Center;
        }
    }

    private static void Text(Label label, Color ink, int size)
    {
        ClassicEventPanel.Bold(label, ink, size);
        label.AddThemeConstantOverride("outline_size", 0);
        label.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
    }
}

/// <summary>
/// The tracker's fold button on the original whisper window's hide and open buttons (the minimise and restore pair
/// the original quest notify box has): hide folds the list, open unfolds it. The client keeps
/// toggling the fold and writing its glyph; the glyph is read once per change and replaced by the arrow.
/// </summary>
public partial class ClassicTrackerFold : Node
{
    private const string Folded = "▸";
    private readonly Button _button;
    private bool? _folded;
    public ClassicTrackerFold() { _button = null!; }
    public ClassicTrackerFold(Button button) { _button = button; Sync(); }
    public bool IsFolded => _folded == true;
    public override void _Process(double delta) => Sync();

    private void Sync()
    {
        if (_button == null || !GodotObject.IsInstanceValid(_button) || _button.Text.Length == 0) return;
        bool folded = _button.Text == Folded;
        _button.Text = "";
        if (_folded == folded) return;
        _folded = folded;
        ClassicChatControls.SkinButton(_button, folded ? Plugin.Kit.Layout("co_whisper_close_us").Find("btn_open")! : Plugin.Kit.Layout("co_whisper_open_us").Find("btn_hide")!);
        _button.TooltipText = folded ? "Show the tracked quests" : "Fold the tracked quests";
        ClassicQuestTracker.FitAround(_button);
    }
}
