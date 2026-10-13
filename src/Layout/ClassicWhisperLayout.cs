using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Additions to the original private-message frame (co_whisper_open_us): the client's header actions on the
/// title bar left of the original minimize button (btn_hide at x 239), the room the emoticon button takes
/// at the right end of the edit field, and the voice-note parts of the client's composer (fa3cfc3): the record
/// cell mirrors the original send button (btn_chat at 3,136) at the right end of the edit band, and the
/// recording strip takes the edit band while a note records.
/// </summary>
public static class ClassicWhisperLayout
{
    public const float ActionPitch = 19, HideLeft = 239;
    public static Rect2 Action(int index, int count) => new(HideLeft - 2 - (count - index) * ActionPitch, 5, 18, 15);
    /// <summary>
    /// The client's emoticon button inside the right end of the edit field, on the 12 px dark band of the original
    /// art (the band of the send and record cells, 136..148) so it neither covers the log nor the frame line.
    /// </summary>
    public const float EmoticonSize = 12, EmoticonInset = 5, EmoticonRoom = EmoticonSize + EmoticonInset * 2;

    /// <summary>Record (and, while recording, send-the-note) cell: btn_chat's 12 px cell, 3 px inside the right edge.</summary>
    public static readonly Rect2 Record = new(288, 136, 12, 12);
    /// <summary>Recording strip (dot, clock, level, cancel) on the edit field's text line: one ChatStrong line, 131..150.</summary>
    public static readonly Rect2 Strip = new(16, 131, 269, 19);
    public const int StripGap = 4;
    public const float StripCancel = 13;

    /// <summary>Thread lines: one ChatStrong line (18 px pitch) with the react cell beside its first line.</summary>
    public const int LineSize = 13, SmallSize = 11;
    public const float LinePitch = 18, ReactCell = 14, ReactIcon = 11, LineGap = 0;
    /// <summary>Voice-note line: play cell, waveform and clock on one line.</summary>
    public const float NoteWidth = 200, PlayCell = 16, PlayIcon = 10, WaveHeight = 16;
    public const int NoteGap = 4;
    /// <summary>Day marker: a small Classic chip with the gold line, 1 px above and below.</summary>
    public const float DayChipHeight = 15;
    public const int DayChipMargin = 1;
}
