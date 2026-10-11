using Godot;

namespace KnightOnlineUiClassic;

/// <summary>
/// One look for both nations inside the plain dark Classic frame. The frame, its black ground and its gold
/// line are the same art for El Morad and Karus, so the windows built on it share the gold inks, the field
/// line, the selection band and the scroll bar instead of switching to the Karus light-art palette.
/// </summary>
public static class ClassicDarkFrame
{
    public static Color Heading => ClassicDesign.FrameTitle;
    public static Color Caption => new("d9be8c");
    public static Color Value => new("efd9b4");
    public static Color Line => new("8e7545");
    public static Color SelectedRow => new("153a54");

    /// <summary>Black field with the gold frame line (edit fields, lists, menus).</summary>
    public static StyleBoxFlat InputBox()
    {
        var box = ClassicDesign.InputBox();
        box.BorderColor = Line;
        return box;
    }

    /// <summary>The thin gold section line of the El Morad information art, the dark frame's own gold.</summary>
    public static StyleBoxTexture SectionBox(int padding = 8)
    {
        var box = new StyleBoxTexture { Texture = Plugin.Kit.Texture("ui_el_myinfo_us.png"), RegionRect = new Rect2(7, 80, 281, 34), DrawCenter = false };
        foreach (var side in new[] { Side.Left, Side.Top, Side.Right, Side.Bottom })
        {
            box.SetTextureMargin(side, 1);
            box.SetContentMargin(side, padding);
        }
        return box;
    }

    /// <summary>
    /// The scroll bar of the dark-frame windows: a dark track with a thin gold line and the service button art
    /// (<c>co_change_bill_us</c> OK, the same art for both nations) as grabber, lit on hover and pressed.
    /// </summary>
    public static void ScrollBar(ScrollBar bar, float thickness = 12)
    {
        var track = new StyleBoxFlat { BgColor = new Color("171715"), BorderColor = new Color("bfa777") };
        track.SetBorderWidthAll(1); track.SetContentMarginAll(0);
        bar.AddThemeStyleboxOverride("scroll", track);
        bar.AddThemeStyleboxOverride("scroll_focus", track);
        var grabber = Plugin.Kit.Layout("co_change_bill_us").Find("btn_ok")!;
        foreach (var state in new[] { "grabber", "grabber_highlight", "grabber_pressed" })
        {
            int tag = state == "grabber_pressed" ? 1 : state == "grabber_highlight" ? 2 : 0;
            var part = grabber.Images.FirstOrDefault(n => n.Tag == tag) ?? grabber.Images.First();
            var box = new StyleBoxTexture { Texture = Plugin.Kit.Texture(part.Texture!), RegionRect = new Rect2(part.SrcX, part.SrcY, part.SrcW, part.SrcH) };
            box.SetTextureMarginAll(3); box.SetContentMarginAll(0);
            bar.AddThemeStyleboxOverride(state, box);
        }
        bar.FocusMode = Control.FocusModeEnum.None;
        if (bar is VScrollBar) bar.CustomMinimumSize = new Vector2(thickness, bar.CustomMinimumSize.Y);
        else bar.CustomMinimumSize = new Vector2(bar.CustomMinimumSize.X, thickness);
    }

    /// <summary>Both scroll bars of a scroll container in the dark-frame look.</summary>
    public static void Scroll(ScrollContainer scroll, float thickness = 12)
    {
        ScrollBar(scroll.GetVScrollBar(), thickness);
        ScrollBar(scroll.GetHScrollBar(), thickness);
    }
}
