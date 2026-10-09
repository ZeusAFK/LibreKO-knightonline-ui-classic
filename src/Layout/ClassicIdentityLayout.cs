using Godot;

namespace KnightOnlineUiClassic.Layout;

public static class ClassicIdentityLayout
{
    public static string Artwork(string id) => id switch
    {
        "creat_clan" => "co_creat_clan_us",
        "namechange" => "co_idchange_us",
        "disguise" => "co_disguisering_us",
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    public static Vector2 Size(string id) => id switch
    {
        "creat_clan" => new(327, 156),
        "namechange" => new(364, 192),
        "disguise" => new(176, 562),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    /// <summary>Rename and Transformation use the plain Classic frame instead of their ornamented outer art.</summary>
    public static bool Plain(string id) => id is "namechange" or "disguise";
    /// <summary>Gold title on the content's left edge (the original art had no title row).</summary>
    public static Rect2 Title(string id) => id == "disguise" ? new(17, 14, 142, 24) : new(16, 14, 332, 24);
    // Rename: explanation, the original ID bar, status and actions on the plain frame's 16 px inset.
    public static readonly Rect2 RenameBar = new(18, 102, 327, 25);
    public static readonly Rect2 RenameId = new(24, 103, 31, 22);
    public static readonly Rect2 RenameStatus = new(18, 129, 327, 18);
    // Transformation keeps the original list boxes (art x 13..160); the actions share their edges.
    public static readonly Rect2 DisguiseGroupBox = new(13, 58, 148, 205);
    public static readonly Rect2 DisguiseFormBox = new(13, 268, 148, 244);
    public static Rect2 Field(string id, string name)
    {
        if (id == "namechange")
            switch (name)
            {
                case "text_explanation": return new Rect2(16, 44, 332, 54);
                case "edit_id": return new Rect2(58, 105, 275, 19);
                case "btn_ok": return new Rect2(16, 147, 97, 29);
                case "btn_cancel": return new Rect2(251, 147, 97, 29);
            }
        if (id == "disguise" && name is "btn_ok" or "btn_close")
            return name == "btn_ok" ? new Rect2(17, 526, 69, 22) : new Rect2(89, 526, 69, 22);
        var layout = Plugin.Kit.Layout(Artwork(id));
        var field = layout.Find(name) ?? throw new InvalidOperationException("Missing original identity field: " + name);
        return new Rect2(field.Position - layout.Position, field.SizeVec);
    }
}
