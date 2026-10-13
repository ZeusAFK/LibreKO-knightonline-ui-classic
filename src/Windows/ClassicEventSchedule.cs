using Godot;
using LibreKO;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Native;
using Family = KnightOnlineUiClassic.Layout.ClassicEventFamilyLayout;
using Spec = KnightOnlineUiClassic.Layout.ClassicEventFamilyLayout.Schedule;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The Classic composition of the client's event schedule (window id <c>events</c>), on top of the event family's
/// plain frame. The client keeps building, filtering, ticking and rebuilding its cards; this restyles them as they
/// arrive: each card on the quest inset (lit while the event is live), the event glyph on the nation inventory cell,
/// name, level band, description and live status in the event inks, time chips and weekday marks as small insets,
/// Join or Open as the service button. The Events and Collection Races tabs take the chat tab art and "Only my
/// level" the chat check socket. The status ink the client rewrites every second is mapped to the Classic ink, and
/// the list takes the height of its cards, so the loading and empty texts are a short box.
/// </summary>
public static class ClassicEventSchedule
{
    public const string CardMeta = "classic_event_card", LiveMeta = "classic_event_live", ActiveMeta = "classic_event_active";
    private const string CardsMeta = "classic_event_cards", StatusMeta = "classic_event_status", InkMeta = "classic_event_ink";

    /// <summary>The schedule's controls, found by the shape the client builds.</summary>
    private sealed record Parts(HBoxContainer Filters, Button[] Tabs, Button? MyLevel, ScrollContainer Scroll, MarginContainer Margin,
        VBoxContainer Cards, Label? Empty);

    /// <summary>
    /// Restyles the filters and the cards already built, and keeps the status ink, the check and the list height.
    /// Returns false (the family's generic restyle then covers the window) when the window lacks the expected shape.
    /// </summary>
    public static bool Apply(HudWindow window)
    {
        if (Find(window) is not { } parts) return false;
        parts.Filters.AddThemeConstantOverride("separation", Spec.FilterGap);
        foreach (var tab in parts.Tabs) Tab(tab);
        if (parts.MyLevel is { } toggle) Toggle(toggle);
        parts.Cards.SetMeta(CardsMeta, true);
        foreach (var card in parts.Cards.GetChildren().OfType<PanelContainer>()) StyleCard(card);
        if (parts.Empty is { } empty)
        {
            empty.CustomMinimumSize = new Vector2(0, Spec.EmptyHeight);
            empty.VerticalAlignment = VerticalAlignment.Center;
        }
        window.AddChild(new NativeEventWatcher(() => { if (window.IsVisibleInTree()) Keep(window, parts); })
            { Name = "classic_schedule_sync", ProcessPriority = int.MaxValue - 1 });
        Keep(window, parts);
        return true;
    }

    /// <summary>Restyles a card the client added to the list after <see cref="Apply"/> (every refresh rebuilds them).</summary>
    public static void Arrived(Node node)
    {
        if (node is PanelContainer card && card.GetParent() is VBoxContainer cards && cards.HasMeta(CardsMeta)) StyleCard(card);
    }

    private static Parts? Find(HudWindow window)
    {
        if (window.Body.GetChildren().OfType<VBoxContainer>().FirstOrDefault() is not { } body) return null;
        var filters = body.GetChildren().OfType<HBoxContainer>().FirstOrDefault(row => row.GetChildren().OfType<Button>().Count(b => b.ToggleMode) >= 2);
        var scroll = body.GetChildren().OfType<ScrollContainer>().FirstOrDefault();
        if (filters == null || scroll?.GetChildren().OfType<MarginContainer>().FirstOrDefault() is not { } margin
            || margin.GetChildren().OfType<VBoxContainer>().FirstOrDefault() is not { } list
            || list.GetChildren().OfType<VBoxContainer>().FirstOrDefault() is not { } cards) return null;
        var toggles = filters.GetChildren().OfType<Button>().Where(b => b.ToggleMode).ToArray();
        var tabs = toggles.Where(b => b.ButtonGroup != null).ToArray();
        if (tabs.Length < 2) return null;
        return new Parts(filters, tabs, toggles.FirstOrDefault(b => b.ButtonGroup == null), scroll, margin, cards,
            list.GetChildren().OfType<Label>().FirstOrDefault());
    }

    // ---- Filters --------------------------------------------------------------------------------------

    /// <summary>An Events / Collection Races tab on the original chat tab art; the chosen tab keeps the pressed art.</summary>
    private static void Tab(Button tab)
    {
        ClassicChatControls.SkinButton(tab, Plugin.Kit.Layout("{nation}_chat_us").Find("btn_normal")!);
        tab.AddThemeStyleboxOverride("hover_pressed", tab.GetThemeStylebox("pressed"));
        ClassicEventPanel.Bold(tab, null, Family.SmallFont);
        var ink = ClassicWorldOverlays.TabInk;
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            tab.AddThemeColorOverride(state, ink);
        // Karus tabs are light stone: black ink without the shadow, as on the Classic chat and war board tabs.
        if (ClassicDesign.Karus) tab.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        float caption = Plugin.Kit.Bold.GetStringSize(tab.Text, HorizontalAlignment.Left, -1, Family.SmallFont).X;
        tab.CustomMinimumSize = new Vector2(Math.Max(Spec.TabMinWidth, Mathf.Ceil(caption) + 2 * Spec.TabPadding), Spec.TabHeight);
        tab.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        tab.FocusMode = Control.FocusModeEnum.None;
        HideUnderline(tab);
        tab.SetMeta(ClassicEventFamily.Skinned, true);
    }

    /// <summary>"Only my level": the original chat check socket before the caption, in the family's check ink.</summary>
    private static void Toggle(Button toggle)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
            toggle.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        ClassicEventPanel.Bold(toggle, null, Family.SmallFont);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            toggle.AddThemeColorOverride(state, ClassicDarkFrame.Value);
        toggle.AddThemeColorOverride("font_disabled_color", ClassicDesign.Muted);
        foreach (var state in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color", "icon_focus_color" })
            toggle.AddThemeColorOverride(state, Colors.White);
        toggle.AddThemeConstantOverride("h_separation", Spec.ToggleGap);
        toggle.Alignment = HorizontalAlignment.Left;
        toggle.CustomMinimumSize = new Vector2(0, Spec.TabHeight);
        toggle.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        toggle.FocusMode = Control.FocusModeEnum.None;
        var socket = Plugin.Kit.Layout("{nation}_chat_us").Find("btn_check_normal")!;
        Texture2D Art(int tag)
        {
            var image = socket.Images.First(i => i.Tag == tag);
            return new AtlasTexture { Atlas = Plugin.Kit.Texture(image.Texture!), Region = new Rect2(image.SrcX, image.SrcY, image.SrcW, image.SrcH), FilterClip = true };
        }
        toggle.SetMeta(CheckMeta, new Godot.Collections.Array { Art(0), Art(1) });
        SyncCheck(toggle);
        HideUnderline(toggle);
        toggle.SetMeta(ClassicEventFamily.Skinned, true);
    }

    private const string CheckMeta = "classic_event_check";

    private static void SyncCheck(Button toggle)
    {
        if (toggle.GetMeta(CheckMeta, new Godot.Collections.Array()).AsGodotArray() is not { Count: 2 } art) return;
        var wanted = art[toggle.ButtonPressed ? 1 : 0].As<Texture2D>();
        if (toggle.Icon != wanted) toggle.Icon = wanted;
    }

    /// <summary>The client marks the chosen tab with a gold underline; the Classic tab art already shows it.</summary>
    private static void HideUnderline(Button button)
    {
        foreach (var line in button.GetChildren().OfType<ColorRect>()) line.Color = Colors.Transparent;
    }

    // ---- Cards ----------------------------------------------------------------------------------------

    private static bool Near(Color a, Color b) => Math.Abs(a.R - b.R) < .02f && Math.Abs(a.G - b.G) < .02f && Math.Abs(a.B - b.B) < .02f;

    private static StyleBoxTexture Inset(int x, int y, bool lit = false)
    {
        var box = ClassicNpcLayout.InsetBox();
        box.ContentMarginLeft = box.ContentMarginRight = x;
        box.ContentMarginTop = box.ContentMarginBottom = y;
        if (lit) box.ModulateColor = new Color(Spec.LiveLight, Spec.LiveLight, Spec.LiveLight);
        return box;
    }

    private static void Text(Label label, Color ink, int size)
    {
        ClassicEventPanel.Bold(label, ink, size);
        label.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        label.SetMeta(ClassicEventFamily.Skinned, true);
    }

    private static void Done(Control control) => control.SetMeta(ClassicEventFamily.Skinned, true);

    /// <summary>
    /// One card: the client's column (top row, time chips, weekday marks) keeps its order and spacing. Parts that
    /// are not in the expected place are left to the family's generic restyle.
    /// </summary>
    private static void StyleCard(PanelContainer card)
    {
        if (card.HasMeta(CardMeta)) return;
        card.SetMeta(CardMeta, true);
        // The client marks a live event with a gold border on its card.
        bool live = card.GetThemeStylebox("panel") is StyleBoxFlat native && native.BorderColor.A > .7f && Near(native.BorderColor, UiTheme.Gold);
        card.SetMeta(LiveMeta, live);
        card.AddThemeStyleboxOverride("panel", Inset(Spec.CardPadding, Spec.CardPadding, live));
        Done(card);
        if (card.GetChildren().OfType<VBoxContainer>().FirstOrDefault() is not { } column) return;
        Done(column);
        var rows = column.GetChildren().OfType<HBoxContainer>().ToArray();
        if (rows.Length > 0) Top(rows[0], live);
        foreach (var chips in column.GetChildren().OfType<HFlowContainer>())
        {
            Done(chips);
            foreach (var chip in chips.GetChildren().OfType<PanelContainer>()) Chip(chip);
        }
        if (rows.Length > 1)
        {
            Done(rows[1]);
            foreach (var day in rows[1].GetChildren().OfType<PanelContainer>()) Day(day);
        }
    }

    /// <summary>Glyph cell, name with level band and description, and the status with Join or Open.</summary>
    private static void Top(HBoxContainer top, bool live)
    {
        Done(top);
        var parts = top.GetChildren().OfType<Control>().ToArray();
        if (parts.FirstOrDefault() is PanelContainer tile)
        {
            tile.AddThemeStyleboxOverride("panel", ClassicProgressionPanel.CellBox());
            Done(tile);
            foreach (var glyph in ClassicWorldOverlays.Descendants(tile).OfType<TextureRect>())
            {
                glyph.SelfModulate = live ? ClassicEventFamily.Highlight : ClassicEventPanel.Caption;
                Done(glyph);
            }
        }
        var columns = parts.OfType<VBoxContainer>().ToArray();
        if (columns.Length > 0 && columns[0].GetChildren().OfType<HBoxContainer>().FirstOrDefault() is { } nameRow)
        {
            Done(columns[0]); Done(nameRow);
            if (nameRow.GetChildren().OfType<Label>().FirstOrDefault() is { } name)
            {
                bool fits = !Near(name.GetThemeColor("font_color"), UiTheme.TextLo);
                Text(name, !fits ? ClassicEventPanel.Muted : live ? ClassicEventFamily.Highlight : ClassicEventPanel.Ink, Spec.NameFont);
            }
            if (nameRow.GetChildren().OfType<PanelContainer>().FirstOrDefault() is { } band)
            {
                // The level band reads as a caption beside the name, red when the character is outside it.
                band.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
                Done(band);
                foreach (var label in band.GetChildren().OfType<Label>())
                    Text(label, Near(label.GetThemeColor("font_color"), UiTheme.Bad) ? ClassicEventPanel.Refused : ClassicEventPanel.Caption, Spec.DetailFont);
            }
            foreach (var description in columns[0].GetChildren().OfType<Label>()) Text(description, ClassicEventPanel.Muted, Spec.DetailFont);
        }
        if (columns.Length > 1)
        {
            var side = columns[1];
            Done(side);
            if (side.GetChildren().OfType<Label>().FirstOrDefault() is { } status)
            {
                var ink = ClassicEventFamily.Ink(status.GetThemeColor("font_color"));
                Text(status, ink, Spec.StatusFont);
                status.HorizontalAlignment = HorizontalAlignment.Right;
                status.SetMeta(InkMeta, ink);
                top.GetParent()?.GetParent()?.SetMeta(StatusMeta, status);
            }
            foreach (var action in side.GetChildren().OfType<Button>())
            {
                ClassicEventFamily.Action(action);
                action.CustomMinimumSize = new Vector2(action.CustomMinimumSize.X, Family.Button);
                Done(action);
            }
        }
    }

    /// <summary>A start time: a small inset with the clock glyph muted and the local time in the light ink.</summary>
    private static void Chip(PanelContainer chip)
    {
        chip.AddThemeStyleboxOverride("panel", Inset(Spec.ChipPadX, Spec.ChipPadY));
        Done(chip);
        foreach (var node in ClassicWorldOverlays.Descendants(chip).Skip(1))
            switch (node)
            {
                case TextureRect glyph: glyph.SelfModulate = ClassicEventPanel.Muted; Done(glyph); break;
                case Label label: Text(label, ClassicEventPanel.Ink, Spec.ChipFont); break;
                case Control control: Done(control); break;
            }
    }

    /// <summary>A weekday: a small inset, lit with the highlight ink on the days the event runs.</summary>
    private static void Day(PanelContainer day)
    {
        var label = day.GetChildren().OfType<Label>().FirstOrDefault();
        bool active = label != null && Near(label.GetThemeColor("font_color"), UiTheme.GoldBright);
        day.SetMeta(ActiveMeta, active);
        day.AddThemeStyleboxOverride("panel", Inset(Spec.DayPadX, Spec.DayPadY, active));
        Done(day);
        if (label != null) Text(label, active ? ClassicEventFamily.Highlight : ClassicEventPanel.Muted, Spec.DayFont);
    }

    // ---- Every frame --------------------------------------------------------------------------------------

    private static void Keep(HudWindow window, Parts parts)
    {
        if (!GodotObject.IsInstanceValid(parts.Scroll)) return;
        if (parts.MyLevel is { } toggle && GodotObject.IsInstanceValid(toggle)) SyncCheck(toggle);
        foreach (var card in parts.Cards.GetChildren().OfType<PanelContainer>())
        {
            // The client recolours the status every second (sign-up green, running amber, upcoming gold).
            if (!card.HasMeta(StatusMeta) || card.GetMeta(StatusMeta).AsGodotObject() is not Label status || !GodotObject.IsInstanceValid(status)) continue;
            var shown = status.GetThemeColor("font_color");
            if (shown == status.GetMeta(InkMeta, shown).AsColor()) continue;
            var ink = ClassicEventFamily.Ink(shown);
            status.AddThemeColorOverride("font_color", ink);
            status.SetMeta(InkMeta, ink);
        }
        // The list takes the height of its cards, at most the client's; the loading and empty texts keep two rows.
        float content = parts.Margin.GetCombinedMinimumSize().Y;
        float height = Mathf.Clamp(content, Spec.EmptyHeight, Spec.MaxListHeight);
        // The scroll bar is drawn over the list's right edge; the cards end the bar gap before it.
        int right = content > Spec.MaxListHeight ? (int)parts.Scroll.GetVScrollBar().GetCombinedMinimumSize().X + Spec.BarGap : 0;
        if (parts.Margin.GetThemeConstant("margin_right") != right) parts.Margin.AddThemeConstantOverride("margin_right", right);
        if (parts.Scroll.CustomMinimumSize.Y != height)
        {
            parts.Scroll.CustomMinimumSize = new Vector2(parts.Scroll.CustomMinimumSize.X, height);
            window.ResetSize();
        }
    }
}
