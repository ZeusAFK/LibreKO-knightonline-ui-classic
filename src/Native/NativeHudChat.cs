using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Native;

public static partial class NativeSetup
{
    static partial void RegisterHud(PluginContext context)
    {
        NativeLayout.WatchContentAnchors();
        var game = context.Game;
        Action attached = () => NativeHudChat.Attach(game);
        NativeHooks.Listen("Game.BecameAvailable", () => game.BecameAvailable += attached, () => game.BecameAvailable -= attached);
    }

    static partial void RegisterChat(PluginContext context)
    {
        NativeWindows.PrepareMatching(NativeWhispers.IsWhisper, NativeWhispers.Prepare);
        NativeWindows.Prepare("chat_colors", NativeChatColours.Prepare);
    }
}

/// <summary>
/// The HUD side of the client the Classic HUD and chat need: hidden top icons, the service windows the
/// taskbar and shortcuts open, notification counts, commands, hotbar selection and minimap state.
/// </summary>
public static class NativeHudChat
{
    private static WeakReference<World>? _world;

    /// <summary>The running client world, as attached to the plugin game API.</summary>
    public static World? World
    {
        get
        {
            if (_world != null && _world.TryGetTarget(out var world) && GodotObject.IsInstanceValid(world)) return world;
            return null;
        }
    }

    /// <summary>Runs whenever the client attaches a world to the plugin game API.</summary>
    internal static void Attach(PluginGame game)
    {
        if (Native.Get<object>(game, "_windows") is not { } proxy || Native.Get<IGameWindows>(proxy, "Source") is not { } source) return;
        if (source is NativeServiceWindows wrapped) source = wrapped.Inner;
        if (!Native.Has(source, "_w") || Native.Get<World>(source, "_w") is not { } world) return;
        _world = new WeakReference<World>(world);
        var wrapper = new NativeServiceWindows(source, world);
        if (Native.Set(proxy, "Source", wrapper))
            NativeHooks.Hold("Game.Windows.Source", () =>
            {
                if (ReferenceEquals(Native.Get<IGameWindows>(proxy, "Source"), wrapper)) Native.Set(proxy, "Source", source);
            }, world);
        HideIcons(world);
        DockClearOfStatus(world);
        // The Genie window is built after the client attaches the plugin game API.
        var built = new WeakReference<World>(world);
        Callable.From(() => { if (Plugin.Active && built.TryGetTarget(out var w) && GodotObject.IsInstanceValid(w)) OpenClearOfMiniMap(w); }).CallDeferred();
        NativeQuestKey.Attach(world, game);
        NativeGoldLog.Attach(world);
        NativeChat.Attach(world);
        NativeWhispers.Attach(world);
    }

    internal static void Reset() => _world = null;

    private static void HideIcons(World world, bool retry = true)
    {
        var icons = new (NativeHudPart Part, string Button, string? Blink)[]
        {
            (NativeHudPart.MailIcon, "_mailIconButton", null),
            (NativeHudPart.AchievementsIcon, "_trophy", "_trophyBlink"),
            (NativeHudPart.AttendanceIcon, "_attendanceGift", "_attendanceGiftBlink"),
            (NativeHudPart.PowerUpStoreIcon, "_powerUpStoreIcon", null),
            // The status bar's G, E and R open these; E and R flash for the alert and the new-day blink.
            (NativeHudPart.GenieIcon, "_genieIcon", null),
            (NativeHudPart.EventsIcon, "_eventsIcon", null),
            (NativeHudPart.DailyRankIcon, "_drIcon", null),
        };
        HideIcons(world, icons, retry);
    }

    private static void HideIcons(World world, (NativeHudPart Part, string Button, string? Blink)[] icons, bool retry)
    {
        bool changed = false;
        var later = new List<(NativeHudPart, string, string?)>();
        foreach (var (part, field, blink) in icons)
        {
            if (!NativeHud.IsHidden(part)) continue;
            if (Native.Get<Control>(world, field) is not { } button)
            {
                // Some icons (daily rank) are created after the client attaches the plugin game API.
                if (Native.Has(world, field)) later.Add((part, field, blink));
                continue;
            }
            button.Visible = false;
            button.VisibilityChanged += () => { if (button.Visible) button.Visible = false; };
            if (blink != null && Native.Get<Tween>(world, blink) is { } tween && tween.IsValid()) tween.Kill();
            button.Modulate = Colors.White;
            changed = true;
        }
        if (later.Count > 0 && retry)
        {
            var weak = new WeakReference<World>(world);
            Callable.From(() =>
            {
                if (Plugin.Active && weak.TryGetTarget(out var w) && GodotObject.IsInstanceValid(w)) HideIcons(w, later.ToArray(), false);
            }).CallDeferred();
        }
        if (!changed) return;
        Native.TryCall(world, "QueueDockLayout", out _);
        // Close the gaps the hidden icons leave in the remaining top-right strip.
        Native.TryCall(world, "PlaceTopIcons", out _);
    }

    private const float StatusGap = 4f;

    /// <summary>
    /// The client docks the Character window clear of its own HUD, which no longer includes the Classic status bar
    /// that replaces it: the window would sit over the bar and catch the clicks meant for its letter row. The docked
    /// spot moves just below the bar, its letter row and envelope; a window the player moved keeps its place.
    /// </summary>
    private static void DockClearOfStatus(World world)
    {
        if (Native.Get<System.Collections.IDictionary>(world, "_mainWindows") is not { } windows || !windows.Contains("Character")
            || windows["Character"] is not HudWindow window || Native.Get<Func<Vector2>>(window, "_dock") is not { } dock) return;
        var weak = new WeakReference<World>(world);
        window.DockTo(() =>
        {
            var spot = dock();
            if (ClassicStatus(weak) is not { } status) return spot;
            var bar = status.BarBounds;
            if (!new Rect2(spot, window.Size).Intersects(bar)) return spot;
            float room = window.GetViewportRect().Size.Y - window.Size.Y - StatusGap;
            return new Vector2(spot.X, Mathf.Max(spot.Y, Mathf.Min(bar.End.Y + StatusGap, room)));
        });
    }

    /// <summary>
    /// The Genie window's first spot (the client's, beside its own small HUD) lies under the Classic minimap, which is
    /// drawn above it; it opens right of the minimap instead. A window the player moved keeps its place.
    /// </summary>
    private static void OpenClearOfMiniMap(World world)
    {
        if (Native.Get<HudWindow>(world, "_geniePanel") is not { } window || !GodotObject.IsInstanceValid(window.Layout)
            || Native.Get<Func<Vector2>>(window.Layout, "_defaultPosition") is not { } placement) return;
        var weak = new WeakReference<World>(world);
        Native.Set(window.Layout, "_defaultPosition", (Func<Vector2>)(() =>
        {
            var spot = placement();
            if (ClassicStatus(weak) is not { MiniMapBounds: { } map } || !new Rect2(spot, window.Size).Intersects(map)) return spot;
            float room = window.GetViewportRect().Size.X - window.Size.X - StatusGap;
            return new Vector2(Mathf.Max(spot.X, Mathf.Min(map.End.X + StatusGap, room)), spot.Y);
        }));
        // The layout placed the window when it was built; place it again whenever it opens (unless moved or saved).
        var layout = window.Layout;
        window.VisibilityChanged += () =>
        {
            if (Plugin.Active && window.Visible && GodotObject.IsInstanceValid(layout)) Native.TryCall(layout, "ReapplyDefault", out _);
        };
    }

    private static KnightOnlineUiClassic.Windows.StatusHud? ClassicStatus(WeakReference<World> weak) =>
        Plugin.Active && weak.TryGetTarget(out var w) && GodotObject.IsInstanceValid(w)
        && Native.Get<System.Collections.IDictionary>(w, "_pluginHud") is { } parts && parts.Contains(HudPart.StatusBars)
        && parts[HudPart.StatusBars] is Node part && GodotObject.IsInstanceValid(part) ? Status(part) : null;

    private static KnightOnlineUiClassic.Windows.StatusHud? Status(Node node)
    {
        if (node is KnightOnlineUiClassic.Windows.StatusHud status) return status.IsVisibleInTree() ? status : null;
        if (node is HudWindow) return null;
        foreach (var child in node.GetChildren())
            if (Status(child) is { } found) return found;
        return null;
    }

    public static int NotificationCount(string id)
    {
        if (World is not { } world) return 0;
        return id.ToLowerInvariant() switch
        {
            "mail" => Net.I?.MailUnread ?? 0,
            "achievements" => Native.Get<System.Collections.IDictionary>(world, "_achClaimablePerTab") is { } tabs
                ? tabs.Values.Cast<int>().Sum() : 0,
            "attendance" => Native.Call(world, "ClaimableAttendanceCount") is int count ? count : 0,
            // Attention states rather than counts: an event starting soon, a new day's rankings not yet viewed.
            "events" => Native.Get<bool>(world, "_eventsAlert") ? 1 : 0,
            "daily_rank" => Native.Get<bool>(world, "_drBlink") ? 1 : 0,
            _ => 0,
        };
    }

    public static int SelectedAbs
    {
        get
        {
            if (World is not { } world) return -1;
            int selected = Native.Get<int>(world, "_hotSelected");
            if (selected < 0 || Native.Get<int[]>(world, "_hotbar") is not { } slots) return -1;
            return selected < slots.Length && slots[selected] != 0 ? selected : -1;
        }
    }

    public static void Select(int abs)
    {
        if (World is { } world) Native.Call(world, "SelectHotAbs", abs);
    }

    public static bool Running => World is not { } world || Native.Get<bool>(world, "_running");
    public static bool Sitting => World is { } world && Native.Get<bool>(world, "_selfSitting");
    public static bool AutoAttacking => World is { } world && Native.Get<bool>(world, "_autoAttack");
    public static void ToggleRun() => Command("ToggleRunMode");
    public static void ToggleAttack() => Command("ToggleAutoAttack");
    public static void TurnCamera() => Command("StartCameraHalfTurn");
    public static void OpenGameMenu() { if (World is { } world) Native.Call(world, "ToggleEsc", (bool?)true); }

    private static void Command(string method) { if (World is { } world) Native.Call(world, method); }

    /// <summary>
    /// Whether the minimap is shown. The Classic HUD hides the native minimap layer, so the native
    /// control's own visibility only records the player's minimap toggle.
    /// </summary>
    public static bool MiniMapVisible => World is not { } world || Native.Get<Control>(world, "_miniMap") is not { } map || map.Visible;
}

/// <summary>
/// Opens and closes the service windows the client does not register as plugin windows (mail,
/// achievements, attendance, Power-Up Store, zone map, genie, events and daily rank) through their own
/// toggles, and passes every other id to the client.
/// </summary>
public sealed class NativeServiceWindows : IGameWindows
{
    private readonly World _world;

    public NativeServiceWindows(IGameWindows inner, World world)
    {
        Inner = inner;
        _world = world;
    }

    public IGameWindows Inner { get; }

    private static readonly Dictionary<string, (string Shown, string Toggle, string? Close)> Services = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mail"] = ("_mailShown", "ToggleMail", null),
        ["achievements"] = ("_achShown", "ToggleAchievements", null),
        ["attendance"] = ("_attendanceShown", "ToggleAttendance", null),
        ["shoppingmall"] = ("_pusShown", "ToggleShoppingMall", null),
        ["zonemap"] = ("_fullMapShown", "ToggleFullMap", null),
        ["genie"] = ("_genieShown", "ToggleGenie", "CloseGenie"),
        // Shown by the window itself. The events toggle asks for the schedule; the daily rank one builds the window
        // on first use and records the day as viewed, as the client's top-right icons did.
        ["events"] = ("_eventsPanel", "ToggleEvents", null),
        ["daily_rank"] = ("_drWindow", "OnDailyRankIcon", null),
        // The party window is gone; the taskbar's party button opens the party board, which loads its list on open.
        ["seek_party"] = ("_seekShown", "ToggleSeekParty", null),
    };

    private bool Service(string id, out (string Shown, string Toggle, string? Close) service) =>
        Services.TryGetValue(id, out service) && GodotObject.IsInstanceValid(_world) && Native.Has(_world, service.Shown);

    private bool Shown(string member) => Native.Get<object>(_world, member) switch
    {
        bool shown => shown,
        CanvasItem window => GodotObject.IsInstanceValid(window) && window.Visible,
        _ => false,
    };

    public bool IsOpen(string id) => Service(id, out var service) ? Shown(service.Shown) : Inner.IsOpen(id);

    public void Open(string id)
    {
        if (!Service(id, out var service)) { Inner.Open(id); return; }
        if (!Shown(service.Shown)) Native.Call(_world, service.Toggle);
    }

    public void Close(string id)
    {
        if (!Service(id, out var service)) { Inner.Close(id); return; }
        if (service.Close != null) Native.Call(_world, service.Close);
        else if (Shown(service.Shown)) Native.Call(_world, service.Toggle);
    }

    public void Toggle(string id)
    {
        if (!Service(id, out var service)) { Inner.Toggle(id); return; }
        Native.Call(_world, service.Toggle);
    }
}
