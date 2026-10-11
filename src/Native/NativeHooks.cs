using System.Reflection;
using Godot;

namespace KnightOnlineUiClassic.Native;

/// <summary>
/// Every hook the plugin attaches to objects it does not own: scene-tree watchers, handlers on the client's
/// network events, handlers that replace one of the World's own handlers, and patched client delegates.
/// The client switches UI plugins at runtime (Settings > Plugins) and keeps the plugin assembly loaded, so
/// each hook is released exactly once: by the caller, when its owner node (normally the World) leaves the
/// tree, or when the plugin shuts down.
/// </summary>
public static class NativeHooks
{
    /// <summary>One attached hook and the action that detaches it.</summary>
    public sealed class Hook
    {
        private readonly Action _release;
        private readonly Node? _owner;
        private readonly Action? _ownerExit;

        internal Hook(string kind, Action release, Node? owner, object? key)
        {
            Kind = kind;
            Key = key;
            _release = release;
            if (owner == null || !GodotObject.IsInstanceValid(owner)) return;
            _owner = owner;
            _ownerExit = Release;
            owner.TreeExiting += _ownerExit;
        }

        public string Kind { get; }
        internal object? Key { get; }
        public bool Active { get; private set; } = true;

        public void Release()
        {
            if (!Active) return;
            Active = false;
            _hooks.Remove(this);
            if (_ownerExit != null && GodotObject.IsInstanceValid(_owner)) _owner!.TreeExiting -= _ownerExit;
            try { _release(); }
            catch (Exception e) { GD.PushError($"[plugin:knightonline-ui-classic] releasing {Kind}: {e.Message}"); }
        }
    }

    private static readonly List<Hook> _hooks = new();

    /// <summary>The hooks currently attached.</summary>
    public static int Count => _hooks.Count;

    /// <summary>The hooks currently attached, by kind.</summary>
    public static IReadOnlyDictionary<string, int> Counts =>
        _hooks.GroupBy(h => h.Kind).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>True while <see cref="ReleaseAll"/> runs: release actions restore what they replaced.</summary>
    public static bool ShuttingDown { get; private set; }

    /// <summary>Plugin delegates the last shutdown found outside the tracked hooks, per holder.</summary>
    public static IReadOnlyDictionary<string, int> LastSweep { get; private set; } = new Dictionary<string, int>();

    /// <summary>Records a hook that is already attached; <paramref name="release"/> detaches it.</summary>
    public static Hook Hold(string kind, Action release, Node? owner = null, object? key = null)
    {
        var hook = new Hook(kind, release, owner, key);
        _hooks.Add(hook);
        return hook;
    }

    /// <summary>Attaches a handler with <paramref name="add"/> and records <paramref name="remove"/>.</summary>
    public static Hook Listen(string kind, Action add, Action remove, Node? owner = null)
    {
        add();
        return Hold(kind, remove, owner);
    }

    /// <summary>Calls <paramref name="added"/> for every node that enters the scene tree.</summary>
    public static Hook? WatchTree(string kind, SceneTree.NodeAddedEventHandler added, Node? owner = null)
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return null;
        tree.NodeAdded += added;
        return Hold(kind, () => { if (GodotObject.IsInstanceValid(tree)) tree.NodeAdded -= added; }, owner);
    }

    /// <summary>
    /// Replaces the handlers of a client delegate field that <paramref name="replaced"/> selects (normally one of the
    /// World's own handlers) with <paramref name="replacement"/>. When the owner leaves the tree only the replacement
    /// is removed; when the plugin shuts down while the owner stays, the replaced handlers are restored.
    /// </summary>
    public static Hook? Replace(object holder, string member, Func<Delegate, bool> replaced, Delegate replacement, Node owner)
    {
        if (Native.Get<Delegate>(holder, member) is not { } current) return null;
        var removed = current.GetInvocationList().Where(replaced).ToArray();
        var kept = removed.Aggregate((Delegate?)current, Delegate.Remove);
        if (!Native.Set(holder, member, Delegate.Combine(kept, replacement))) return null;
        return Hold(holder.GetType().Name + "." + member, () =>
        {
            var now = Native.Get<Delegate>(holder, member);
            now = Delegate.Remove(now, replacement);
            if (ShuttingDown && GodotObject.IsInstanceValid(owner) && owner.IsInsideTree())
                now = Delegate.Combine(new[] { now }.Concat(removed).ToArray());
            Native.Set(holder, member, now);
        }, owner);
    }

    /// <summary>Releases the hook recorded with <paramref name="key"/>, if any; returns whether one was found.</summary>
    internal static bool ReleaseKey(object key)
    {
        var hook = _hooks.FirstOrDefault(h => Equals(h.Key, key));
        hook?.Release();
        return hook != null;
    }

    /// <summary>Releases every hook, newest first.</summary>
    public static int ReleaseAll()
    {
        int released = _hooks.Count;
        ShuttingDown = true;
        try
        {
            while (_hooks.Count > 0) _hooks[^1].Release();
        }
        finally { ShuttingDown = false; }
        return released;
    }

    /// <summary>
    /// Removes the plugin's delegates that are still attached to <paramref name="holder"/>'s delegate fields (and, for
    /// the client's plugin game API, to its nested source proxies). Returns how many were removed.
    /// </summary>
    public static int Sweep(object holder)
    {
        int removed = 0;
        foreach (var (target, field, calls) in DelegateFields(holder))
        {
            int ours = calls.Count(Ours);
            if (ours == 0) continue;
            removed += ours;
            field.SetValue(target, Delegate.Combine(calls.Where(d => !Ours(d)).ToArray()));
        }
        return removed;
    }

    /// <summary>Plugin delegates still attached to <paramref name="holder"/>, without removing them (for audits).</summary>
    public static int Attached(object holder) => DelegateFields(holder).Sum(f => f.Calls.Count(Ours));

    /// <summary>Disconnects the plugin's handlers that are still connected to the scene tree's <c>node_added</c>.</summary>
    public static int SweepTree()
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return 0;
        var ours = TreeConnections(tree).ToArray();
        foreach (var callable in ours) tree.Disconnect(SceneTree.SignalName.NodeAdded, callable);
        return ours.Length;
    }

    /// <summary>Plugin handlers connected to the scene tree's <c>node_added</c> (for audits).</summary>
    public static int TreeWatchers() => Engine.GetMainLoop() is SceneTree tree ? TreeConnections(tree).Count() : 0;

    private static bool Ours(Delegate handler) => handler.Method.Module.Assembly == typeof(NativeHooks).Assembly;

    private static IEnumerable<Callable> TreeConnections(SceneTree tree) =>
        tree.GetSignalConnectionList(SceneTree.SignalName.NodeAdded)
            .Select(c => c["callable"].AsCallable())
            .Where(c => c.Delegate is { } handler && Ours(handler));

    private static IEnumerable<(object Target, FieldInfo Field, Delegate[] Calls)> DelegateFields(object holder)
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var targets = new List<object> { holder };
        foreach (var field in holder.GetType().GetFields(Fields))
            if (field.FieldType.DeclaringType == holder.GetType() && field.GetValue(holder) is { } nested) targets.Add(nested);
        foreach (var target in targets)
            for (var type = target.GetType(); type != null && type != typeof(object); type = type.BaseType)
                foreach (var field in type.GetFields(Fields))
                    if (typeof(Delegate).IsAssignableFrom(field.FieldType) && field.GetValue(target) is Delegate handlers)
                        yield return (target, field, handlers.GetInvocationList());
    }

    internal static void RecordSweep(IReadOnlyDictionary<string, int> sweep) => LastSweep = sweep;
}
