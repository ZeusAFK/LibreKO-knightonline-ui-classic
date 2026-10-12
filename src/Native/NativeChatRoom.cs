using Godot;
using LibreKO;
using KnightOnlineUiClassic.Windows;

namespace KnightOnlineUiClassic.Native;

/// <summary>
/// Prepares the client's chat room window (3277df5) for the Classic skin: names the native controls the skin and
/// the audits look for, dresses the room's popups (room settings, voice settings, a member's volume, the emoticon
/// picker, the member menu) as the client creates them, and turns the client's minimized blink (a modulate tween
/// on the window) into the Classic attention styler, as the whisper windows do.
/// </summary>
public static class NativeChatRoom
{
    public const string ControlsMeta = "classic_chat_room_controls";
    private const string PopupsMeta = "classic_chat_room_popups";

    private static readonly (string Field, string Name)[] Named =
    {
        ("_roomScroll", "room_log_scroll"), ("_roomLog", "room_log"), ("_roomOlder", "room_older"),
        ("_roomComposerRow", "room_composer_row"), ("_roomInput", "room_input"), ("_roomRecord", "room_record"),
        ("_roomSend", "room_send"), ("_roomRecordStrip", "room_record_strip"), ("_roomRecordClock", "room_record_clock"),
        ("_roomRecordLevel", "room_record_level"), ("_roomVoiceRow", "room_voice_row"), ("_roomMembersToggle", "room_members_toggle"),
        ("_roomConfigButton", "room_config"), ("_roomJoin", "room_join"), ("_roomDeafen", "room_deafen"), ("_roomVolume", "room_volume"),
        ("_roomMic", "room_mic"), ("_roomVoiceGear", "room_voice_gear"), ("_roomVoiceLeave", "room_voice_leave"),
        ("_roomMembersColumn", "room_members_column"), ("_roomRosterRows", "room_roster_rows"), ("_roomRosterEmpty", "room_roster_empty"),
        ("_roomLeaderBox", "room_leader_box"), ("_roomLeaderHandOver", "room_leader_handover"), ("_roomLeaderKick", "room_leader_kick"),
    };

    public static void Prepare(HudWindow window, World world)
    {
        if (window.HasMeta(ControlsMeta)) return;
        var controls = Named.Select(n => (Control: Native.Get<Control>(world, n.Field), n.Name)).ToArray();
        if (controls.Any(c => c.Control == null)) return;
        // The row of the members column and the log, voice and composer stack: the client's own flow, placed by the skin.
        if (Native.Get<ScrollContainer>(world, "_roomScroll")!.GetParent()?.GetParent() is not HBoxContainer main) return;
        foreach (var (control, name) in controls) control!.Name = name;
        main.Name = "room_main";
        NativeWindows.Name(Native.Get<Button>(world, "_roomChannelsButton"), "room_channels");
        if (Native.Get<CanvasLayer>(world, "_roomLayer") is { } layer) StylePopups(layer);
        StylePopups(window);
        Attention(window, world);
        window.SetMeta(ControlsMeta, 1);
        ClassicChatRoomFrame.Of(window)?.Bind(window);
    }

    /// <summary>
    /// Dresses the popups the client parks on the room layer (or, in a preview, inside the window) as they are
    /// created: the Classic menu panel for the panels, the Classic drop-down look for the member menu.
    /// </summary>
    private static void StylePopups(Node holder)
    {
        if (holder.HasMeta(PopupsMeta)) return;
        holder.SetMeta(PopupsMeta, true);
        foreach (var popup in holder.GetChildren().OfType<Popup>()) Style(popup);
        holder.ChildEnteredTree += node =>
        {
            if (node is Popup popup) Callable.From(() => { if (Plugin.Active && GodotObject.IsInstanceValid(popup)) Style(popup); }).CallDeferred();
        };
    }

    private static void Style(Popup popup)
    {
        if (popup is PopupMenu menu) ClassicSocialStyle.Menu(menu);
        else if (popup is PopupPanel panel) ClassicChatRoomSkin.StylePopup(panel);
    }

    /// <summary>
    /// The client blinks the minimized room by fading the whole window when a line from someone else arrives;
    /// the Classic compact bar carries the whisper attention blink instead, until the room is restored.
    /// </summary>
    private static void Attention(HudWindow window, World world)
    {
        bool attention = false;
        NativeWindows.Sync(window, () =>
        {
            var blink = Native.Get<Tween>(world, "_roomBlink");
            if (blink != null && blink.IsValid())
            {
                blink.Kill();
                window.Modulate = Colors.White;
                if (window.AttentionStyler is { } styler) { attention = true; styler(true); }
            }
            else if (blink == null && attention)
            {
                attention = false;
                window.AttentionStyler?.Invoke(false);
            }
        });
    }
}
