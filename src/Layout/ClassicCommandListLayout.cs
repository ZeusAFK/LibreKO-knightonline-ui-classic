using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Editable geometry of the Classic command list, taken from the original 1.298 command window
/// (<c>el_cmdlist_us</c> / <c>ka_cmdlist_us</c>, the <c>CUICmdList</c> "curtailment" window): the tall
/// translucent panel with the close ornament, the "Command" caption, the category list (<c>list_curtailment</c>)
/// in the upper box and the command list (<c>list_content</c>) in the lower box, each with the original
/// scroll bar. Human and Karus differ by a pixel or two, so each nation keeps its own rectangles.
/// </summary>
public static class ClassicCommandListLayout
{
    public const string WindowId = "command_list";
    public const string Uif = "{nation}_cmdlist_us";
    public const string Texture = "ui_curtail_us.png";

    public sealed record Nation(
        Vector2 Size,
        Rect2 Panel, Rect2 PanelSource,
        Rect2 Foot, Rect2 FootSource,
        Rect2 Title, Rect2 Close,
        Rect2 Groups, Scroll GroupScroll,
        Rect2 Commands, Scroll CommandScroll,
        Color TitleInk);

    /// <summary>An original list scroll bar: its area, the 14 x 16 arrow buttons and the thumb track between them.</summary>
    public sealed record Scroll(Rect2 Area, Rect2 Up, Rect2 Down, Rect2 Track);

    public static readonly Nation ElMorad = new(
        Size: new(165, 573),
        Panel: new(1, 1, 162, 512), PanelSource: new(3, 0, 162, 512),
        Foot: new(1, 513, 162, 60), FootSource: new(347, 15, 162, 60),
        Title: new(12, 56, 142, 16), Close: new(135, 17, 21, 20),
        Groups: new(13, 85, 138, 205),
        GroupScroll: new(new(135, 85, 16, 204), new(136, 85, 14, 16), new(136, 273, 14, 16), new(136, 100, 14, 174)),
        Commands: new(12, 304, 139, 242),
        CommandScroll: new(new(135, 304, 16, 241), new(136, 304, 14, 16), new(136, 527, 14, 16), new(136, 321, 14, 207)),
        TitleInk: new("ead29b"));

    public static readonly Nation Karus = new(
        Size: new(165, 576),
        Panel: new(0, 0, 163, 512), PanelSource: new(175, 0, 163, 512),
        Foot: new(0, 512, 163, 64), FootSource: new(346, 79, 163, 64),
        Title: new(12, 56, 142, 16), Close: new(134, 18, 20, 20),
        Groups: new(11, 85, 141, 209),
        GroupScroll: new(new(135, 84, 17, 210), new(137, 84, 14, 16), new(137, 278, 14, 16), new(137, 102, 14, 176)),
        Commands: new(12, 305, 139, 245),
        CommandScroll: new(new(135, 305, 16, 243), new(137, 305, 14, 16), new(137, 532, 14, 16), new(137, 321, 14, 211)),
        TitleInk: new("c0c0c0"));

    public static Nation For(bool karus) => karus ? Karus : ElMorad;

    /// <summary>The original window is not dragged by its panel; the title band above the lists is the handle.</summary>
    public static Rect2 Drag(Nation n) => new(n.Panel.Position.X, 40, n.Close.Position.X - n.Panel.Position.X - 2, n.Groups.Position.Y - 44);

    /// <summary>The original thumb keeps its 14 x 16 art and slides inside the track.</summary>
    public static readonly Vector2 Thumb = new(14, 16);
    public static Rect2 ThumbAt(Rect2 track, double ratio) =>
        new(track.Position.X, track.Position.Y + Mathf.Round((float)(Math.Clamp(ratio, 0, 1) * (track.Size.Y - Thumb.Y))), Thumb.X, Thumb.Y);

    /// <summary>
    /// List text: the original lists use GulimChe 10 pt (13 px). The Classic UI draws it in Arial 13 px; rows are
    /// the text height (the original <c>CN3UIList</c> stacks rows by text extent) and start 3 px inside the box.
    /// </summary>
    public const int ListFontSize = 13, TitleFontSize = 16;
    public const int RowGap = 0, TextInset = 3;
    public static Color GroupInk => new("ffff00");
    public static Color CommandInk => new("ffffff");
    public static Color UnavailableInk => new("808080");
    /// <summary>The original marks the selected row with a 1 px green outline and the focused list with a yellow one 2 px outside.</summary>
    public static Color SelectedLine => new("00ff00");
    public static Color HoverLine => new(0, 1, 0, .38f);
    public static Color FocusLine => new("ffff00");
    public const float FocusOutset = 2;

    /// <summary>
    /// Presentation groups. The client's catalog keeps its own six groups; the Classic list regroups its entries
    /// by what they do. Each entry names client command ids (<c>CommandId</c> member names); a group title
    /// that is a client group name keeps the client's text. Commands the client adds later and this table does
    /// not name stay in the group named after their client group.
    /// </summary>
    public sealed record Group(string Title, string? ClientGroup, string[] Commands);

    public static readonly Group[] Groups =
    {
        new("Private", "Private", new[] { "Town", "Exit", "Location", "SitStand", "WalkRun", "IndividualBattle", "Vault", "Achieve", "Recommend" }),
        new("Emotion", null, new[] { "Greeting", "Greeting2", "Greeting3", "Provoke", "Provoke2", "Provoke3" }),
        new("Chat", null, new[] { "Pm", "AddFriend", "ChatBlock", "NationChat" }),
        new("Trade", "Trade", new[] { "Trade", "BlockTrade", "AllowTrade", "Merchant", "Price", "EditMerchant" }),
        new("Party", "Party", new[] { "Party", "QuitParty", "SeekingParty", "BlockParty", "AllowParty" }),
        new("Clan", "Clan", new[] { "JoinClan", "QuitClan", "BanFromClan", "AppointVice", "ClanChat", "ClanNotice", "ClanWar", "Surrender", "ClanCommand", "ClanBattle", "ClanKick" }),
        new("Knights", "Knights", new[] { "Confederacy", "QuitKnights", "BanKnights", "Report" }),
        new("King", "King", new[] { "RoyalOrder", "Prize", "ExperiencePoint", "DropRate", "Rain", "Clear" }),
    };
}
