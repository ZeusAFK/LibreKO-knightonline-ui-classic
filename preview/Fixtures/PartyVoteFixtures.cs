using System.Reflection;
using Godot;
using LibreKO;
using LibreKO.Plugins;

/// <summary>
/// Party, user vote and user card fixtures (3277df5). The client attaches its user card and user vote bridges
/// to its global plugin game API; the preview runs the plugin on its own <see cref="PluginGame"/>, so these
/// entry points link the two the way the client does when it hands the plugin its game API.
/// </summary>
public static partial class PreviewFixtures
{
    private static bool _linked;

    /// <summary>Points the plugin's user card and user vote API at the client's, and forwards their changes.</summary>
    public static void LinkClientFacades(PluginGame game)
    {
        if (_linked) return;
        _linked = true;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var host = PluginHost.Game;
        typeof(PluginGame).GetMethod("AttachUserInfo", flags)!.Invoke(game, new object[] { host.UserInfo });
        typeof(PluginGame).GetMethod("AttachUserVote", flags)!.Invoke(game, new object[] { host.UserVote });
        var raiseInfo = typeof(PluginGame).GetMethod("RaiseUserInfo", flags)!;
        var raiseVote = typeof(PluginGame).GetMethod("RaiseUserVote", flags)!;
        host.UserInfo.Changed += () => raiseInfo.Invoke(game, null);
        host.UserVote.Changed += () => raiseVote.Invoke(game, null);
    }

    /// <summary>Moves a world canvas layer into the preview scene so the windows the client adds to it render.</summary>
    public static T Adopt<T>(World world, string layer, Node scene) where T : CanvasLayer
    {
        var node = Field<T>(world, layer);
        node.Reparent(scene);
        return node;
    }
}
