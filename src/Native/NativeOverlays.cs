using Godot;
using LibreKO;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Native;

/// <summary>Native world overlays that have no window id: prompts, event plates, banners, menus and loot.</summary>
public enum NativeOverlay
{
    /// <summary>The temple event registration modal (a full-rect host named <c>event_registration</c>).</summary>
    Registration,
    /// <summary>The registration countdown plate.</summary>
    Countdown,
    /// <summary>The in-zone event leave plate.</summary>
    Leave,
    /// <summary>The death notice panel.</summary>
    Death,
    /// <summary>A native confirmation dialog (party invite, duel challenge, town recall).</summary>
    Prompt,
    /// <summary>A centre-screen broadcast banner or prompt line.</summary>
    Announcement,
    /// <summary>The zone type chip.</summary>
    Chip,
    /// <summary>A war score strip.</summary>
    Score,
    /// <summary>A world context menu.</summary>
    Menu,
    /// <summary>The loot window.</summary>
    Loot,
}

/// <summary>
/// Prepares the client's world overlays for the Classic skins once the world is attached. The
/// preparer names the native controls a skin looks for and mirrors native state into metas
/// (<see cref="Urgent"/>, <see cref="Registered"/>); every native control, callback and timer stays.
/// </summary>
public static class NativeOverlays
{
    public const string Urgent = "overlay_urgent", Registered = "overlay_registered";
    private const string Prepared = "classic_overlays";
    private static readonly Dictionary<NativeOverlay, List<Action<Node>>> _skins = new();

    public static void Extend(NativeOverlay part, Action<Node> skin)
    {
        if (!_skins.TryGetValue(part, out var list)) _skins[part] = list = new();
        list.Add(skin);
    }

    internal static void Register(PluginContext context) =>
        context.Game.BecameAvailable += () => { if (NativeHudChat.World is { } world) Prepare(world); };

    /// <summary>Prepares every overlay of a world; safe to call again.</summary>
    public static void Prepare(World world)
    {
        if (!GodotObject.IsInstanceValid(world) || world.HasMeta(Prepared)) return;
        world.SetMeta(Prepared, true);
        PrepareEvents(world);
        Apply(NativeOverlay.Death, Named(world, "_deathPanel", "death_notice"));
        foreach (var (field, name) in new[] { ("_inviteAskDialog", "party_invite_prompt"), ("_challengeAskDialog", "duel_prompt"), ("_townRecallDialog", "town_recall_prompt") })
            Apply(NativeOverlay.Prompt, Named(world, field, name));
        foreach (var (field, name) in new[] { ("_upgradeNoticeLabel", "upgrade_announcement"), ("_shoutLabel", "shout_announcement") })
            if (Native.Get<Label>(world, field)?.GetParent()?.GetParent() is PanelContainer panel) Apply(NativeOverlay.Announcement, Named(panel, name));
        Apply(NativeOverlay.Announcement, Named(world, "_battleeventBanner", "battle_announcement"));
        Apply(NativeOverlay.Announcement, Named(world, "_battleeventResult", "battle_result"));
        Apply(NativeOverlay.Announcement, Named(world, "_clanBattlePanel", "clan_battle_announcement"));
        Apply(NativeOverlay.Announcement, Named(world, "_lootPrompt", "loot_prompt"));
        Apply(NativeOverlay.Chip, Named(world, "_zoneabilityChip", "zone_chip"));
        Apply(NativeOverlay.Score, Named(world, "_battleeventBoard", "battle_score"));
        Apply(NativeOverlay.Score, Named(world, "_bdwScoreBanner", "border_war_score"));
        Apply(NativeOverlay.Menu, Named(world, "_playerMenu", "player_menu"));
        PrepareLoot(world);
    }

    private static void PrepareEvents(World world)
    {
        var modal = Named(world, "_joinModal", "event_registration");
        Named(world, "_joinModalTitle", "registration_title");
        Named(world, "_joinModalTimer", "registration_timer");
        Named(world, "_joinModalBar", "registration_bar");
        Named(world, "_joinModalStatus", "registration_status");
        Named(world, "_joinModalCount", "registration_count");
        Named(world, "_joinModalBtn", "registration_join");
        Named(world, "_joinModalCloseBtn", "registration_close");
        var countdown = Named(world, "_bifrostBanner", "event_countdown");
        Named(world, "_bifrostTitleLbl", "countdown_title");
        Named(world, "_bifrostTimerLbl", "countdown_timer");
        Named(world, "_bifrostBar", "countdown_bar");
        var leave = Named(world, "_inZoneLeaveBanner", "event_leave");
        Named(world, "_inZoneLeaveTitleLbl", "leave_title");
        Named(world, "_inZoneLeaveBtn", "leave_action");
        // The registration and countdown share the client's remaining time and registration state.
        if (modal is Control host)
        {
            var watcher = new NativeEventWatcher(() =>
            {
                if (!GodotObject.IsInstanceValid(world)) return;
                bool urgent = Native.Get<int>(world, "_bifrostRemaining") is var left && left > 0 && left <= Native.Get<int>(typeof(World), "BifrostUrgentSecs");
                bool registered = Native.Get<bool>(world, "_isEventRegistered");
                foreach (var node in new[] { host, countdown })
                {
                    if (node == null || !GodotObject.IsInstanceValid(node)) continue;
                    node.SetMeta(Urgent, urgent);
                    node.SetMeta(Registered, registered);
                }
            }) { Name = "classic_event_state", ProcessPriority = int.MinValue };
            host.AddChild(watcher);
        }
        Apply(NativeOverlay.Registration, modal);
        Apply(NativeOverlay.Countdown, countdown);
        Apply(NativeOverlay.Leave, leave);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Node, World> _lootWorlds = new();

    private static void PrepareLoot(World world)
    {
        // The loot box is a twelve-cell grid of client item cells (entry order) with Take all and Filter under it.
        if (Named(world, "_lootPanel", "loot_window") is not { } panel || Native.Get<ItemSlotView[]>(world, "_lootCells") is not { } cells) return;
        for (int i = 0; i < cells.Length; i++)
        {
            if (Named(cells[i], "loot_slot_" + i) == null) return;
            if (i == 0) Named(cells[i].GetParent(), "loot_grid");
        }
        if (cells[0].GetParent()?.GetParent() is Node column)
            foreach (var actions in column.GetChildren().OfType<HBoxContainer>())
            {
                var buttons = actions.GetChildren().OfType<Button>().ToArray();
                if (buttons.Length < 2) continue;
                Named(actions, "loot_actions"); Named(buttons[0], "loot_take_all"); Named(buttons[1], "loot_filter");
            }
        _lootWorlds.AddOrUpdate(panel, world);
        Apply(NativeOverlay.Loot, panel);
    }

    /// <summary>Closes the loot box through the client, as Escape does; the client's box has no close button.</summary>
    public static void CloseLoot(Node panel)
    {
        if (_lootWorlds.TryGetValue(panel, out var world) && GodotObject.IsInstanceValid(world)) Native.Call(world, "CloseLoot");
    }

    private static Node? Named(World world, string field, string name) => Named(Native.Get<Node>(world, field), name);

    private static Node? Named(Node? node, string name)
    {
        if (node == null || !GodotObject.IsInstanceValid(node)) return null;
        node.Name = name;
        return node;
    }

    private static void Apply(NativeOverlay part, Node? node)
    {
        if (node == null || !_skins.TryGetValue(part, out var skins)) return;
        foreach (var skin in skins)
        {
            try { skin(node); }
            catch (Exception e) { GD.PushError($"[plugin:knightonline-ui-classic] {node.Name} overlay: {e}"); }
        }
    }
}

public static partial class NativeSetup
{
    static partial void RegisterOverlays(PluginContext context) => NativeOverlays.Register(context);
}
