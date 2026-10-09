using Godot;

namespace KnightOnlineUiClassic.Layout;

// World Map and Fortune Teller have no original UIF. Both are composed from the nation Zone List (warp)
// artwork: its title band, the inset that opens under it, the inset's closing strip and the plain lower
// band, with the warp title, 22-pixel list rows and the original message buttons.
public static class ClassicMapFortuneLayout
{
    public static readonly Rect2 Title = new(11, 11, 240, 19);
    public static Rect2 Close(float width) => new(width - 36, 7, 24, 24);
    public static Rect2 Grip(float width) => new(4, 4, width - 44, 30);
    public const int Row = 22;

    public static class Map
    {
        public static readonly Vector2 Size = new(320, 332);
        // The inset's lower border; the inset itself opens at the top of the warp title band.
        public const float InsetBottom = 288;
        public static readonly Rect2 Heads = new(17, 39, 268, Row);
        public static readonly float[] Columns = { 142, 56, 70 };
        public static Rect2 Head(int column)
        {
            float x = Heads.Position.X;
            for (int i = 0; i < column; i++) x += Columns[i];
            return new Rect2(x, Heads.Position.Y, Columns[column], Row);
        }
        public static readonly Rect2 HeadRule = new(17, 62, 286, 1);
        public const int Rows = 10;
        public static readonly Rect2 List = new(17, 64, 268, Rows * Row);
        public static readonly Rect2 Scroll = new(285, 64, 18, Rows * Row);
        public static readonly Rect2 Total = new(13, 296, 294, Row);
    }

    public static class Fortune
    {
        public static readonly Vector2 Size = new(600, 386);
        public const float InsetBottom = 324;
        // Native card and face geometry is kept; the fan is centred in the inset.
        public const float CardTop = 62, CardLeft = 31, NativeCardTop = 34;
        public static readonly Rect2 Greeting = new(13, 272, 574, 24);
        public static readonly Rect2 Prompt = new(13, 338, 574, 29);
        public static readonly Rect2 Face = new(37, 62, 178, 236);
        // The reading column spans the face; the turn caption sits on its centre line.
        public static readonly Rect2 Reading = new(230, 62, 342, 236);
        public static readonly Rect2 Reveal = new(230, 168, 342, 24);
        public static readonly Rect2 Ok = new(252, 338, 97, 29);
    }
}
