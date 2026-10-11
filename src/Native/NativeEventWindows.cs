using System.Runtime.CompilerServices;
using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Native;

public static partial class NativeSetup
{
    static partial void RegisterEventsA(PluginContext context)
    {
        foreach (var id in NativeEventWindows.WindowIds) NativeWindows.Prepare(id, NativeEventWindows.Prepare);
    }
}

/// <summary>
/// Names the controls of the Collection Race, Lottery, Roulette and Tournament windows and keeps the
/// presentation state the client does not: the roulette reel and prize history, the drawn lottery
/// winners and short pending locks between a request and its reply. Requests, prices, counts and
/// results remain the client's and the server's.
/// </summary>
public static class NativeEventWindows
{
    public static readonly string[] WindowIds = { "collectionrace", "lottery", "roulette", "tournament" };
    public const string ControlsMeta = "classic_events_controls";
    public const ulong PendingTimeoutMsec = 6000;
    public const ulong MinimumSpinMsec = 1200;

    public sealed class RouletteView
    {
        public bool StatusKnown, Spinning;
        public ulong SpinStarted, ResultAt;
        public bool? ResultOk;
        public int PrizeItem, PrizeGold;
        public RoulettePrize[] History = Array.Empty<RoulettePrize>();
        public int HistoryRevision;

        /// <summary>Whether the reel may stop: the reply arrived and the minimum spin has elapsed.</summary>
        public bool CanLand => Spinning && ResultOk == true && Time.GetTicksMsec() >= Math.Max(SpinStarted + MinimumSpinMsec, ResultAt);
    }

    public sealed class LotteryView
    {
        public ulong PendingSince;
        public List<LotteryWinner> Winners = new();
        public int WinnersRevision;
    }

    public sealed class TournamentView { public ulong PendingSince; }

    private static readonly ConditionalWeakTable<World, RouletteView> _roulette = new();
    private static readonly ConditionalWeakTable<World, LotteryView> _lottery = new();
    private static readonly ConditionalWeakTable<World, TournamentView> _tournament = new();

    private static readonly ConditionalWeakTable<HudWindow, World> _worlds = new();

    /// <summary>The world that owns a prepared event window.</summary>
    public static World? WorldOf(HudWindow window) => _worlds.TryGetValue(window, out var world) && GodotObject.IsInstanceValid(world) ? world : null;

    public static RouletteView Roulette(World world) => _roulette.GetOrCreateValue(world);
    public static LotteryView Lottery(World world) => _lottery.GetOrCreateValue(world);
    public static TournamentView Tournament(World world) => _tournament.GetOrCreateValue(world);

    public static void Prepare(HudWindow window, World world)
    {
        if (!NativeIdentity.Begin(window, "native_events")) return;
        _worlds.AddOrUpdate(window, world);
        bool ready = window.Id switch
        {
            "collectionrace" => PrepareCollectionRace(world),
            "lottery" => PrepareLottery(window, world),
            "roulette" => PrepareRoulette(window, world),
            "tournament" => PrepareTournament(window, world),
            _ => false,
        };
        if (ready) window.SetMeta(ControlsMeta, 1);
    }

    private static bool Name(World world, string field, string name) => NativeWindows.Name(Native.Get<Node>(world, field), name);

    private static bool PrepareCollectionRace(World world) =>
        Name(world, "_crCompletingLabel", "cr_completing") & Name(world, "_crWinnersRow", "cr_winners_row")
        & Name(world, "_crWinnersLabel", "cr_winners") & Name(world, "_crTimerDigits", "cr_time")
        & Name(world, "_crHuntBlock", "cr_hunt_block") & Name(world, "_crHuntBox", "cr_hunt")
        & Name(world, "_crCollectBlock", "cr_collect_block") & Name(world, "_crCollectBox", "cr_collect")
        & Name(world, "_crRewardsBlock", "cr_rewards_block") & Name(world, "_crRewardsBox", "cr_rewards")
        & Name(world, "_crCompleteBanner", "cr_complete") & Name(world, "_crCompleteTitle", "cr_complete_title");

    private static bool PrepareLottery(HudWindow window, World world)
    {
        var icon = Native.Get<TextureRect>(world, "_lotteryReqIcon");
        var cost = Native.Get<Label>(world, "_lotteryCostLabel");
        var buy = Native.Get<Button>(world, "_lotteryBuyBtn");
        bool named = Name(world, "_lotteryTimerLabel", "lottery_time") & Name(world, "_lotterySoldLabel", "lottery_sold")
            & Name(world, "_lotteryWinnersLabel", "lottery_winners") & Name(world, "_lotteryMyTicketsLabel", "lottery_tickets")
            & Name(world, "_lotteryPlayerHeader", "lottery_player") & Name(world, "_lotteryRewardsBox", "lottery_rewards")
            & Name(world, "_lotteryStatusLabel", "lottery_status")
            & NativeWindows.Name(icon, "lottery_req_icon") & NativeWindows.Name(icon?.GetParent(), "lottery_req_slot")
            & NativeWindows.Name(cost, "lottery_cost") & NativeWindows.Name(cost?.GetParent(), "lottery_cost_box")
            & NativeWindows.Name(buy, "lottery_buy");
        if (!named || buy == null || Net.I is not { } net) return false;
        var view = Lottery(world);
        // The client repaints Buy on every lottery update; the lock only covers the reply to this press.
        buy.Pressed += () => { view.PendingSince = Time.GetTicksMsec(); buy.Disabled = true; };
        Action detach = null!;
        Action<LotteryJoinResult> joined = _ => { if (!Gone(window, detach)) view.PendingSince = 0; };
        Action<LibreKO.Network.LotteryState> opened = state =>
        {
            if (Gone(window, detach) || !state.Active || view.Winners.Count == 0) return;
            view.Winners.Clear(); view.WinnersRevision++;
        };
        Action<string, List<LotteryWinner>> ended = (_, winners) =>
        {
            if (Gone(window, detach)) return;
            view.PendingSince = 0;
            view.Winners = winners.OrderBy(w => w.Place).ToList();
            view.WinnersRevision++;
        };
        detach = NativeHooks.Listen("Net.Lottery",
            () => { net.LotteryJoinEvent += joined; net.LotteryStateEvent += opened; net.LotteryEndedEvent += ended; },
            () => { net.LotteryJoinEvent -= joined; net.LotteryStateEvent -= opened; net.LotteryEndedEvent -= ended; }, world).Release;
        DetachWhenFreed(window, detach);
        NativeWindows.Sync(window, () =>
        {
            if (view.PendingSince == 0 || Time.GetTicksMsec() - view.PendingSince <= PendingTimeoutMsec) return;
            view.PendingSince = 0;
            Native.Call(world, "RenderLottery");
        });
        return true;
    }

    private static bool PrepareRoulette(HudWindow window, World world)
    {
        var spin = Native.Get<Button>(world, "_rouletteSpinBtn");
        var result = Native.Get<Label>(world, "_rouletteResultLabel");
        bool named = Name(world, "_rouletteCoinsLabel", "roulette_coins") & NativeWindows.Name(result, "roulette_result")
            & NativeWindows.Name(spin, "roulette_spin");
        if (!named || spin == null || result == null || Net.I is not { } net) return false;
        var view = Roulette(world);
        NativeIdentity.Rewire(spin, () =>
        {
            if (view.Spinning) return;
            bool affordable = Native.Get<int>(world, "_rouletteCoins") >= 1;
            Native.Call(world, "OnRouletteSpinPressed");
            if (!affordable) return;
            view.Spinning = true; view.SpinStarted = Time.GetTicksMsec();
            view.ResultOk = null; view.ResultAt = 0;
        });
        Action detach = null!;
        Action<int> status = _ => { if (!Gone(window, detach)) view.StatusKnown = true; };
        Action<bool, int, int> spun = (ok, item, gold) =>
        {
            if (Gone(window, detach) || !view.Spinning) return;
            view.ResultOk = ok; view.PrizeItem = item; view.PrizeGold = gold; view.ResultAt = Time.GetTicksMsec();
            if (!ok) { view.Spinning = false; return; }
            // The client names item prizes by number; show the item name when the item is known.
            if (gold <= 0 && item > 0 && ItemData.Get(item) != null) result.Text = $"You won {ItemData.DisplayName(item)}!";
            net.SendRoulettePrizeLog();
        };
        Action<RoulettePrize[]> history = entries =>
        {
            if (Gone(window, detach)) return;
            view.History = entries; view.HistoryRevision++;
        };
        detach = NativeHooks.Listen("Net.Roulette",
            () => { net.RouletteStatusEvent += status; net.RouletteSpinEvent += spun; net.RoulettePrizeLogEvent += history; },
            () => { net.RouletteStatusEvent -= status; net.RouletteSpinEvent -= spun; net.RoulettePrizeLogEvent -= history; }, world).Release;
        DetachWhenFreed(window, detach);
        window.VisibilityChanged += () => { if (window.Visible) net.SendRoulettePrizeLog(); };
        NativeWindows.Sync(window, () =>
        {
            if (view.Spinning && view.ResultOk == null && Time.GetTicksMsec() - view.SpinStarted > PendingTimeoutMsec) view.Spinning = false;
            spin.Disabled = view.Spinning || view.StatusKnown && Native.Get<int>(world, "_rouletteCoins") < 1;
        });
        return true;
    }

    /// <summary>Stops the reel after its landing animation.</summary>
    public static void FinishSpin(World world) => Roulette(world).Spinning = false;

    private static bool PrepareTournament(HudWindow window, World world)
    {
        var register = Native.Get<Button>(world, "_tournamentRegBtn");
        var unregister = Native.Get<Button>(world, "_tournamentUnregBtn");
        bool named = Name(world, "_tournamentRoundLbl", "tournament_round") & Name(world, "_tournamentCountLbl", "tournament_count")
            & Name(world, "_tournamentStateLbl", "tournament_state")
            & NativeWindows.Name(register, "tournament_register") & NativeWindows.Name(unregister, "tournament_unregister");
        if (!named || register == null || unregister == null || Net.I is not { } net) return false;
        var view = Tournament(world);
        // Both buttons wait for the server's status; the client repaints them when it arrives.
        void Lock() { view.PendingSince = Time.GetTicksMsec(); register.Disabled = unregister.Disabled = true; }
        register.Pressed += Lock;
        unregister.Pressed += Lock;
        Action detach = null!;
        Action<bool, int, int> status = (_, _, _) => { if (!Gone(window, detach)) view.PendingSince = 0; };
        detach = NativeHooks.Listen("Net.TournamentStatusEvent", () => net.TournamentStatusEvent += status,
            () => net.TournamentStatusEvent -= status, world).Release;
        DetachWhenFreed(window, detach);
        NativeWindows.Sync(window, () =>
        {
            if (view.PendingSince == 0 || Time.GetTicksMsec() - view.PendingSince <= PendingTimeoutMsec) return;
            view.PendingSince = 0;
            bool registered = Native.Get<bool>(world, "_tournamentRegistered");
            register.Disabled = registered; unregister.Disabled = !registered;
        });
        return true;
    }

    private static bool Gone(HudWindow window, Action detach)
    {
        if (GodotObject.IsInstanceValid(window) && !window.IsQueuedForDeletion()) return false;
        detach();
        return true;
    }

    private static void DetachWhenFreed(HudWindow window, Action detach) =>
        window.TreeExiting += () => { if (window.IsQueuedForDeletion()) detach(); };
}
