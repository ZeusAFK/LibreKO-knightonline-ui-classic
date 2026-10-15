using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Name lookups over a fixed set of native controls, resolved once. Reading <c>Node.Name</c> creates a Godot
/// <c>StringName</c> wrapper per control, so a linear scan per lookup in per-frame code produced most of the plugin's
/// finalizable garbage. A found control is kept until it is freed (or, with <c>live</c>, queued for deletion); then
/// the same scan runs again. A name that is not found stays not found: the set of controls never grows.
/// </summary>
public sealed class ControlIndex
{
    private readonly Control[] _controls;
    private readonly bool _live;
    private readonly Dictionary<(string Name, Type Type), Control?> _found = new();

    /// <param name="controls">The controls to search, in their original order.</param>
    /// <param name="live">Skip controls queued for deletion, as well as freed ones.</param>
    public ControlIndex(Control[] controls, bool live = false)
    {
        _controls = controls; _live = live;
    }

    public IReadOnlyList<Control> All => _controls;

    private bool Usable(Control control) => GodotObject.IsInstanceValid(control) && !(_live && control.IsQueuedForDeletion());

    /// <summary>The only control of type <typeparamref name="T"/> with this name; throws as <c>Single</c> does when there is none.</summary>
    public T Single<T>(string name) where T : Control =>
        Maybe<T>(name) ?? throw new InvalidOperationException($"No {typeof(T).Name} named '{name}'.");

    /// <summary>The first control of type <typeparamref name="T"/> with this name, or null.</summary>
    public T? Maybe<T>(string name) where T : Control
    {
        var key = (name, typeof(T));
        if (_found.TryGetValue(key, out var known) && (known == null || Usable(known))) return (T?)known;
        T? found = null;
        foreach (var control in _controls)
            if (control is T typed && Usable(control) && control.Name == name) { found = typed; break; }
        _found[key] = found;
        return found;
    }
}
