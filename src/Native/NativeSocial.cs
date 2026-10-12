using Godot;
using LibreKO;

namespace KnightOnlineUiClassic.Native;

/// <summary>
/// Prepares the native social windows the Classic social frame shows (nation chat, clan and join-request
/// boards, leader transfer, commander): names the controls the skin and the audits look for
/// and replaces symbol-only captions with the English words of the original client.
/// </summary>
public static class NativeSocial
{
    internal static void Register()
    {
        NativeWindows.Prepare("nation_chat", PrepareNationChat);
        NativeWindows.Prepare("nation_chat_members", PrepareNationMembers);
        NativeWindows.Prepare("commander", PrepareCommander);
        NativeCommandList.Register();
    }

    /// <summary>Runs the social preparer of a window built outside the client's window flow (audits).</summary>
    public static void Prepare(HudWindow window, World world)
    {
        if (window.Id == "nation_chat") PrepareNationChat(window, world);
        else if (window.Id == "nation_chat_members") PrepareNationMembers(window, world);
        else if (window.Id == "commander") PrepareCommander(window, world);
    }

    private static void PrepareNationChat(HudWindow window, World world)
    {
        if (window.HasMeta("classic_social_controls")) return;
        window.SetMeta("classic_social_controls", 1);
        if (Native.Get<Button>(world, "_ncRefresh") is { } refresh)
        {
            refresh.Name = "nation_refresh";
            refresh.Text = "Refresh";
        }
        NativeWindows.Name(Native.Get<Button>(world, "_ncPublicTab"), "nation_public_tab");
        NativeWindows.Name(Native.Get<Button>(world, "_ncPrivateTab"), "nation_private_tab");
        NativeWindows.Name(Native.Get<VBoxContainer>(world, "_ncPublicRows"), "nation_public_rows");
        NativeWindows.Name(Native.Get<VBoxContainer>(world, "_ncPrivateRows"), "nation_private_rows");
        // 3277df5: "Open" shows the chat room of the joined channel or room; it sits in the Classic button row.
        NativeWindows.Name(Native.Get<Button>(world, "_ncPubOpen"), "nation_public_open");
        NativeWindows.Name(Native.Get<Button>(world, "_ncPriOpen"), "nation_private_open");
    }

    /// <summary>The members window lost its header row in 3277df5: the room title and the summary line sit in the body.</summary>
    private static void PrepareNationMembers(HudWindow window, World world)
    {
        if (window.HasMeta("classic_social_controls")) return;
        window.SetMeta("classic_social_controls", 1);
        NativeWindows.Name(Native.Get<Label>(world, "_ncMembersTitle"), "nation_members_title");
        NativeWindows.Name(Native.Get<Label>(world, "_ncMembersCount"), "nation_members_count");
        NativeWindows.Name(Native.Get<VBoxContainer>(world, "_ncMemberRows"), "nation_member_rows");
    }

    private static void PrepareCommander(HudWindow window, World world)
    {
        if (window.HasMeta("classic_social_controls")) return;
        window.SetMeta("classic_social_controls", 1);
        // The direction toggle is a symbol in the client; the Classic button carries its word.
        if (window.Body.GetChildren().OfType<Button>().FirstOrDefault() is { } flip)
        {
            flip.Name = "commander_flip";
            flip.Text = "Rotate";
        }
    }
}
