using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Theme item names as cached <see cref="StringName"/>s, and override setters that skip an unchanged value.
/// A string literal passed to a theme or meta API creates a finalizable <c>StringName</c> per call, and an override
/// applied again re-shapes the control's text and re-sorts its container, so per-frame code uses these instead.
/// </summary>
public static class ThemeNames
{
    public static readonly StringName Panel = "panel";
    public static readonly StringName Normal = "normal";
    public static readonly StringName Hover = "hover";
    public static readonly StringName Pressed = "pressed";
    public static readonly StringName HoverPressed = "hover_pressed";
    public static readonly StringName Disabled = "disabled";
    public static readonly StringName Focus = "focus";
    public static readonly StringName FontColor = "font_color";
    public static readonly StringName FontHoverColor = "font_hover_color";
    public static readonly StringName FontPressedColor = "font_pressed_color";
    public static readonly StringName FontDisabledColor = "font_disabled_color";
    public static readonly StringName FontShadowColor = "font_shadow_color";
    public static readonly StringName MarginRight = "margin_right";

    /// <summary>Overrides a colour unless the control already carries that override with that value.</summary>
    public static void SetColor(Control control, StringName name, Color color)
    {
        if (control.HasThemeColorOverride(name) && control.GetThemeColor(name) == color) return;
        control.AddThemeColorOverride(name, color);
    }

    /// <summary>Overrides a constant unless the control already carries that override with that value.</summary>
    public static void SetConstant(Control control, StringName name, int value)
    {
        if (control.HasThemeConstantOverride(name) && control.GetThemeConstant(name) == value) return;
        control.AddThemeConstantOverride(name, value);
    }

    /// <summary>Overrides a style box unless the control already resolves to that very box.</summary>
    public static void SetStylebox(Control control, StringName name, StyleBox box)
    {
        if (control.GetThemeStylebox(name) == box) return;
        control.AddThemeStyleboxOverride(name, box);
    }
}
