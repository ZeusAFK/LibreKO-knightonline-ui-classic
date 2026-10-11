using System.Text.Json;
using Godot;
using KnightOnlineUiClassic.Layout;

namespace KnightOnlineUiClassic;

/// <summary>
/// Optional art that stays on the player's machine and is never distributed with the plugin: layouts and textures
/// in <c>assets-local/</c> next to the installed plugin (or the folder named by
/// <see cref="RootVariable"/>). Windows that find their local layout use it; otherwise they keep the bundled design.
/// </summary>
public sealed class LocalAssets
{
    public const string Folder = "assets-local";
    public const string RootVariable = "KNIGHTONLINE_UI_CLASSIC_LOCAL_ASSETS";
    private readonly Dictionary<string, LayoutNode?> _layouts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D?> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Image?> _images = new(StringComparer.OrdinalIgnoreCase);
    public string Root { get; }

    public LocalAssets(string pluginRoot)
    {
        string? overridden = System.Environment.GetEnvironmentVariable(RootVariable);
        Root = Path.GetFullPath(string.IsNullOrWhiteSpace(overridden) ? Path.Combine(pluginRoot, Folder) : overridden);
    }

    /// <summary>The local layout, or null when it (or one of its textures) is missing.</summary>
    public LayoutNode? Layout(string name)
    {
        if (_layouts.TryGetValue(name, out var cached)) return cached;
        LayoutNode? node = null;
        string path = Path.Combine(Root, "layouts", name + ".json");
        try
        {
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
                node = LayoutNode.Parse(doc.RootElement);
                if (node.All().Any(n => n.Texture != null && !File.Exists(TexturePath(n.Texture)))) node = null;
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { Plugin.Kit.Log.Warn($"local layout {name}: {e.Message}"); node = null; }
        return _layouts[name] = node;
    }

    public Texture2D? Texture(string png)
    {
        if (_textures.TryGetValue(png, out var cached)) return cached;
        var image = Image(png);
        return _textures[png] = image == null ? null : ImageTexture.CreateFromImage(image);
    }

    public Image? Image(string png)
    {
        if (_images.TryGetValue(png, out var cached)) return cached;
        Image? image = null;
        string path = TexturePath(png);
        if (File.Exists(path))
        {
            image = new Image();
            if (image.LoadPngFromBuffer(File.ReadAllBytes(path)) != Error.Ok) image = null;
        }
        return _images[png] = image;
    }

    private string TexturePath(string png) => Path.Combine(Root, "textures", Path.GetFileName(png));
}
