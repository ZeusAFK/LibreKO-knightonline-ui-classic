using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using KnightOnlineUiClassic.Native;

/// <summary>
/// Castle siege fixtures. The client's own <c>BuildSiegeUiPreview</c> builds and fills each window; these
/// add long-list states and run the siege preparer as the client's window flow would before the skin.
/// </summary>
public static partial class PreviewFixtures
{
    public static HudWindow BuildSiegeWindowPreview(World world, string view)
    {
        var window = (HudWindow)Call(world, "BuildSiegeUiPreview", view)!;
        NativeSiege.Prepare(window, world);
        return window;
    }

    /// <summary>The guard window detached with its native layout, so header dragging stays live.</summary>
    public static HudWindow BuildSiegeDragPreview(World world)
    {
        Call(world, "BuildSiegeWindows"); Call(world, "OpenSiegeGuard");
        var window = Detach<HudWindow>(world, "_siegeGuardPanel");
        NativeSiege.Prepare(window, world);
        return window;
    }

    private static readonly string[] SiegeLongClans =
    {
        "Olympus", "Avalon", "Midgard", "Asgard", "Brotherhood of the Endless Northern Crusade", "Vanguard", "Templars",
        "Valkyries", "Ironclad", "Nightfall", "Dawnbreakers", "Ravens",
    };

    public static void FillSiegeLongChallengers(World world, bool applied)
    {
        var clans = SiegeLongClans.Select((name, i) => new SiegeClanRow(applied && i == 9 ? "Valhalla" : name,
            (byte)(i % 2 == 0 ? Nations.Karus : Nations.ElMorad), (byte)(50 - i * 3))).ToArray();
        Call(world, "OnSiegeChallengers", new SiegeChallengers(SiegeWarfare.Success, clans, 12, 6, 1_000_000, 3, 9, 0, 3));
    }

    public static void FillSiegeLongDefenders(World world, bool empty)
    {
        var clans = empty ? Array.Empty<SiegeClanRow>() : SiegeLongClans.Concat(SiegeLongClans.Select(n => n + " II"))
            .Select((name, i) => new SiegeClanRow(name, (byte)Nations.Karus, (byte)(48 - i))).ToArray();
        Call(world, "OnSiegeDefenders", new SiegeDefenders(SiegeWarfare.Success, clans));
    }

    public static void FillSiegeLongSchedule(World world, bool empty)
    {
        var rows = empty ? Array.Empty<SiegeScheduleRow>() : Enumerable.Range(0, 14)
            .Select(i => new SiegeScheduleRow(i % 3 == 0 ? SiegeWarfare.CastleWarType : (byte)1, (byte)(1 + i % 7), (byte)(18 + i % 5), (byte)(i % 2 * 30))).ToArray();
        Call(world, "OnSiegeSchedule", new SiegeSchedule(SiegeWarfare.Success, rows));
    }
}
