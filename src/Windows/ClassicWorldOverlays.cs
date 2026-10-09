using Godot;
using KnightOnlineUiClassic.Layout;
using LibreKO;
using Spec = KnightOnlineUiClassic.Layout.ClassicOverlayLayout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// Classic skins of the native world overlays. Nothing here has its own window art in 1.298 except
/// the loot grid, so every overlay is composed from original pieces: prompts from the common message
/// box (the same art and type as <see cref="MessageBox"/>), one-line plates and chips from the nation
/// information plaque and its chat-tab buttons, countdown bars from the original progress art, and
/// broadcast banners as outlined Arial Bold text like the original centre notices. The client's own
/// controls are restyled or moved in place; their callbacks, timers and state stay native.
/// </summary>
public static class ClassicWorldOverlays
{
    private const string Skinned = "classic_overlay";
    public const string ExpectedRect = "overlay_expected_rect";

    public static void Register()
    {
        NativeOverlays.Extend(NativeOverlay.Registration, node => Once(node, () => Registration((Control)node)));
        NativeOverlays.Extend(NativeOverlay.Countdown, node => Once(node, () => Countdown((PanelContainer)node)));
        NativeOverlays.Extend(NativeOverlay.Leave, node => Once(node, () => Leave((PanelContainer)node)));
        NativeOverlays.Extend(NativeOverlay.Death, node => Once(node, () => Death((PanelContainer)node)));
        NativeOverlays.Extend(NativeOverlay.Prompt, node => Once(node, () => Prompt((AcceptDialog)node)));
        NativeOverlays.Extend(NativeOverlay.Announcement, node => Once(node, () => Announcement((Control)node)));
        NativeOverlays.Extend(NativeOverlay.Chip, node => Once(node, () => Chip((PanelContainer)node)));
        NativeOverlays.Extend(NativeOverlay.Score, node => Once(node, () => Score((PanelContainer)node)));
        NativeOverlays.Extend(NativeOverlay.Menu, node => Once(node, () => Menu((PopupMenu)node)));
        NativeOverlays.Extend(NativeOverlay.Loot, node => Once(node, () => Loot((PanelContainer)node)));
    }

    private static void Once(Node node, Action skin)
    {
        if (node.HasMeta(Skinned)) return;
        node.SetMeta(Skinned, true);
        skin();
    }

    // ---- Palette (original art strings) -----------------------------------------------------------

    /// <summary>Message box title and message ink (<c>Text_title</c>, <c>Text_Message</c>).</summary>
    public static Color MessageTitle => new("ffff00");
    public static Color MessageText => Colors.White;
    /// <summary>Information plaque caption: El Morad light on dark slate, Karus black on light stone.</summary>
    public static Color PlaqueInk => ClassicDesign.Karus ? Colors.Black : new Color("ffff80");
    public static Color PlaqueShadow => ClassicDesign.Karus ? new Color("c7c5be") : Colors.Black;
    /// <summary>Chat tab caption ink.</summary>
    public static Color TabInk => ClassicDesign.Karus ? Colors.Black : new Color("f8ead1");
    /// <summary>Urgent countdown ink on the dark message box and on each nation plaque.</summary>
    public static Color UrgentOnMessage => new("ff6a5a");
    public static Color UrgentOnPlaque => ClassicDesign.Karus ? new Color("7a160c") : new Color("ff6a5a");

    // ---- Shared pieces ------------------------------------------------------------------------------

    private static T? Find<T>(Node root, string name) where T : Node => root.FindChild(name, true, false) as T;

    internal static IEnumerable<Node> Descendants(Node root)
    {
        yield return root;
        foreach (var child in root.GetChildren())
            foreach (var node in Descendants(child)) yield return node;
    }

    internal static void Text(Label label, int size, Color ink, bool bold = true, Color? shadow = null)
    {
        label.AddThemeFontOverride("font", bold ? Plugin.Kit.Bold : Plugin.Kit.Regular);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeConstantOverride("outline_size", 0);
        label.AddThemeColorOverride("font_color", ink);
        label.AddThemeColorOverride("font_shadow_color", shadow ?? new Color(0, 0, 0, .75f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeConstantOverride("shadow_outline_size", 0);
    }

    /// <summary>Sets a label's ink only when it changed, so per-frame syncs cause no theme updates.</summary>
    private static void Ink(Label label, Color ink)
    {
        if (label.GetThemeColor("font_color") != ink) label.AddThemeColorOverride("font_color", ink);
    }

    private static void Place(Control control, Control parent, Rect2 rect)
    {
        ClassicVendorSkin.Move(control, parent, rect);
        control.SetMeta(ExpectedRect, rect);
    }

    /// <summary>The message box buttons: original art per state, the native text on a shadowed caption.</summary>
    internal static void MessageButton(Button button, bool cancel = false)
    {
        var art = Plugin.Kit.Layout("{nation}_messagebox_us").Find(cancel ? "btn_cancel" : "btn_ok")!;
        ArtButton(button, art, 12, false, Colors.White, new Color(0, 0, 0, .75f), new Color("8a8a8a"));
    }

    /// <summary>The chat tab art used as an action on a plaque.</summary>
    internal static void TabButton(Button button)
    {
        var art = Plugin.Kit.Layout("{nation}_chat_us").Find("btn_normal")!;
        ArtButton(button, art, 12, false, TabInk, ClassicDesign.Karus ? Colors.Transparent : new Color(0, 0, 0, .75f),
            ClassicDesign.Karus ? new Color("57544f") : new Color("8a8a8a"));
    }

    private static void ArtButton(Button button, LayoutNode art, int size, bool bold, Color ink, Color shadow, Color disabled)
    {
        ClassicChatControls.SkinButton(button, art);
        var normal = (StyleBoxTexture)button.GetThemeStylebox("normal").Duplicate();
        normal.ModulateColor = new Color(.6f, .6f, .6f);
        button.AddThemeStyleboxOverride("disabled", normal);
        button.FocusMode = Control.FocusModeEnum.None;
        button.Icon = null;
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color", "font_disabled_color" })
            button.AddThemeColorOverride(state, Colors.Transparent);
        button.AddThemeConstantOverride("outline_size", 0);
        if (button.GetNodeOrNull<ClassicButtonCaption>("caption") == null)
            button.AddChild(new ClassicButtonCaption(button, size, bold, ink, shadow, disabled) { Name = "caption" });
    }

    private static StyleBoxTexture Bar(Rect2 source)
    {
        var box = new StyleBoxTexture { Texture = Plugin.Kit.Texture("ui_message_us.png"), RegionRect = source };
        box.SetTextureMargin(Side.Left, 2); box.SetTextureMargin(Side.Right, 2);
        box.SetContentMarginAll(0);
        return box;
    }

    /// <summary>Original progress art: the groove and the orange (calm) or red (urgent) fill.</summary>
    internal static void ProgressBar(ProgressBar bar, bool urgent)
    {
        // The whole groove, with its light rims, so an almost empty bar still shows its track.
        bar.AddThemeStyleboxOverride("background", Bar(new Rect2(321, 445, 179, 16)));
        bar.AddThemeStyleboxOverride("fill", Bar(urgent ? new Rect2(323, 462, 175, 12) : new Rect2(323, 389, 175, 12)));
        bar.SetMeta(NativeOverlays.Urgent, urgent);
    }

    private static void SyncBar(ProgressBar bar, bool urgent)
    {
        if (bar.GetMeta(NativeOverlays.Urgent, false).AsBool() != urgent) ProgressBar(bar, urgent);
    }

    /// <summary>Runs <paramref name="sync"/> every frame, before drawing, while <paramref name="owner"/> is visible.</summary>
    private static void Keep(Control owner, Action sync)
    {
        owner.AddChild(new NativeEventWatcher(() => { if (owner.IsVisibleInTree()) sync(); }) { Name = "classic_overlay_sync", ProcessPriority = int.MaxValue - 1 });
        sync();
    }

    /// <summary>Replaces a native plate's flat panel with the information plaque.</summary>
    private static void Plaque(PanelContainer panel, float left, float top, float right, float bottom)
    {
        var box = new StyleBoxEmpty { ContentMarginLeft = left, ContentMarginTop = top, ContentMarginRight = right, ContentMarginBottom = bottom };
        panel.AddThemeStyleboxOverride("panel", box);
        panel.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        panel.Draw += () => ClassicPlaqueArt.Draw(panel, new Rect2(Vector2.Zero, panel.Size));
        panel.QueueRedraw();
    }

    // ---- Message box overlays -----------------------------------------------------------------------

    private static void Registration(Control host)
    {
        if (host.GetChildren().OfType<CenterContainer>().FirstOrDefault() is not { } native) return;
        if (Find<Label>(host, "registration_title") is not { } title || Find<Label>(host, "registration_timer") is not { } timer
            || Find<ProgressBar>(host, "registration_bar") is not { } bar || Find<Label>(host, "registration_status") is not { } status
            || Find<Button>(host, "registration_join") is not { } join || Find<Button>(host, "registration_close") is not { } close) return;
        native.Visible = false;
        var panel = new ClassicMessagePanel(Spec.Registration.Size, centre: true) { Name = "classic_registration" };
        host.AddChild(panel);
        Place(title, panel, Spec.Registration.Title); Text(title, 13, MessageTitle);
        title.HorizontalAlignment = HorizontalAlignment.Center; title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        Place(timer, panel, Spec.Registration.Timer); Text(timer, 15, MessageText);
        timer.HorizontalAlignment = HorizontalAlignment.Center;
        Place(bar, panel, Spec.Registration.Bar); ProgressBar(bar, false);
        Place(status, panel, Spec.Registration.Status); Text(status, 13, ClassicDesign.Text);
        status.ClipText = false; status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        status.HorizontalAlignment = HorizontalAlignment.Center; status.VerticalAlignment = VerticalAlignment.Center;
        Place(join, panel, Spec.Registration.Join); MessageButton(join);
        Place(close, panel, Spec.Registration.Close); MessageButton(close, cancel: true);
        Keep(panel, () =>
        {
            bool urgent = host.GetMeta(NativeOverlays.Urgent, false).AsBool();
            Ink(timer, urgent ? UrgentOnMessage : MessageText);
            SyncBar(bar, urgent);
            Ink(status, host.GetMeta(NativeOverlays.Registered, false).AsBool() ? ClassicEventPanel.Ready : ClassicDesign.Text);
        });
    }

    private static void Death(PanelContainer native)
    {
        if (native.GetChildren().OfType<MarginContainer>().FirstOrDefault() is not { } content) return;
        var labels = Descendants(content).OfType<Label>().ToArray();
        if (labels.Length < 2 || Descendants(content).OfType<Button>().FirstOrDefault() is not { } ok) return;
        native.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        native.CustomMinimumSize = Spec.Death.Size;
        var panel = new ClassicMessagePanel(Spec.Death.Size, centre: false) { Name = "classic_death" };
        native.AddChild(panel);
        content.Visible = false;
        var (loss, hint) = (labels[0], labels[1]);
        Place(loss, panel, Spec.Death.Loss); Text(loss, 13, MessageTitle);
        loss.AutowrapMode = TextServer.AutowrapMode.Off; loss.HorizontalAlignment = HorizontalAlignment.Center; loss.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        Place(hint, panel, Spec.Death.Hint); Text(hint, 15, MessageText);
        hint.ClipText = false; hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hint.HorizontalAlignment = HorizontalAlignment.Center; hint.VerticalAlignment = VerticalAlignment.Center;
        Place(ok, panel, Spec.Death.Ok); MessageButton(ok);
        // The native panel centres on its anchors; the box keeps the original size on whole pixels.
        void Centre()
        {
            native.OffsetLeft = -Mathf.Ceil(Spec.Death.Size.X / 2); native.OffsetRight = native.OffsetLeft + Spec.Death.Size.X;
            native.OffsetTop = -Mathf.Ceil(Spec.Death.Size.Y / 2); native.OffsetBottom = native.OffsetTop + Spec.Death.Size.Y;
            var at = native.Position.Round(); if (at != native.Position) native.Position = at;
        }
        Keep(native, () => { if (native.Size != Spec.Death.Size || native.Position != native.Position.Round()) Centre(); });
    }

    private static void Prompt(AcceptDialog dialog)
    {
        dialog.Borderless = true; dialog.Transparent = true; dialog.TransparentBg = true;
        var box = new StyleBoxEmpty();
        box.SetContentMargin(Side.Top, Spec.Message.Text.Position.Y);
        box.SetContentMargin(Side.Left, Spec.Message.Text.Position.X);
        box.SetContentMargin(Side.Right, Spec.Message.Width - Spec.Message.Text.End.X);
        box.SetContentMargin(Side.Bottom, Spec.Message.ButtonBottom);
        dialog.AddThemeStyleboxOverride("panel", box);
        dialog.AddThemeConstantOverride("buttons_min_width", (int)Spec.Message.Button.X);
        dialog.AddThemeConstantOverride("buttons_min_height", (int)Spec.Message.Button.Y);
        // The dialog's button separation is the vertical gap under the message: the original message band ends at 86.
        dialog.AddThemeConstantOverride("buttons_separation", (int)(Spec.Message.Height - Spec.Message.ButtonBottom - Spec.Message.Button.Y - Spec.Message.Text.End.Y));
        dialog.DialogAutowrap = true;
        dialog.MinSize = new Vector2I((int)Spec.Message.Width, (int)Spec.Message.Height);
        dialog.MaxSize = new Vector2I((int)Spec.Message.Width, 4096);
        dialog.Size = dialog.MinSize;
        var label = dialog.GetLabel();
        Text(label, 15, MessageText);
        label.HorizontalAlignment = HorizontalAlignment.Center; label.VerticalAlignment = VerticalAlignment.Center;
        MessageButton(dialog.GetOkButton());
        if (dialog is ConfirmationDialog confirmation)
        {
            var cancel = confirmation.GetCancelButton();
            MessageButton(cancel, cancel: true);
            // The original two buttons sit 19 pixels apart, centred. The dialog keeps a spacer between them (and
            // re-shows it with the buttons), so it becomes a fixed 1-pixel gap between two 9-pixel separations.
            if (cancel.GetParent() is HBoxContainer row)
            {
                int gap = (int)Spec.Message.ButtonGap;
                row.AddThemeConstantOverride("separation", gap / 2);
                var items = row.GetChildren(true).OfType<Control>().ToList();
                int from = Math.Min(items.IndexOf(cancel), items.IndexOf(dialog.GetOkButton())), to = Math.Max(items.IndexOf(cancel), items.IndexOf(dialog.GetOkButton()));
                for (int i = from + 1; i < to; i++)
                    if (items[i] is not Button) { items[i].SizeFlagsHorizontal = Control.SizeFlags.Fill; items[i].CustomMinimumSize = new Vector2(gap - gap / 2 * 2, 0); }
            }
        }
        // Decoration stays outside AcceptDialog's direct child layout.
        var decoration = new Node { Name = "classic_prompt" };
        dialog.AddChild(decoration);
        var art = new ClassicMessageArt { Name = "message_art", ZIndex = -1 };
        decoration.AddChild(art);
        var title = new Label { Name = "prompt_title", Position = Spec.Message.Title.Position, Size = Spec.Message.Title.Size,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, ClipText = true,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        Text(title, 13, MessageTitle);
        title.SetMeta(ExpectedRect, Spec.Message.Title);
        decoration.AddChild(title);
        art.AddChild(new NativeEventWatcher(() =>
        {
            if (!dialog.Visible) return;
            art.Position = Vector2.Zero;
            art.Size = new Vector2(Spec.Message.Width, Math.Max(Spec.Message.Height, dialog.Size.Y));
            title.Text = dialog.Title;
        }) { Name = "classic_prompt_sync" });
    }

    // ---- Plates, chips and strips -------------------------------------------------------------------

    private static void Countdown(PanelContainer plate)
    {
        if (plate.GetChildren().OfType<MarginContainer>().FirstOrDefault() is not { } content
            || Find<Label>(plate, "countdown_title") is not { } title || Find<Label>(plate, "countdown_timer") is not { } timer
            || Find<ProgressBar>(plate, "countdown_bar") is not { } bar) return;
        Plaque(plate, 0, 0, 0, 0);
        plate.CustomMinimumSize = Spec.Countdown.Size;
        var surface = new Control { Name = "classic_countdown", MouseFilter = Control.MouseFilterEnum.Ignore };
        plate.AddChild(surface);
        content.Visible = false;
        Place(title, surface, Spec.Countdown.Title); Text(title, 13, PlaqueInk, shadow: PlaqueShadow);
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; title.MouseFilter = Control.MouseFilterEnum.Ignore;
        Place(timer, surface, Spec.Countdown.Timer); Text(timer, 13, PlaqueInk, shadow: PlaqueShadow);
        timer.HorizontalAlignment = HorizontalAlignment.Right; timer.MouseFilter = Control.MouseFilterEnum.Ignore;
        Place(bar, surface, Spec.Countdown.Bar); ProgressBar(bar, false); bar.MouseFilter = Control.MouseFilterEnum.Ignore;
        plate.ResetSize();
        Keep(plate, () =>
        {
            bool urgent = plate.GetMeta(NativeOverlays.Urgent, false).AsBool();
            Ink(timer, urgent ? UrgentOnPlaque : PlaqueInk);
            SyncBar(bar, urgent);
        });
    }

    private static void Leave(PanelContainer plate)
    {
        if (plate.GetChildren().OfType<MarginContainer>().FirstOrDefault() is not { } content
            || Find<Label>(plate, "leave_title") is not { } title || Find<Button>(plate, "leave_action") is not { } action) return;
        Plaque(plate, 0, 0, 0, 0);
        plate.CustomMinimumSize = Spec.Leave.Size;
        var surface = new Control { Name = "classic_leave", MouseFilter = Control.MouseFilterEnum.Ignore };
        plate.AddChild(surface);
        // The grip glyph goes with the native row; the whole plaque stays the drag handle (move cursor, tooltip).
        content.Visible = false;
        Place(title, surface, Spec.Leave.Title); Text(title, 13, PlaqueInk, shadow: PlaqueShadow);
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; title.MouseFilter = Control.MouseFilterEnum.Ignore;
        Place(action, surface, Spec.Leave.Action); TabButton(action);
        plate.ResetSize();
    }

    private static void Chip(PanelContainer chip)
    {
        Plaque(chip, Spec.Plaque.Inset, 3, Spec.Plaque.Inset, 3);
        chip.CustomMinimumSize = new Vector2(0, Spec.Plaque.Height);
        // The chip hangs from its top offset; drop the flat panel's taller padding.
        chip.OffsetBottom = chip.OffsetTop + Spec.Plaque.Height;
        var labels = Descendants(chip).OfType<Label>().ToArray();
        foreach (var label in labels) { Text(label, 13, PlaqueInk, shadow: PlaqueShadow); label.VerticalAlignment = VerticalAlignment.Center; }
        // The zone name carries the zone type; the native per-type tint has no place on the plaque.
        Keep(chip, () => { foreach (var label in labels) Ink(label, PlaqueInk); });
    }

    private static void Score(PanelContainer strip)
    {
        Plaque(strip, 12, 5, 12, 5);
        var labels = Descendants(strip).OfType<Label>().ToArray();
        foreach (var label in labels) Text(label, 13, PlaqueInk, shadow: PlaqueShadow);
        // The nation colours move to the original red and blue bars under each name; on the plaque every
        // caption keeps the plaque ink, which the client's per-update status tints would otherwise replace.
        foreach (var part in Descendants(strip).OfType<ColorRect>().ToArray())
        {
            bool karus = part.Color.R > part.Color.B;
            var source = karus ? new Rect2(323, 462, 175, 12) : new Rect2(323, 403, 175, 12);
            part.Color = Colors.Transparent;
            part.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            part.Draw += () => part.DrawStyleBox(Bar(source), new Rect2(Vector2.Zero, part.Size));
            if (part.GetParent() is Control row) row.CustomMinimumSize = new Vector2(row.CustomMinimumSize.X, Spec.Plaque.BarHeight);
        }
        Keep(strip, () => { foreach (var label in labels) Ink(label, PlaqueInk); });
    }

    private static void Announcement(Control node)
    {
        if (node is PanelContainer panel) panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        foreach (var label in Descendants(node).OfType<Label>())
        {
            int size = label.GetThemeFontSize("font_size");
            label.AddThemeFontOverride("font", Plugin.Kit.Bold);
            label.AddThemeFontSizeOverride("font_size", Math.Max(13, size));
            label.AddThemeConstantOverride("font_embolden", 0);
            label.AddThemeColorOverride("font_outline_color", Colors.Black);
            label.AddThemeConstantOverride("outline_size", 3);
            label.AddThemeColorOverride("font_shadow_color", Colors.Black);
            label.AddThemeConstantOverride("shadow_offset_x", 1);
            label.AddThemeConstantOverride("shadow_offset_y", 1);
        }
    }

    private static void Menu(PopupMenu menu)
    {
        var panel = ClassicDesign.InputBox();
        panel.BgColor = new Color(0, 0, 0, .94f);
        panel.SetContentMarginAll(4);
        menu.AddThemeStyleboxOverride("panel", panel);
        var hover = new StyleBoxFlat { BgColor = ClassicReportDesign.SelectedRow };
        hover.SetContentMarginAll(0);
        menu.AddThemeStyleboxOverride("hover", hover);
        var rule = new StyleBoxLine { Color = new Color(ClassicDesign.Karus ? "77746e" : "8e7545"), Thickness = 1 };
        menu.AddThemeStyleboxOverride("separator", rule);
        menu.AddThemeFontOverride("font", Plugin.Kit.Regular);
        menu.AddThemeFontSizeOverride("font_size", ClassicDesign.FontSize);
        menu.AddThemeFontOverride("font_separator", Plugin.Kit.Bold);
        menu.AddThemeFontSizeOverride("font_separator_size", ClassicDesign.FontSize);
        menu.AddThemeColorOverride("font_color", ClassicDesign.Text);
        menu.AddThemeColorOverride("font_hover_color", ClassicDesign.Heading);
        menu.AddThemeColorOverride("font_disabled_color", ClassicDesign.Muted);
        menu.AddThemeColorOverride("font_separator_color", ClassicReportDesign.Caption);
        menu.AddThemeConstantOverride("v_separation", 6);
        menu.AddThemeConstantOverride("item_start_padding", 6);
        menu.AddThemeConstantOverride("item_end_padding", 6);
    }

    private static void Loot(PanelContainer native)
    {
        if (native.GetChildren().OfType<MarginContainer>().FirstOrDefault() is not { } content
            || Find<HBoxContainer>(native, "loot_header") is not { } header || Find<Label>(native, "loot_title") is not { } title
            || Find<Button>(native, "loot_close") is not { } close || Find<Container>(native, "loot_rows") is not { } rows) return;
        native.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        native.CustomMinimumSize = Spec.Loot.Size;
        var panel = new ClassicLootPanel(rows) { Name = "classic_loot" };
        native.AddChild(panel);
        content.Visible = false;
        // The native title row stays the drag handle.
        ClassicVendorSkin.Move(header, panel, Spec.Loot.Header);
        header.SetMeta(ExpectedRect, Spec.Loot.Header);
        header.AddThemeConstantOverride("separation", 0);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        Text(title, 13, PlaqueInk, shadow: PlaqueShadow);
        title.VerticalAlignment = VerticalAlignment.Center; title.ClipText = true;
        title.CustomMinimumSize = new Vector2(0, Spec.Plaque.Height);
        var indent = new Control { Name = "loot_indent", CustomMinimumSize = new Vector2(Spec.Loot.Title.Position.X, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        header.AddChild(indent); header.MoveChild(indent, 0);
        var art = Plugin.Kit.Layout("{nation}_notice_us").Find("btn_quit")!;
        ClassicChatControls.SkinButton(close, art);
        close.Text = ""; close.TooltipText = "Close"; close.FocusMode = Control.FocusModeEnum.None;
        close.CustomMinimumSize = Spec.Loot.Close.Size; close.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        close.SetMeta(ExpectedRect, Spec.Loot.Close);
        var gap = new Control { Name = "loot_end", CustomMinimumSize = new Vector2(Spec.Loot.Size.X - Spec.Loot.Close.End.X, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        header.AddChild(gap);
        native.ResetSize();
        Keep(native, () => { if (native.Size != Spec.Loot.Size) native.Size = Spec.Loot.Size; });
    }
}

/// <summary>The original common message box art at its 327-pixel width; the middle band takes any extra height.</summary>
public partial class ClassicMessageArt : Control
{
    public ClassicMessageArt() { MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.Nearest; Resized += QueueRedraw; }

    public override void _Draw()
    {
        var root = Plugin.Kit.Layout("{nation}_messagebox_us");
        float extra = Math.Max(0, Size.Y - root.H), band = ClassicOverlayLayout.Message.MiddleBand;
        foreach (var image in root.Images)
        {
            if (image.Texture == null) continue;
            var rect = new Rect2(image.Position, image.SizeVec);
            if (image.Y == band) rect.Size += new Vector2(0, extra);
            else if (image.Y > band) rect.Position += new Vector2(0, extra);
            DrawTextureRectRegion(Plugin.Kit.Texture(image.Texture)!, rect, new Rect2(image.SrcX, image.SrcY, image.SrcW, image.SrcH));
        }
    }
}

/// <summary>A message box host: blocks the world under the art and, when free-standing, stays centred.</summary>
public partial class ClassicMessagePanel : Control
{
    private readonly bool _centre;
    public ClassicMessagePanel(Vector2 size, bool centre)
    {
        _centre = centre; MouseFilter = MouseFilterEnum.Stop;
        Size = CustomMinimumSize = size;
        AddChild(new ClassicMessageArt { Name = "message_art", Size = size });
    }

    public override void _Process(double delta)
    {
        if (!_centre || !IsVisibleInTree() || GetParent() is not Control parent) return;
        Position = ((parent.Size - Size) / 2).Round();
    }
}

/// <summary>Shows a native button's own text on the original art with the art's caption ink.</summary>
public partial class ClassicButtonCaption : Label
{
    private readonly Button _button;
    private readonly Color _ink, _disabled;

    public ClassicButtonCaption() { _button = null!; }

    public ClassicButtonCaption(Button button, int size, bool bold, Color ink, Color shadow, Color disabled)
    {
        _button = button; _ink = ink; _disabled = disabled;
        MouseFilter = MouseFilterEnum.Ignore; ClipText = true;
        HorizontalAlignment = HorizontalAlignment.Center; VerticalAlignment = VerticalAlignment.Center;
        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ClassicWorldOverlays.Text(this, size, ink, bold, shadow);
        Sync();
    }

    public override void _Process(double delta) => Sync();

    private void Sync()
    {
        if (_button == null || !GodotObject.IsInstanceValid(_button)) return;
        if (Text != _button.Text) Text = _button.Text;
        var ink = _button.Disabled ? _disabled : _ink;
        if (GetThemeColor("font_color") != ink) AddThemeColorOverride("font_color", ink);
    }
}

/// <summary>The nation information plaque at any size: original caps, stretched slate or stone body.</summary>
public static class ClassicPlaqueArt
{
    private const int MainWidth = 81, CapSource = 96, Height = 21;

    public static void Draw(CanvasItem item, Rect2 rect)
    {
        var (main, cap) = Boxes();
        float capWidth = ClassicOverlayLayout.Plaque.CapRight;
        item.DrawStyleBox(main, new Rect2(rect.Position, new Vector2(Math.Max(0, rect.Size.X - capWidth), rect.Size.Y)));
        item.DrawStyleBox(cap, new Rect2(rect.Position + new Vector2(rect.Size.X - capWidth, 0), new Vector2(capWidth, rect.Size.Y)));
    }

    private static (StyleBoxTexture, StyleBoxTexture) Boxes()
    {
        var art = Plugin.Kit.Layout("{nation}_msgoutput_small_us").Images.First();
        var texture = Plugin.Kit.Texture(art.Texture!);
        StyleBoxTexture Box(Rect2 region, int left, int right)
        {
            var box = new StyleBoxTexture { Texture = texture, RegionRect = region };
            box.SetTextureMargin(Side.Left, left); box.SetTextureMargin(Side.Right, right);
            box.SetTextureMargin(Side.Top, ClassicOverlayLayout.Plaque.CapTop); box.SetTextureMargin(Side.Bottom, ClassicOverlayLayout.Plaque.CapBottom);
            box.SetContentMarginAll(0);
            return box;
        }
        // The original plaque ends in a button well; the body joins the outer right cap directly.
        return (Box(new Rect2(art.SrcX, art.SrcY, MainWidth, Height), ClassicOverlayLayout.Plaque.CapLeft, 0),
            Box(new Rect2(art.SrcX + CapSource, art.SrcY, ClassicOverlayLayout.Plaque.CapRight, Height), 0, 0));
    }
}

/// <summary>
/// The original dropped-item grid under an information plaque. The client's loot rows stay hidden in
/// their list; each cell shows a row's icon and count and takes it through the row's own click.
/// </summary>
public partial class ClassicLootPanel : Control
{
    private readonly Container _rows;
    private readonly Control[] _cells = new Control[ClassicOverlayLayout.Loot.Cells.Length];
    private readonly Label[] _counts = new Label[ClassicOverlayLayout.Loot.Cells.Length];
    private readonly TextureRect[] _icons = new TextureRect[ClassicOverlayLayout.Loot.Cells.Length];
    private readonly Control?[] _bound = new Control?[ClassicOverlayLayout.Loot.Cells.Length];

    public ClassicLootPanel() { _rows = null!; }

    public ClassicLootPanel(Container rows)
    {
        _rows = rows; MouseFilter = MouseFilterEnum.Stop; TextureFilter = TextureFilterEnum.Nearest;
        Size = CustomMinimumSize = ClassicOverlayLayout.Loot.Size;
        for (int i = 0; i < _cells.Length; i++)
        {
            var rect = ClassicOverlayLayout.Loot.Cells[i];
            rect.Position += ClassicOverlayLayout.Loot.Grid;
            var cell = new Control { Name = $"loot_cell_{i}", Position = rect.Position, Size = rect.Size, MouseFilter = MouseFilterEnum.Stop,
                MouseDefaultCursorShape = CursorShape.PointingHand };
            cell.SetMeta(ClassicWorldOverlays.ExpectedRect, rect);
            var icon = new TextureRect { Name = "icon", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
            icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            cell.AddChild(icon);
            var count = new Label { Name = "count" };
            cell.AddChild(count);
            ItemCountStyle.Apply(count, cell, Plugin.Kit.Bold);
            int slot = i;
            cell.GuiInput += ev =>
            {
                if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } || _bound[slot] is not { } row) return;
                cell.AcceptEvent();
                NativeOverlays.TakeLoot(row);
            };
            AddChild(cell);
            (_cells[i], _icons[i], _counts[i]) = (cell, icon, count);
        }
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        var rows = _rows.GetChildren().OfType<Control>().Where(r => !r.IsQueuedForDeletion()).ToArray();
        for (int i = 0; i < _cells.Length; i++)
        {
            var row = i < rows.Length ? rows[i] : null;
            _bound[i] = row;
            var label = row == null ? null : ClassicWorldOverlays.Descendants(row).OfType<Label>().FirstOrDefault();
            var native = row == null ? null : ClassicWorldOverlays.Descendants(row).OfType<TextureRect>().FirstOrDefault();
            string text = label?.Text ?? "";
            bool gold = row != null && native?.Texture == null && text.StartsWith("Gold", StringComparison.Ordinal);
            _icons[i].Texture = row == null ? null : gold ? ClassicEventPanel.GoldIcon : native?.Texture;
            int mark = text.LastIndexOf("  x", StringComparison.Ordinal);
            _counts[i].Text = mark >= 0 ? text[(mark + 3)..] : "";
            _cells[i].TooltipText = row == null ? "" : text + "\nClick to take it.";
            _cells[i].MouseDefaultCursorShape = row == null ? CursorShape.Arrow : CursorShape.PointingHand;
        }
    }

    public override void _Draw()
    {
        ClassicPlaqueArt.Draw(this, ClassicOverlayLayout.Loot.Header);
        var grid = Plugin.Kit.Layout("co_droppeditem_us").Images.First();
        DrawTextureRectRegion(Plugin.Kit.Texture(grid.Texture!)!, new Rect2(ClassicOverlayLayout.Loot.Grid + grid.Position, grid.SizeVec),
            new Rect2(grid.SrcX, grid.SrcY, grid.SrcW, grid.SrcH));
    }
}
