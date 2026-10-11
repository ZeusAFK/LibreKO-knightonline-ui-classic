using Godot;
using KnightOnlineUiClassic.Layout;
using L = KnightOnlineUiClassic.Layout.ClassicMykoHudLayout;

namespace KnightOnlineUiClassic.Windows;

/// <summary>
/// Original HUD art read through <see cref="LocalAssets"/>: the nation's layouts, views on them and their texture
/// regions. Every piece checks <see cref="Layout"/> first and keeps the bundled Classic design when the art is absent.
/// </summary>
public static class ClassicMykoArt
{
    public static string NationTag => Plugin.Kit.Nation == 1 ? "ka" : "el";

    /// <summary>The installed local layout for the player's nation, or null (not installed or a texture missing).</summary>
    public static LayoutNode? Layout(string name) => Plugin.Kit.Local.Layout(name.Replace("{nation}", NationTag));

    /// <summary>A view of local art; one-line texts taller than their UIF rect stay centred on it.</summary>
    public static LayoutView View(LayoutNode root)
    {
        var view = new LayoutView(Plugin.Kit, root, Plugin.Kit.Local.Texture);
        foreach (var (node, control) in view.Where(n => n.IsString).ToList())
            if (control is Label { AutowrapMode: TextServer.AutowrapMode.Off } label)
            {
                view.CentreOnRect(label, node);
                label.Position = label.Position.Round();
            }
        return view;
    }

    public static Texture2D? Texture(LayoutNode image) => image.Texture == null ? null : Plugin.Kit.Local.Texture(image.Texture);

    public static Rect2 Source(LayoutNode image) => new(image.SrcX, image.SrcY, image.SrcW, image.SrcH);

    /// <summary>The image of a button for a state, falling back to the normal art.</summary>
    public static LayoutNode? State(LayoutNode button, int state) =>
        button.Images.FirstOrDefault(i => i.Tag == state) ?? button.Images.FirstOrDefault(i => i.Tag == ButtonState.Normal);

    public static int StateOf(BaseButton button, bool hovered) =>
        button.Disabled ? ButtonState.Disabled : button.IsPressed() || button.ButtonPressed ? ButtonState.Pressed
        : hovered ? ButtonState.Hover : ButtonState.Normal;

    /// <summary>The scaled art of a button state as a texture style box (UIF art is stretched to the control rect).</summary>
    public static StyleBoxTexture? Box(LayoutNode button, int state)
    {
        if (State(button, state) is not { } image || Texture(image) is not { } texture) return null;
        return new StyleBoxTexture { Texture = texture, RegionRect = Source(image), DrawCenter = true };
    }
}

/// <summary>
/// The status bar's original letter plates on the local menu art (<c>re_*_hpmenu</c>): Q, P, D, E and R use their
/// own plates of the sheet; A and G, which the sheet does not have, are drawn on the plate's letterless border and
/// fill with the sheet's white serif letter.
/// </summary>
public static class ClassicMykoPlates
{
    public static bool Available => Menu != null;

    private static LayoutNode? Menu => ClassicMykoArt.Layout(L.HpMenu);

    /// <summary>The sheet's letters are a white, regular-weight serif.</summary>
    private static readonly Font LetterFont = new SystemFont { FontNames = new[] { "Times New Roman" }, FontWeight = 400 };

    /// <summary>The plate art of a letter for a state; <c>Baked</c> says whether the letter is part of the art.</summary>
    public static (Texture2D Texture, Rect2 Source, bool Baked)? Plate(string letter, int state)
    {
        if (Menu is not { } menu || menu.Find("btn_pus") is not { } p) return null;
        var source = letter == "R" ? menu.Find("btn_dailyrank") : p;
        if (source == null || ClassicMykoArt.State(source, state) is not { } image || ClassicMykoArt.Texture(image) is not { } texture) return null;
        var rect = ClassicMykoArt.Source(image);
        if (letter == "R") return (texture, rect, true);
        bool baked = L.SheetRows.TryGetValue(letter, out int row);
        return (texture, new Rect2(rect.Position + new Vector2(0, row * L.PlatePitch), rect.Size), baked);
    }

    /// <summary>Draws the plate of <paramref name="letter"/> into <paramref name="target"/>; false without local art.</summary>
    public static bool Draw(CanvasItem canvas, Rect2 target, string letter, int state, Color tint)
    {
        if (Plate(letter, state) is not var (texture, source, baked)) return false;
        if (baked)
        {
            canvas.DrawTextureRectRegion(texture, target, source, tint);
            return true;
        }
        // Border and a clean fill column of the P plate, scaled like the art to the plate rect.
        float sx = target.Size.X / source.Size.X, sy = target.Size.Y / source.Size.Y;
        int b = L.PlateBorder;
        void Slice(Rect2 crop) => canvas.DrawTextureRectRegion(texture,
            new Rect2(target.Position + crop.Position * new Vector2(sx, sy), crop.Size * new Vector2(sx, sy)), new Rect2(source.Position + crop.Position, crop.Size), tint);
        var w = source.Size.X; var h = source.Size.Y;
        canvas.DrawTextureRectRegion(texture, new Rect2(target.Position + new Vector2(b * sx, b * sy), new Vector2((w - 2 * b) * sx, (h - 2 * b) * sy)),
            new Rect2(source.Position + new Vector2(L.PlateFillColumn, b), new Vector2(1, h - 2 * b)), tint);
        Slice(new Rect2(0, 0, w, b));
        Slice(new Rect2(0, h - b, w, b));
        Slice(new Rect2(0, b, b, h - 2 * b));
        Slice(new Rect2(w - b, b, b, h - 2 * b));
        var font = LetterFont;
        float width = font.GetStringSize(letter, fontSize: L.LetterFontSize).X;
        var pen = new Vector2(Mathf.Round(target.Position.X + (target.Size.X - width) / 2), Mathf.Round(target.Position.Y + L.LetterBaseline * sy));
        if (state == ButtonState.Pressed) pen += Vector2.One;
        canvas.DrawStringOutline(font, pen, letter, fontSize: L.LetterFontSize, size: 2, modulate: new Color(0, 0, 0, 0.55f));
        canvas.DrawString(font, pen, letter, fontSize: L.LetterFontSize, modulate: Colors.White * tint);
        return true;
    }
}

/// <summary>
/// The original alarm clock beside the letter row (<c>btn_scheduler</c>, with the original art). Like the original it opens
/// the event schedule: the client's Events window, whose request for the schedule goes to the server as from E.
/// </summary>
public partial class ClassicStatusClock : BaseButton
{
    public const string NodeName = "btn_scheduler", Title = "Event Schedule";
    private readonly LayoutNode _art;
    private bool _hovered;

    public ClassicStatusClock() { _art = null!; }

    public ClassicStatusClock(LayoutNode art, Vector2 size, Action press)
    {
        _art = art;
        Name = NodeName;
        Size = size; CustomMinimumSize = size;
        FocusMode = FocusModeEnum.None; MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TextureFilter = TextureFilterEnum.Nearest;
        TooltipText = Title;
        Pressed += press;
        MouseEntered += () => { _hovered = true; QueueRedraw(); };
        MouseExited += () => { _hovered = false; QueueRedraw(); };
        ButtonDown += QueueRedraw;
        ButtonUp += QueueRedraw;
    }

    /// <summary>The clock art the status bar uses, or null without local art.</summary>
    public static LayoutNode? Art => ClassicMykoArt.Layout(L.HpMenu)?.Find("btn_scheduler");

    public int State => ClassicMykoArt.StateOf(this, _hovered);

    public override void _Draw()
    {
        if (ClassicMykoArt.State(_art, State) is { } image && ClassicMykoArt.Texture(image) is { } texture)
            DrawTextureRectRegion(texture, new Rect2(Vector2.Zero, Size), ClassicMykoArt.Source(image));
    }
}
