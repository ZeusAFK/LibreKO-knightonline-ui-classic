using Godot;
using System.Reflection;
using System.Text.Json;
using KnightOnlineUiClassic;
using KnightOnlineUiClassic.Layout;
using KnightOnlineUiClassic.Windows;

// Checks for the frame-time changes that must not change what is drawn (mode perf-classic-audit):
// - the shown-only gate: a hidden node does not process, a shown one is synced before its first frame draws, for a
//   window shown by its own visibility, by its canvas layer, during input or after the node processed in that frame;
// - the combat log's append path: after 260 appended lines (some in one frame) it holds the same text and draws the
//   same pixels as one full set of the same lines, follows the end, and keeps a scrolled-up position;
// - the minimap's disc mesh draws the same pixels as DrawCircle for the blip radii and sub-pixel positions.
public partial class Preview
{
    private async Task CapturePerfClassicAudit(int nation)
    {
        var output = OS.GetEnvironment("LIBREKO_AUDIT_OUTPUT_DIR"); System.IO.Directory.CreateDirectory(output);
        string prefix = nation == 1 ? "karus" : "human";
        var window = GetWindow(); window.ContentScaleMode = Window.ContentScaleModeEnum.Disabled;
        window.Size = new Vector2I(1000, 700); window.ContentScaleSize = Vector2I.Zero;
        var checks = new List<string>(); var failures = new List<string>(); var data = new Dictionary<string, object>();
        void Require(bool value, string text) { if (!value) { failures.Add(text); GD.PushError("PERF_CLASSIC_AUDIT: " + text); } else checks.Add(text); }
        async Task Frames(int count = 4) { for (int i = 0; i < count; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
        Image Capture() => GetViewport().GetTexture().GetImage();
        int Differing(Image image, Rect2I a, Rect2I b)
        {
            int count = 0;
            for (int y = 0; y < a.Size.Y; y++)
                for (int x = 0; x < a.Size.X; x++)
                    if (image.GetPixel(a.Position.X + x, a.Position.Y + y) != image.GetPixel(b.Position.X + x, b.Position.Y + y)) count++;
            return count;
        }
        AddChild(new ColorRect { Color = new Color("34362b"), Size = new Vector2(1000, 700), MouseFilter = MouseFilterEnum.Ignore });

        // ---- Shown-only gate -----------------------------------------------------------------------------------
        {
            var layer = new CanvasLayer { Layer = 5 }; AddChild(layer);
            var host = new Control { Size = new Vector2(100, 100) }; layer.AddChild(host);
            var probe = new GateProbe { Size = new Vector2(10, 10) }; host.AddChild(probe);
            host.Visible = false;
            await Frames(2);
            int processed = probe.Processed, synced = probe.Synced;
            await Frames(3);
            Require(!probe.IsProcessing() && probe.Processed == processed && probe.Synced == synced, "gate: a hidden node does not process or sync");
            // Shown during input-like code: synced before the frame draws.
            bool syncedBeforeDraw = false;
            void PreDraw() { if (probe.IsVisibleInTree() && probe.Synced > synced) syncedBeforeDraw = true; }
            RenderingServer.FramePreDraw += PreDraw;
            host.Visible = true;
            await Frames(1);
            Require(probe.IsProcessing() && syncedBeforeDraw, "gate: shown by its own visibility, it processes and is synced before the first drawn frame");
            RenderingServer.FramePreDraw -= PreDraw;
            // Shown by its canvas layer.
            layer.Visible = false; await Frames(2);
            Require(!probe.IsProcessing(), "gate: hiding the canvas layer stops processing");
            synced = probe.Synced; syncedBeforeDraw = false;
            RenderingServer.FramePreDraw += PreDraw;
            layer.Visible = true; await Frames(1);
            RenderingServer.FramePreDraw -= PreDraw;
            Require(probe.IsProcessing() && syncedBeforeDraw, "gate: shown by its canvas layer, it is synced before the first drawn frame");
            // Shown by a node that processes after the probe in the same frame.
            host.Visible = false; await Frames(2);
            var late = new LateShow(host) { ProcessPriority = int.MaxValue }; AddChild(late);
            synced = probe.Synced; syncedBeforeDraw = false;
            RenderingServer.FramePreDraw += PreDraw;
            late.Armed = true; await Frames(1);
            RenderingServer.FramePreDraw -= PreDraw;
            Require(probe.IsProcessing() && syncedBeforeDraw && probe.SyncedAfterShow, "gate: shown by a node that processes after it, it is synced before the frame draws");
            // Moved under a hidden parent.
            var hidden = new Control { Visible = false }; layer.AddChild(hidden);
            probe.Reparent(hidden); await Frames(2);
            Require(!probe.IsProcessing(), "gate: moved under a hidden parent, it stops processing");
            probe.Reparent(host); await Frames(2);
            Require(probe.IsProcessing(), "gate: moved back under a shown parent, it processes again");
            late.QueueFree(); layer.QueueFree();
            await Frames(2);
        }

        // ---- Combat log append path ------------------------------------------------------------------------------
        {
            var rng = new RandomNumberGenerator { Seed = 1298 };
            string[] inks = { "ffffff", "80cfff", "ffff00", "a8ffb0", "ff6a5a" };
            string[] words = { "Beginning", "attack", "on", "Worm.", "You", "recovered", "42", "MP.", "Water", "of", "Ibex", "succeeded", "[lb]Guard[lb]", "critical", "damage", "1,234" };
            string Line(int i)
            {
                int count = rng.RandiRange(2, 22);
                var text = string.Join(" ", Enumerable.Range(0, count).Select(_ => words[rng.RandiRange(0, words.Length - 1)]));
                return $"[color=#{inks[i % inks.Length]}]{i}: {text}[/color]";
            }
            var lines = Enumerable.Range(0, 260).Select(Line).ToList();
            var appended = new LogText(13, Colors.White, Plugin.Kit.ChatFont) { Position = new Vector2(10, 10), Size = new Vector2(420, 300) };
            var full = new LogText(13, Colors.White, Plugin.Kit.ChatFont) { Position = new Vector2(450, 10), Size = new Vector2(420, 300) };
            AddChild(appended); AddChild(full);
            await Frames(2);
            int at = 0;
            while (at < lines.Count)
            {
                int batch = Math.Min(lines.Count - at, rng.RandiRange(1, 5));
                for (int i = 0; i < batch; i++) appended.Append(lines[at++]);
                await Frames(1);
            }
            full.Set(lines);
            await Frames(6);
            var a = Descendants(appended).OfType<RichTextLabel>().Single();
            var b = Descendants(full).OfType<RichTextLabel>().Single();
            Require(a.GetParsedText() == b.GetParsedText(), "log: 260 appended lines hold the same text as one full set of the kept 200");
            Require(a.GetParagraphCount() == b.GetParagraphCount() && a.GetLineCount() == b.GetLineCount() && a.GetContentHeight() == b.GetContentHeight(),
                $"log: same paragraphs, wrapped lines and height ({a.GetParagraphCount()}/{a.GetLineCount()}/{a.GetContentHeight()} vs {b.GetParagraphCount()}/{b.GetLineCount()}/{b.GetContentHeight()})");
            var image = Capture();
            image.SavePng(System.IO.Path.Combine(output, prefix + "-log-append-vs-set.png"));
            int differing = Differing(image, new Rect2I(10, 10, 420, 300), new Rect2I(450, 10, 420, 300));
            Require(differing == 0, $"log: the appended log draws the same pixels as the full set ({differing} differ)");
            data["logDifferingPixels"] = differing;
            var bar = Descendants(appended).OfType<ScrollContainer>().Single().GetVScrollBar();
            Require(bar.Value + bar.Page >= bar.MaxValue - 2, "log: appending at the end keeps following the end");
            // Scrolled up: further lines keep the position.
            bar.Value = Math.Max(0, bar.Value - 120); await Frames(2);
            double held = bar.Value;
            for (int i = 0; i < 12; i++) { appended.Append(Line(1000 + i)); if (i % 4 == 3) await Frames(1); }
            await Frames(4);
            Require(Math.Abs(bar.Value - held) < 1 || bar.Value < held, $"log: a scrolled-up position is kept while lines arrive ({held} -> {bar.Value})");
            Require(!(bar.Value + bar.Page >= bar.MaxValue - 2), "log: a scrolled-up log does not jump to the end");
            bar.Value = bar.MaxValue; await Frames(2);
            appended.Append(Line(2000)); await Frames(4);
            Require(bar.Value + bar.Page >= bar.MaxValue - 2, "log: scrolled back to the end, it follows again");
            // A line of two paragraphs takes the full set; the text stays the same as a full set.
            appended.Append("[color=#ffff00]first part\nsecond part[/color]"); await Frames(4);
            var kept = ((System.Collections.Generic.IEnumerable<string>)typeof(LogText).GetField("_lines", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(appended)!).ToList();
            full.Set(kept); await Frames(4);
            Require(a.GetParsedText() == b.GetParsedText(), "log: after a two-paragraph line the text equals a full set");
            appended.QueueFree(); full.QueueFree();
            await Frames(2);
        }

        // ---- Minimap disc mesh ----------------------------------------------------------------------------------
        {
            var disc = (ArrayMesh)typeof(MapCanvas).GetMethod("Disc", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
            var rng = new RandomNumberGenerator { Seed = 1299 };
            var blips = Enumerable.Range(0, 240).Select(_ => (P: new Vector2(rng.RandfRange(4, 196), rng.RandfRange(4, 196)), R: (float)rng.RandiRange(2, 6) + (rng.Randf() < .3f ? .5f : 0f),
                C: new Color(rng.Randf(), rng.Randf(), rng.Randf(), rng.Randf() < .2f ? .6f : 1f))).ToList();
            var circles = new DrawProbe(c => { foreach (var b in blips) c.DrawCircle(b.P, b.R, b.C); }) { Position = new Vector2(10, 350), Size = new Vector2(200, 200) };
            var meshes = new DrawProbe(c => { foreach (var b in blips) c.DrawMesh(disc, null, new Transform2D(0f, new Vector2(b.R, b.R), 0f, b.P), b.C); }) { Position = new Vector2(260, 350), Size = new Vector2(200, 200) };
            var bgA = new ColorRect { Color = new Color(0.03f, 0.035f, 0.045f), Position = circles.Position, Size = circles.Size };
            var bgB = new ColorRect { Color = new Color(0.03f, 0.035f, 0.045f), Position = meshes.Position, Size = meshes.Size };
            AddChild(bgA); AddChild(bgB); AddChild(circles); AddChild(meshes);
            await Frames(4);
            var image = Capture();
            image.SavePng(System.IO.Path.Combine(output, prefix + "-minimap-disc-vs-circle.png"));
            int differing = Differing(image, new Rect2I(10, 350, 200, 200), new Rect2I(260, 350, 200, 200));
            data["discDifferingPixels"] = differing;
            Require(differing == 0, $"minimap: the disc mesh draws the same pixels as DrawCircle ({differing} of 40000 differ)");
            // The same at the 200% UI scale (the canvas scaled by its parent).
            var scaled = new Control { Scale = new Vector2(2, 2), Position = new Vector2(500, 300) }; AddChild(scaled);
            foreach (var node in new Control[] { bgA, bgB, circles, meshes }) node.Reparent(scaled, false);
            bgA.Position = circles.Position = Vector2.Zero; bgB.Position = meshes.Position = new Vector2(100, 0);
            circles.Size = meshes.Size = bgA.Size = bgB.Size = new Vector2(100, 200);
            await Frames(4);
            image = Capture();
            image.SavePng(System.IO.Path.Combine(output, prefix + "-minimap-disc-vs-circle-200.png"));
            differing = Differing(image, new Rect2I(500, 300, 200, 400), new Rect2I(700, 300, 200, 400));
            data["discDifferingPixels200"] = differing;
            Require(differing == 0, $"minimap at 200%: the disc mesh draws the same pixels as DrawCircle ({differing} of 80000 differ)");
            scaled.QueueFree();
        }

        var result = new { checks, failures, data };
        System.IO.File.WriteAllText(System.IO.Path.Combine(output, prefix + "-perf-classic-audit.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print($"PERF_CLASSIC_AUDIT {(failures.Count == 0 ? "OK" : "FAILED")}: {checks.Count} checks, {failures.Count} failures");
        if (failures.Count > 0) GD.PushError("PERF_CLASSIC_AUDIT failed: " + string.Join(" | ", failures));
    }

    /// <summary>A gated probe that counts its processing and its syncs.</summary>
    private sealed partial class GateProbe : Control
    {
        public int Processed, Synced;
        public bool SyncedAfterShow;
        public GateProbe() { ShownOnly.Gate(this, Sync); }
        private void Sync() { Synced++; if (LateShow.ShownThisFrame) SyncedAfterShow = true; }
        public override void _Process(double delta) { Processed++; Sync(); }
    }

    /// <summary>Shows a control from its own _Process, after every other node processed.</summary>
    private sealed partial class LateShow : Node
    {
        public static bool ShownThisFrame;
        private readonly Control _target;
        public bool Armed;
        public LateShow() { _target = null!; }
        public LateShow(Control target) => _target = target;
        public override void _Process(double delta)
        {
            ShownThisFrame = false;
            if (!Armed) return;
            Armed = false; ShownThisFrame = true; _target.Visible = true;
        }
    }

    private sealed partial class DrawProbe : Control
    {
        private readonly Action<CanvasItem> _draw;
        public DrawProbe() { _draw = _ => { }; }
        public DrawProbe(Action<CanvasItem> draw) { _draw = draw; ClipContents = true; MouseFilter = MouseFilterEnum.Ignore; }
        public override void _Draw() => _draw(this);
    }
}
