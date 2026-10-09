using Godot;

namespace KnightOnlineUiClassic.Windows;

// Shared states for original action buttons in the appearance, rebirth and Familiar services.
internal static class ClassicServiceAction
{
    // A disabled original button dims its artwork and mutes its caption, so it never reads as available.
    internal static void Disabled(Button button, Color? ink = null)
    {
        if (button.GetThemeStylebox("normal") is StyleBoxTexture normal)
        {
            var dim = (StyleBoxTexture)normal.Duplicate(); dim.ModulateColor = new Color(.55f, .55f, .55f);
            button.AddThemeStyleboxOverride("disabled", dim);
        }
        button.AddThemeColorOverride("font_disabled_color", ink ?? new Color("8b8175"));
    }
}
