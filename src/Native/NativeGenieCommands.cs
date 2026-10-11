using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;

namespace KnightOnlineUiClassic.Native;

/// <summary>
/// Genie rows of the Classic command list. The client has no Genie slash command; its Genie is opened with the
/// Genie key (keypad +) or the quick panel's settings button (<c>ToggleGenie</c>), started and stopped by the quick
/// panel's Start/Stop toggle (<c>ToggleGenieFromSub</c>, with the client's own cooldown and message) and given time
/// by the Genie window's "Use Spirit of Genie" button. Each row runs that client action; a row is disabled while
/// the client's own state says the action cannot run, with the client's status line as the reason.
/// </summary>
public static class NativeGenieCommands
{
    public const string Window = "GenieWindow", Start = "GenieStart", Stop = "GenieStop", Spirit = "GenieSpirit";
    public static readonly string[] Ids = { Window, Start, Stop, Spirit };

    /// <summary>The client's texts: the quick panel's Start/Stop hints (31008/31009) and the Genie window's button and status lines.</summary>
    private const int StartHintText = 31008, StopHintText = 31009;
    private const string SpiritButton = "Use Spirit of Genie";
    private const string StoppedLine = "Genie stopped.", WaitingLine = "Waiting for server response…";
    private const string NoTimeLine = "No time remaining. Spirit of Genie required.";

    /// <summary>The row state the list shows: enabled, and the tooltip (the action's hint, or why it cannot run).</summary>
    public readonly record struct State(bool Enabled, string Tooltip);

    /// <summary>Whether the client exposes the Genie actions these rows run.</summary>
    public static bool Supported(World world) =>
        Native.HasMethod(world, "ToggleGenie") && (Native.HasMethod(world, "ToggleGenieFromSub") || Native.HasMethod(world, "ToggleGenieRun"))
        && Native.Has(world, "_genieShown") && Native.Has(world, "_genieRequestPending");

    /// <summary>The Genie rows as command list rows (no client <c>CommandEntry</c>: the list runs them through <see cref="Run"/>).</summary>
    public static List<NativeCommandList.Row> Rows(World world)
    {
        var rows = new List<NativeCommandList.Row>();
        if (!Supported(world)) return rows;
        foreach (string id in Ids)
        {
            if (id == Spirit && !Native.Has(world, "_geniePanel")) continue;
            var state = StateOf(world, id);
            rows.Add(new NativeCommandList.Row(id, "", Text(id), state.Tooltip, state.Enabled, null));
        }
        return rows;
    }

    public static string Text(string id) => id switch
    {
        Window => "Genie Window",
        Start => "Start Genie",
        Stop => "Stop Genie",
        _ => SpiritButton,
    };

    public static State StateOf(World world, string id)
    {
        bool running = Net.I?.GenieRunning == true;
        bool pending = Native.Get<bool>(world, "_genieRequestPending");
        string status = RunLine(world);
        switch (id)
        {
            case Window:
                return new State(true, "Genie settings");
            case Start:
                if (running || pending) return new State(false, status);
                // The client knows the remaining time once the server has reported it (the Genie window, a start or a tick).
                if (Reported(world) && Native.Get<int>(world, "_geniePoints") <= 0) return new State(false, NoTimeLine);
                return new State(true, ItemData.Text(StartHintText, "Start Knight Genie"));
            case Stop:
                return running && !pending ? new State(true, ItemData.Text(StopHintText, "Disable Knight Genie")) : new State(false, status);
            default:
                // The client spends a Spirit of Genie only when the bag holds one (its own count, as its automatic use does).
                return SpiritCount(world) > 0 ? new State(true, SpiritButton) : new State(false, "No Spirit of Genie in the bag.");
        }
    }

    /// <summary>Runs the client's own action for the row, if the client still allows it.</summary>
    public static bool Run(World world, string id)
    {
        if (!Supported(world) || !StateOf(world, id).Enabled) return false;
        switch (id)
        {
            case Window:
                // As the Merchant command does: open, never close, the window the row names.
                if (!Native.Get<bool>(world, "_genieShown")) Native.Call(world, "ToggleGenie");
                return true;
            case Start or Stop:
                if (Native.HasMethod(world, "ToggleGenieFromSub")) Native.Call(world, "ToggleGenieFromSub");
                else Native.Call(world, "ToggleGenieRun");
                return true;
            case Spirit:
                if (SpiritButtonOf(world) is not { } button) return false;
                button.EmitSignal(BaseButton.SignalName.Pressed);
                return true;
        }
        return false;
    }

    /// <summary>The client's status line in the Genie window ("Genie active • 90 minutes", "Genie stopped.", …).</summary>
    private static string RunLine(World world)
    {
        string text = Native.Get<Label>(world, "_genieRunLabel") is { } label && GodotObject.IsInstanceValid(label) ? label.Text : "";
        if (text.Length > 0) return text;
        return Native.Get<bool>(world, "_genieRequestPending") ? WaitingLine : StoppedLine;
    }

    /// <summary>The client sets its Genie deadline whenever the server reports the remaining time.</summary>
    private static bool Reported(World world) => Native.TryGet(world, "_genieDeadline", out double deadline) && deadline != 0;

    private static int SpiritCount(World world)
    {
        if (!Native.TryGet(typeof(World), "SpiritOfGenieItem", out int item)) return 0;
        try { return Native.TryCall(world, "CountInBackpack", out var count, item) && count is int n ? n : 0; }
        catch (Exception) { return 0; }
    }

    /// <summary>The Genie window's own "Use Spirit of Genie" button (its callback is the client's request).</summary>
    private static Button? SpiritButtonOf(World world)
    {
        if (Native.Get<Control>(world, "_geniePanel") is not { } panel || !GodotObject.IsInstanceValid(panel)) return null;
        var buttons = Native.Descendants(panel).OfType<Button>().ToList();
        return buttons.FirstOrDefault(b => b.Text == SpiritButton) ?? buttons.FirstOrDefault(b => b.Name == "genie_spirit");
    }
}
