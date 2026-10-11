using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Additions to the original private-message frame (co_whisper_open_us): the client's header actions on the
/// title bar left of the original minimize button (btn_hide at x 239), and the room the emoticon button takes
/// at the right end of the edit field.
/// </summary>
public static class ClassicWhisperLayout
{
    public const float ActionPitch = 19, HideLeft = 239;
    public static Rect2 Action(int index, int count) => new(HideLeft - 2 - (count - index) * ActionPitch, 5, 18, 15);
    public const float EmoticonRoom = 26;
}
