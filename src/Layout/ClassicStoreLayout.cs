using Godot;
using LibreKO;
namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Power-Up Store geometry. The composition follows the original in-game Power Up Store screen: a title bar with
/// the store's CLOSE plate (co_shoppingmall_us), the search row with the cash display above the sidebar, the
/// category tabs over the item well, and the cart as the right sidebar. The window uses the plain straight
/// Classic frame (ui_message_us), without the ornamental header strip.
/// </summary>
public static class ClassicStoreLayout
{
    public const int Inset = 16;
    public const int TitleTop = 10, TitleHeight = 22;
    public const int ToolsTop = 42;
    /// <summary>The native search field and sort list; touch screens keep the 40-pixel hit target.</summary>
    public static int ToolsHeight => Platform.TouchUi ? 40 : 32;
    public static int BodyTop => ToolsTop + ToolsHeight + 10;
    public const int TabHeight = 30, TouchTabHeight = 40, TabGap = 2, TabPadding = 8, TabMinWidth = 72, TabMaxWidth = 220, TabIcon = 14, TabFont = 12;
    public const int WellMargin = 8;
    /// <summary>The item details description: five whole 18-pixel lines of Arial Bold 13 before it scrolls.</summary>
    public const int DescriptionHeight = 90;
    public static Vector2 Chrome => new(2 * Inset, BodyTop + Inset);
    /// <summary>The plain notice frame in ui_message_us (as Classic service notices draw it): a three-pixel gold line.</summary>
    public static readonly Rect2 FrameArt = new(330, 2, 129, 129);
    public const int FrameCorner = 8;
    public static readonly Vector2 ClosePlate = new(125, 22);
    public static Rect2 Close(float width) => new(width - Inset - ClosePlate.X, TitleTop, ClosePlate.X, ClosePlate.Y);
    /// <summary>The centred window title, kept clear of the CLOSE plate on both sides.</summary>
    public static Rect2 Title(float width) => new(Inset + ClosePlate.X + 8, TitleTop, width - 2 * (Inset + ClosePlate.X + 8), TitleHeight);
    public static Rect2 Drag(float width) => new(4, 4, width - Inset - ClosePlate.X - 12, ToolsTop - 8);
    public static Vector2 Tools => new(Inset, ToolsTop);
    /// <summary>The cash display sits at the top of the sidebar, right-aligned with the cart column.</summary>
    public static Rect2 Wallet(float width, Vector2 size) => new(new Vector2(width - Inset - size.X, ToolsTop + (ToolsHeight - size.Y) / 2).Round(), size);
    public static Rect2 Body(Vector2 size) => new(new Vector2(Inset, BodyTop), size - Chrome);
    /// <summary>Tab captions: strong ink on the open tab, a quieter ink on the others (light ink on Human art, dark on Karus).</summary>
    public static Color TabInk(bool selected) => new(ClassicDesign.Karus ? selected ? "0c0a08" : "403b35" : selected ? "fff1c0" : "b9a98a");
    public static int TabWidth(float caption, bool icon) => (int)Mathf.Clamp(Mathf.Ceil(caption) + 2 * TabPadding + (icon ? TabIcon + 4 : 0), TabMinWidth, TabMaxWidth);

    /// <summary>
    /// Places the category tabs: one left-aligned row of natural plates when they fit (as the original tab strip),
    /// otherwise the fewest rows with balanced counts, each row justified to the full well width.
    /// </summary>
    public static (Rect2[] Rects, float Height) TabRects(IReadOnlyList<float> widths, float room, float height)
    {
        int n = widths.Count; var counts = new List<int>();
        for (int rows = 1; rows <= n && counts.Count == 0; rows++)
        {
            var split = Enumerable.Range(0, rows).Select(r => n / rows + (r < n % rows ? 1 : 0)).ToList();
            int start = 0; bool fits = true;
            foreach (int count in split) { fits &= widths.Skip(start).Take(count).Sum() + TabGap * (count - 1) <= room; start += count; }
            if (fits) counts = split;
        }
        if (counts.Count == 0) counts = Enumerable.Repeat(1, n).ToList();
        var rects = new Rect2[n]; int at = 0;
        for (int r = 0; r < counts.Count; r++)
        {
            var row = Enumerable.Range(at, counts[r]).ToArray(); at += counts[r];
            float natural = row.Sum(i => widths[i]), gaps = TabGap * (row.Length - 1);
            // A single tab wider than the well shrinks to it (its caption keeps the ellipsis and tooltip).
            float scale = natural + gaps > room ? Math.Max(0, room - gaps) / natural : 1;
            float extra = counts.Count > 1 && scale == 1 ? (room - natural - gaps) / row.Length : 0;
            float x = 0;
            foreach (int i in row) { float w = Mathf.Floor(widths[i] * scale + extra); rects[i] = new Rect2(x, r * (height + TabGap), w, height); x += w + TabGap; }
            if (extra > 0) rects[row[^1]].Size = new Vector2(room - rects[row[^1]].Position.X, height);
        }
        return (rects, counts.Count == 0 ? 0 : counts.Count * height + (counts.Count - 1) * TabGap);
    }
    public static StyleBoxFlat Box(int margin = 8, bool active = false)
    {
        var box = new StyleBoxFlat { BgColor = active ? new Color("211b11") : Colors.Black, BorderColor = new Color(active ? "cfb575" : "7e6b48") };
        box.SetBorderWidthAll(1); box.SetContentMarginAll(margin); return box;
    }
    /// <summary>
    /// The frame's corner and edge sources. The art holds four 64-pixel quadrants with a one-pixel gap between them,
    /// so the edges repeat a 56-pixel run from inside one quadrant.
    /// </summary>
    public static (Rect2 Source, Rect2 Target, bool Horizontal)[] FrameParts(Vector2 size)
    {
        float l = FrameArt.Position.X, t = FrameArt.Position.Y, r = FrameArt.End.X - FrameCorner, b = FrameArt.End.Y - FrameCorner;
        const int c = FrameCorner, run = 56;
        return new[]
        {
            (new Rect2(l, t, c, c), new Rect2(0, 0, c, c), true), (new Rect2(r, t, c, c), new Rect2(size.X - c, 0, c, c), true),
            (new Rect2(l, b, c, c), new Rect2(0, size.Y - c, c, c), true), (new Rect2(r, b, c, c), new Rect2(size.X - c, size.Y - c, c, c), true),
            (new Rect2(l + c, t, run, c), new Rect2(c, 0, size.X - 2 * c, c), true), (new Rect2(l + c, b, run, c), new Rect2(c, size.Y - c, size.X - 2 * c, c), true),
            (new Rect2(l, t + c, c, run), new Rect2(0, c, c, size.Y - 2 * c), false), (new Rect2(r, t + c, c, run), new Rect2(size.X - c, c, c, size.Y - 2 * c), false),
        };
    }
    public static StyleBoxTexture IconBox() => new()
    {
        Texture = Plugin.Kit.Texture("ui_message_us.png"), RegionRect = new Rect2(15, 193, 50, 50),
        TextureMarginLeft = 1, TextureMarginRight = 1, TextureMarginTop = 1, TextureMarginBottom = 1,
        ContentMarginLeft = 3, ContentMarginRight = 3, ContentMarginTop = 3, ContentMarginBottom = 3, DrawCenter = false,
    };
}
