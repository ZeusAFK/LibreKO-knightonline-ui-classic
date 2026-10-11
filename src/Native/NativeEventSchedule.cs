using System.Runtime.CompilerServices;
using Godot;
using LibreKO;
using LibreKO.Domain;

namespace KnightOnlineUiClassic.Native;

/// <summary>
/// The client's event schedule as the Events window shows it: the running or sign-up events (<c>Now</c>) and the
/// starts in the next 24 hours, in the order and with the actions of the client's own rows (Join for a temple
/// sign-up, Open for the lottery). The client keeps requesting and receiving the schedule.
/// </summary>
public static class NativeEventSchedule
{
    /// <summary>The client's own window for upcoming starts (<c>World.EventsUpcomingWindow</c>).</summary>
    public static readonly TimeSpan Upcoming = TimeSpan.FromHours(24);

    public readonly record struct Row(string Name, int MinLevel, int MaxLevel, EventPhase? Phase, int SecondsLeft, DateTime? StartUtc, string? Action, EventNow? Now);

    private static readonly ConditionalWeakTable<HudWindow, World> _worlds = new();

    /// <summary>Records the world of an Events window built outside it (the preview builds it detached).</summary>
    public static void Bind(HudWindow window, World world) => _worlds.AddOrUpdate(window, world);

    public static World? WorldOf(HudWindow window) =>
        _worlds.TryGetValue(window, out var world) && GodotObject.IsInstanceValid(world) ? world : Native.WorldOf(window) ?? NativeHudChat.World;

    /// <summary>The schedule the client received, or null while it is loading.</summary>
    public static EventSchedule? Schedule(World world) => Native.Get<EventSchedule>(world, "_eventSchedule");

    /// <summary>Rows in the client's order: what runs now, then the next starts.</summary>
    public static List<Row> Rows(EventSchedule schedule, DateTime utcNow)
    {
        var rows = new List<Row>();
        foreach (var entry in schedule.Now)
            rows.Add(new Row(entry.Name, entry.MinLevel, entry.MaxLevel, entry.Phase, schedule.SecondsLeft(entry, utcNow), null, ActionOf(entry), entry));
        foreach (var next in schedule.Upcoming(utcNow, Upcoming))
            rows.Add(new Row(next.Slot.Name, next.Slot.MinLevel, next.Slot.MaxLevel, null, 0, next.StartUtc, null, null));
        return rows;
    }

    /// <summary>The client's action caption for a running entry (World.NowAction), or null.</summary>
    public static string? ActionOf(EventNow entry) => entry switch
    {
        { Kind: EventKind.Temple, Phase: EventPhase.SignUp } => "Join",
        { Kind: EventKind.Lottery } => "Open",
        _ => null,
    };

    /// <summary>Runs the client's action of a running entry, as its Join or Open button does.</summary>
    public static bool Run(World world, EventNow entry) => ActionOf(entry) switch
    {
        "Join" => Native.TryCall(world, "OfferBifrostJoin", out _),
        "Open" => Native.TryCall(world, "ToggleLottery", out _),
        _ => false,
    };

    /// <summary>The client's level band text (World.LevelBand).</summary>
    public static string LevelBand(int minLevel, int maxLevel) =>
        minLevel <= 0 && maxLevel <= 0 ? "All levels" : $"Lv {Math.Max(1, minLevel)}-{(maxLevel > 0 ? maxLevel : 83)}";

    public static string PhaseText(EventPhase phase) => phase == EventPhase.SignUp ? "Sign-up" : "Running";

    /// <summary>A start time as the client writes it: the local time, with the day when it is not today.</summary>
    public static string StartText(DateTime startUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(startUtc, TimeZoneInfo.Local);
        return local.Date == DateTime.Now.Date ? local.ToString("HH:mm") : local.ToString("ddd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string LeftText(int seconds)
    {
        var left = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return left.TotalHours >= 1 ? left.ToString(@"h\:mm\:ss") : left.ToString(@"mm\:ss");
    }
}
