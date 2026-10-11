using Godot;
using LibreKO;
using KnightOnlineUiClassic.Native;
using Spec = KnightOnlineUiClassic.Layout.ClassicQuestHudLayout.Premium;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// The account and clan premium chips as Classic information plaques, like the event title chips beside them.
/// 1.298 showed premium only as a yellow line on the login screen (el_login_intro_us "Premium"); in the world the
/// status keeps the client's text ("Gold Premium: 2d 4h", "No Premium", "Clan Premium · 3d"): the plaque ink
/// while a premium runs, the muted plaque ink without one. The client's status dot gives way to the ink. The
/// benefits tooltip is the original tooltip (co_tooltip_us): translucent black behind white Arial Bold.
/// </summary>
public static class ClassicPremiumChip
{
    public const string Skinned = "classic_premium_chip";

    public static void Register()
    {
        NativeQuestHud.Extend(NativeQuestHudPart.PremiumChip, node => Apply((PanelContainer)node));
        NativeQuestHud.Extend(NativeQuestHudPart.ClanPremiumChip, node => Apply((PanelContainer)node));
    }

    public static void Apply(PanelContainer chip)
    {
        if (chip.HasMeta(Skinned)) return;
        chip.SetMeta(Skinned, true);
        chip.AddThemeStyleboxOverride("panel", new StyleBoxEmpty { ContentMarginLeft = Spec.Inset, ContentMarginRight = Spec.Inset, ContentMarginTop = Spec.PadY, ContentMarginBottom = Spec.PadY });
        chip.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        chip.Draw += () => ClassicPlaqueArt.Draw(chip, new Rect2(Vector2.Zero, chip.Size));
        chip.CustomMinimumSize = new Vector2(chip.CustomMinimumSize.X, Spec.Height);
        chip.Theme = Tooltip();
        foreach (var row in chip.GetChildren().OfType<HBoxContainer>()) row.AddThemeConstantOverride("separation", 0);
        // The status dot's meaning moves into the caption ink.
        foreach (var dot in Native.Native.Descendants(chip).OfType<Panel>())
            if (dot.GetParent() is CenterContainer wrap) wrap.Visible = false;
        var labels = Native.Native.Descendants(chip).OfType<Label>().ToArray();
        foreach (var label in labels)
        {
            ClassicEventPanel.Bold(label, null, Spec.FontSize);
            label.AddThemeConstantOverride("outline_size", 0);
            label.AddThemeColorOverride("font_shadow_color", ClassicWorldOverlays.PlaqueShadow);
            label.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            label.VerticalAlignment = VerticalAlignment.Center;
        }
        chip.AddChild(new ClassicPremiumInk(labels) { Name = "classic_premium_ink" });
        chip.QueueRedraw();
    }

    /// <summary>The original tooltip look for the chip's benefits tooltip (the tooltip popup inherits the chip's theme).</summary>
    internal static Theme Tooltip()
    {
        var theme = new Theme();
        var ground = new StyleBoxFlat { BgColor = Spec.TooltipGround };
        ground.ContentMarginLeft = ground.ContentMarginRight = Spec.TooltipPadX;
        ground.ContentMarginTop = ground.ContentMarginBottom = Spec.TooltipPadY;
        theme.SetStylebox("panel", "TooltipPanel", ground);
        theme.SetFont("font", "TooltipLabel", Plugin.Kit.Bold);
        theme.SetFontSize("font_size", "TooltipLabel", Spec.TooltipFont);
        theme.SetColor("font_color", "TooltipLabel", Colors.White);
        theme.SetColor("font_shadow_color", "TooltipLabel", Colors.Black);
        theme.SetConstant("shadow_offset_x", "TooltipLabel", 1);
        theme.SetConstant("shadow_offset_y", "TooltipLabel", 1);
        theme.SetConstant("outline_size", "TooltipLabel", 0);
        return theme;
    }
}

/// <summary>
/// Keeps the chip caption in the plaque ink: the client recolours it on every status change (gold while a
/// premium runs, grey without one); that state becomes the plaque ink or the muted plaque ink.
/// </summary>
public partial class ClassicPremiumInk : Node
{
    private readonly Label[] _labels;
    public ClassicPremiumInk() { _labels = Array.Empty<Label>(); }
    public ClassicPremiumInk(Label[] labels) { _labels = labels; ProcessPriority = int.MaxValue - 1; Sync(); }
    public override void _Process(double delta) => Sync();

    /// <summary>Whether the client shows a running premium (its gold caption).</summary>
    public static bool Active(Color native) => native.IsEqualApprox(UiTheme.GoldBright) || native.IsEqualApprox(UiTheme.Gold);

    private void Sync()
    {
        foreach (var label in _labels)
        {
            if (!GodotObject.IsInstanceValid(label)) continue;
            var shown = label.GetThemeColor("font_color");
            if (shown == ClassicWorldOverlays.PlaqueInk || shown == Layout.ClassicQuestHudLayout.PlaqueMuted) continue;
            label.SetMeta("premium_active", Active(shown));
            label.AddThemeColorOverride("font_color", Active(shown) ? ClassicWorldOverlays.PlaqueInk : Layout.ClassicQuestHudLayout.PlaqueMuted);
        }
    }
}
