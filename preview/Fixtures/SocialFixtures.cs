using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;

/// <summary>
/// Social window fixtures for the Classic audits: nation chat, clan and join-request boards, leader transfer,
/// commander and command list. They reuse the client's own preview builders where it ships one and build the
/// remaining states through the client's handlers, so every window shows real native content.
/// </summary>
public static partial class PreviewFixtures
{
    private static HudWindow Detached(Control control) => (HudWindow)Native.Call(typeof(World), "DetachPreviewControl", control)!;

    /// <summary>Builds one social window in a named state and returns it detached from the world.</summary>
    public static HudWindow BuildSocialUiPreview(World world, string view)
    {
        ItemData.EnsureLoaded();
        switch (view)
        {
            case "nation-public": return (HudWindow)Call(world, "BuildNationChatUiPreview", "public")!;
            case "nation-private": return (HudWindow)Call(world, "BuildNationChatUiPreview", "private")!;
            case "nation-create": return (HudWindow)Call(world, "BuildNationChatUiPreview", "create")!;
            case "nation-password":
                Call(world, "NationChatInit");
                Field<LineEdit>(world, "_ncPasswordEdit").Text = "1234";
                Field<HudWindow>(world, "_ncPassword").Visible = true;
                return Detached(Field<HudWindow>(world, "_ncPassword"));
            case "commander": return (HudWindow)Call(world, "BuildCommanderUiPreview", (byte)0)!;
            case "clan-board": return (HudWindow)Call(world, "BuildClanBoardsUiPreview", "clan-board")!;
            case "clan-message": return (HudWindow)Call(world, "BuildClanBoardsUiPreview", "clan-message")!;
            case "wish-board": return (HudWindow)Call(world, "BuildClanBoardsUiPreview", "wish-board")!;
            case "wish-message": return (HudWindow)Call(world, "BuildClanBoardsUiPreview", "wish-message")!;
            case "clan-join-list":
                Call(world, "ClanBoardInit");
                Call(world, "OnClanBoardStatus", new ClanBoardStatus(ClanBoards.StateStaff, new List<ClanApplicant>
                {
                    new(72, 112, "Rin", "Priest, online most evenings."),
                    new(68, 107, "Kaito", "Archer looking for a Lunar War clan."),
                    new(80, 205, "LongCharacterName1299", "Warrior with siege experience, ready to join today."),
                }, 0, 0, ""));
                Call(world, "OpenClanJoinList");
                return Detached(Field<HudWindow>(world, "_cjWindow"));
            case "wish-requests":
                Call(world, "ClanBoardInit");
                Call(world, "OnWishRequests", new List<WishRequest>
                {
                    new(10, "Alpha", "Wolves", 2, 2, 0, "Join us for the Lunar War tonight."),
                    new(12, "Cassandra", "NightWatch", 1, 2, 0, "Royal clan, castle siege every week."),
                });
                return Detached(Field<HudWindow>(world, "_wrWindow"));
            case "clanhandover": return (HudWindow)Call(world, "BuildClanHandoverUiPreview")!;
            case "command-list": return (HudWindow)Call(world, "BuildCommandListUiPreview", 0)!;
        }
        throw new ArgumentException("Unknown social view " + view);
    }
}
