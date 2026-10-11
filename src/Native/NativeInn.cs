using System.Runtime.CompilerServices;
using Godot;
using LibreKO;
using LibreKO.Network;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Native;

/// <summary>
/// Recognises the Inn Hostess's opening choice. The client answers the hostess (warehouse sub 0x10) with its
/// shared NPC dialogue, so the Classic skin learns which menu is the inn's from the same network event and
/// shows it on the original co_inn_us art. The marks follow the client's own choice buttons and end with them.
/// </summary>
public static class NativeInn
{
    private static readonly ConditionalWeakTable<World, NativeHooks.Hook> _hooks = new();
    private static readonly ConditionalWeakTable<Node, object> _choices = new();

    public static void Prepare(HudWindow window, World world)
    {
        if (Net.I is not { } net || _hooks.TryGetValue(world, out var held) && held.Active) return;
        var weakWorld = new WeakReference<World>(world);
        // The client may subscribe before or after the plugin; mark once its menu is built.
        Action handler = () =>
        {
            if (weakWorld.TryGetTarget(out var w) && GodotObject.IsInstanceValid(w))
                Callable.From(() => { if (GodotObject.IsInstanceValid(w)) Opened(w); }).CallDeferred();
        };
        _hooks.AddOrUpdate(world, NativeHooks.Listen("Net.WarehouseNpcEvent", () => net.WarehouseNpcEvent += handler, () => net.WarehouseNpcEvent -= handler, world));
    }

    /// <summary>Marks the choice buttons of the NPC dialogue the client has just built for the Inn Hostess.</summary>
    public static void Opened(World world)
    {
        if (Native.Get<HudWindow>(world, "_npcPanel") is not { Visible: true }) return;
        if (Native.Get<VBoxContainer>(world, "_npcMenuBox") is not { } menu || !GodotObject.IsInstanceValid(menu)) return;
        foreach (var choice in menu.GetChildren().OfType<Button>()) _choices.AddOrUpdate(choice, true);
    }

    /// <summary>True while the native NPC menu holds the Inn Hostess's choices.</summary>
    public static bool IsInnMenu(Node menu) =>
        GodotObject.IsInstanceValid(menu) && menu.GetChildCount() > 0 && _choices.TryGetValue(menu.GetChild(0), out _);
}

public static partial class NativeSetup
{
    static partial void RegisterNpc(PluginContext context) => NativeWindows.Prepare("npc_dialog", NativeInn.Prepare);
}
