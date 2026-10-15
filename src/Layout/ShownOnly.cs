using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Lets a node process only while a canvas item is shown: normally the skin itself, or the window a helper node
/// mirrors. The client builds every window when the World starts, so a skin that ticks while its window is closed
/// works every frame for nothing; its sync is only seen while the window is open.
/// <para>
/// Godot turns processing on at <c>NOTIFICATION_READY</c> for every script that overrides <c>_Process</c>, so the
/// state is applied once the node is ready, again whenever it enters the tree, and on every visibility change of
/// the target. When the target becomes visible, <c>sync</c> (normally the body of <c>_Process</c>) also runs once as
/// a deferred call. Godot flushes deferred calls before it draws the frame, so the first visible frame is synced
/// even when the window is shown after the node processed in that frame.
/// </para>
/// </summary>
public static class ShownOnly
{
    /// <summary>Processes <paramref name="node"/> only while it is visible in the tree.</summary>
    public static void Gate(CanvasItem node, Action? sync = null) => Gate(node, node, sync);

    /// <summary>Processes <paramref name="node"/> only while <paramref name="target"/> is visible in the tree.</summary>
    public static void Gate(Node node, CanvasItem target, Action? sync = null)
    {
        var gate = new State(node, target, sync);
        node.Ready += gate.Apply;
        node.TreeEntered += gate.Apply;
        target.VisibilityChanged += gate.Apply;
        if (node.IsNodeReady() && node.IsInsideTree()) gate.Apply();
    }

    private sealed class State
    {
        private readonly Node _node;
        private readonly CanvasItem _target;
        private readonly Action? _sync;
        private bool _queued;

        public State(Node node, CanvasItem target, Action? sync)
        {
            _node = node; _target = target; _sync = sync;
        }

        public void Apply()
        {
            if (!GodotObject.IsInstanceValid(_node))
            {
                // A helper freed before the window it follows: let go of the window's signal.
                if (GodotObject.IsInstanceValid(_target)) _target.VisibilityChanged -= Apply;
                return;
            }
            if (!GodotObject.IsInstanceValid(_target) || !_node.IsInsideTree()) return;
            bool shown = _target.IsVisibleInTree();
            if (_node.IsProcessing() != shown) _node.SetProcess(shown);
            if (!shown || _sync == null || _queued) return;
            _queued = true;
            Callable.From(RunQueued).CallDeferred();
        }

        private void RunQueued()
        {
            _queued = false;
            if (!Plugin.Active || !GodotObject.IsInstanceValid(_node) || !GodotObject.IsInstanceValid(_target)) return;
            if (!_node.IsInsideTree() || !_target.IsVisibleInTree()) return;
            _sync!();
        }
    }
}
