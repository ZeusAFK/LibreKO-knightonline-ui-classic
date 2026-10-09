using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Editable geometry of the castle siege windows, measured on the original warfare UIF artwork. The artwork
/// is common to both nations, as in the original client.
/// </summary>
public static class ClassicSiegeLayout
{
    /// <summary>NPC talk windows: portrait frame, two speech lines and four menu rows.</summary>
    public sealed record Speech(string Art, Vector2 Size, Rect2 Greeting, Rect2 Question, float[] Options, Rect2 Drag)
    {
        public static readonly Rect2 Option = new(42, 0, 200, 18);
        public Rect2 OptionRect(int index) => new(Option.Position.X, Options[index], Option.Size.X, Option.Size.Y);
    }

    /// <summary>List windows (list heights are whole rows): title, three column headings over a scrolled list, notes and two footer actions.</summary>
    public sealed record Table(string Art, Rect2 List, float[] Columns, float Gap, Rect2[] Heads, Rect2 Scroll, Rect2[] Actions)
    {
        public static readonly Vector2 Size = new(286, 366);
        public static readonly Rect2 Title = new(36, 19, 214, 20);
        public static readonly Rect2 Close = new(256, 10, 20, 20);
        public static readonly Rect2 Drag = new(3, 3, 248, 44);
        public const float RowHeight = 17;
    }

    public static readonly Speech Guard = new("co_warfarenpc_us", new(279, 306),
        new(108, 20, 160, 56), new(108, 80, 160, 44), new float[] { 157, 183, 209, 235 }, new(3, 3, 273, 148));

    public static readonly Speech Office = new("co_warfareadministrationnpc_us", new(279, 306),
        new(96, 56, 172, 22), new(96, 78, 172, 56), new float[] { 137, 160, 183, 206 }, new(3, 3, 273, 132));

    public static readonly Speech TaxList = new("co_warfaretaxtariff_us", new(279, 306),
        new(84, 70, 188, 24), new(84, 96, 188, 46), new float[] { 157, 182, 207, 232 }, new(3, 3, 273, 148));

    private static readonly Rect2[] Footer = { new(33, 331, 94, 27), new(158, 331, 94, 27) };

    public static readonly Table Schedule = new("co_warfareschedule_us", new(8, 69, 257, 170), new float[] { 136, 57, 58 }, 3,
        new Rect2[] { new(8, 51, 136, 16), new(147, 51, 57, 16), new(207, 51, 58, 16) }, new(266, 68, 14, 180), Footer);

    // The Members heading spans the header cell above the scroll bar, as the original string does.
    private static readonly Rect2[] ClanHeads = { new(8, 51, 163, 16), new(175, 51, 57, 16), new(233, 51, 46, 16) };

    public static readonly Table Challengers = new("co_warfarepetition_us", new(8, 69, 257, 136), new float[] { 163, 57, 29 }, 4,
        ClanHeads, new(266, 68, 14, 144), new Rect2[] { new(33, 331, 94, 27), new(157, 331, 95, 27) });

    public static readonly Table Defenders = new("co_castleunion_us", new(8, 69, 257, 221), new float[] { 163, 57, 29 }, 4,
        ClanHeads, new(266, 68, 14, 230), Footer);

    /// <summary>Challenger notes, in native order: signed up, chosen, fee and registration period.</summary>
    public static readonly Rect2[] ChallengerNotes = { new(9, 224, 268, 15), new(9, 239, 268, 28), new(9, 267, 268, 15), new(9, 282, 268, 30) };

    public static Speech? SpeechFor(string id) => id switch
    {
        "siegeguard" => Guard,
        "siegeoffice" => Office,
        "siegetaxlist" => TaxList,
        _ => null,
    };

    public static Table? TableFor(string id) => id switch
    {
        "siegeschedule" => Schedule,
        "siegechallengers" => Challengers,
        "siegedefenders" => Defenders,
        _ => null,
    };
}
