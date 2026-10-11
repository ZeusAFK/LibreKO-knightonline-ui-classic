using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Plugins;
using KnightOnlineUiClassic;
using KnightOnlineUiClassic.Windows;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;

public partial class Preview
{
    /// <summary>
    /// command-function-audit: every command of the Classic command list, run from the list in a full native
    /// World (all facets, the real Classic plugin and chat) over an isolated loopback session. Each row is
    /// activated the way a double click does; the audit records the requests the client sends, the messages it
    /// prints, the windows and states it changes, and the chat prefill the Classic chat shows. Prefilled commands
    /// are then completed with arguments and submitted through the Classic chat input. The character has no
    /// target, party or clan, so context commands are expected to refuse or do nothing; the JSON lists each result.
    /// </summary>
    private async Task CaptureCommandFunctionAudit(int nation, PluginInfo info)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        string output = OS.GetEnvironment("LIBREKO_AUDIT_OUTPUT_DIR"); Directory.CreateDirectory(output);
        string prefix = nation == 1 ? "karus" : "human";
        foreach (string pack in new[] { "knightonline.pck", "content/characters.pck", "content/armor.pck", "content/weapons.pck", "content/npcs.pck" })
            if (!ProjectSettings.LoadResourcePack(ProjectSettings.GlobalizePath("res://../../build/client/source-content/" + pack), false)) throw new Exception("Missing existing pack " + pack);
        GetWindow().ContentScaleMode = Window.ContentScaleModeEnum.Disabled; GetWindow().Size = new Vector2I(1200, 850);
        GetViewport().GuiEmbedSubwindows = true;
        new Plugin().Initialize((PluginContext)Create(typeof(PluginContext), info, new List<PluginInfo> { info }, PluginHost.Ui, PluginHost.Game));
        var original = Net.I; var net = new Net(); GetTree().Root.AddChild(net);
        var originalLogin = LoginNet.I; var loginNet = new LoginNet(); GetTree().Root.AddChild(loginNet);
        loginNet.Login("offline-audit", "offline-audit");
        var project = new ConfigFile(); project.Load(ProjectSettings.GlobalizePath("res://../../LibreKO/Client/project.godot"));
        foreach (string action in project.GetSectionKeys("input")) if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        typeof(Config).GetProperty(nameof(Config.PingEnabled))!.SetValue(null, false);
        var template = new PetReconnectWorld { Name = "World" };
        template.AddChild(new Node3D { Name = "Entities" });
        template.AddChild(new Camera3D { Name = "Camera", Current = true });
        template.AddChild(new MeshInstance3D { Name = "Ground", Mesh = new PlaneMesh { Size = new Vector2(400, 400) } });
        foreach (var child in template.GetChildren()) child.Owner = template;
        var worldScene = new PackedScene(); worldScene.Pack(template); template.Free();
        worldScene.TakeOverPath("res://scenes/World.tscn");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static;
        var checks = new List<string>();
        void Require(bool ok, string message) { if (!ok) throw new Exception("COMMAND_FUNCTION_AUDIT: " + message); checks.Add(message); }
        async Task Frames(int count = 4) { for (int i = 0; i < count; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
        async Task Until(Func<bool> condition, string message)
        {
            ulong deadline = Time.GetTicksMsec() + 60000;
            while (!condition() && Time.GetTicksMsec() < deadline) await Frames(1);
            Require(condition(), message);
        }
        Type TypeOf(object owner) => owner is World ? typeof(World) : owner.GetType();
        object? Get(object owner, string field) => TypeOf(owner).GetField(field, flags)?.GetValue(owner) ?? TypeOf(owner).GetProperty(field, flags)?.GetValue(owner);
        void Set(object owner, string field, object? value) => TypeOf(owner).GetField(field, flags)!.SetValue(owner, value);
        async Task Send(NetworkStream stream, Packet packet)
        {
            byte[] body = packet.GetBytes(); byte[] frame = new byte[body.Length + 6]; frame[0] = 0xaa; frame[1] = 0x55;
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), (ushort)body.Length); body.CopyTo(frame, 4); frame[^2] = 0x55; frame[^1] = 0xaa;
            await stream.WriteAsync(frame);
        }
        async Task<byte[]> ReadFrame(NetworkStream stream, CancellationToken token)
        {
            byte[] header = new byte[4]; await stream.ReadExactlyAsync(header, token);
            int length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2));
            byte[] body = new byte[length + 2]; await stream.ReadExactlyAsync(body, token);
            return body;
        }
        async Task Read(NetworkStream stream, GameOpcodes expected)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            for (int i = 0; i < 100; i++) if ((await ReadFrame(stream, timeout.Token))[0] == (byte)expected) return;
            throw new Exception("Missing native request " + expected);
        }
        void Record(Packet p) { p.WriteInt(0); p.WriteShort(0); p.WriteShort(0); p.WriteByte(0); p.WriteShort(0); p.WriteInt(0); p.WriteInt(0); }
        Packet MyInfoPacket()
        {
            var p = new Packet(GameOpcodes.GS_MYINFO); p.WriteInt(42); p.WriteSByteString("CommandTester");
            p.WriteShort(100); p.WriteShort(100); p.WriteShort(0); p.WriteByte((byte)nation); p.WriteByte((byte)(nation == 1 ? 1 : 11));
            p.WriteShort((short)(nation * 100 + 6)); p.WriteByte(0); p.WriteInt(0);
            for (int i = 0; i < 4; i++) p.WriteByte(0);
            p.WriteByte(60); p.WriteShort(0); p.WriteLong(1000000); p.WriteLong(10000); p.WriteInt(0); p.WriteInt(0);
            p.WriteShort(0); p.WriteByte(0); p.WriteLong(0); p.WriteUShort(0); p.WriteInt(0); p.WriteLong(0);
            p.WriteShort(1000); p.WriteShort(1000); p.WriteShort(1000); p.WriteShort(1000); p.WriteInt(17000); p.WriteInt(0);
            for (int i = 0; i < 5; i++) { p.WriteByte(100); p.WriteByte(0); }
            p.WriteShort(100); p.WriteShort(100); for (int i = 0; i < 6; i++) p.WriteByte(0);
            p.WriteInt(100000); p.WriteByte(1); p.WriteByte(255); p.WriteByte(255); for (int i = 0; i < 9; i++) p.WriteByte(0);
            for (int i = 0; i < InventoryConstants.MyInfoWireTotal; i++) Record(p);
            return p;
        }

        var results = new List<Dictionary<string, object?>>();
        var sent = new ConcurrentQueue<string>();
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        net.LoginResultEvent += (ok, _) => { if (ok) net.SelectChar("CommandTester"); };
        net.EnterWorldEvent += _ => GetTree().ChangeSceneToFile("res://scenes/World.tscn");
        GetTree().CurrentScene = null;
        using var stop = new CancellationTokenSource();
        try
        {
            net.BeginGameLogin("127.0.0.1", port, "offline-audit", "offline-audit");
            using var peer = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(30)); var stream = peer.GetStream();
            await Read(stream, GameOpcodes.GS_VERSION_CHECK);
            var version = new Packet(GameOpcodes.GS_VERSION_CHECK); version.WriteByte(1); version.WriteUShort(1298); await Send(stream, version);
            await Read(stream, GameOpcodes.GS_LOGIN); var login = new Packet(GameOpcodes.GS_LOGIN); login.WriteByte((byte)nation); await Send(stream, login);
            await Read(stream, GameOpcodes.GS_SELECT_CHARACTER); var select = new Packet(GameOpcodes.GS_SELECT_CHARACTER); select.WriteByte(1); select.WriteShort(32700); select.WriteShort(100); select.WriteShort(100); select.WriteShort(0); await Send(stream, select);
            await Read(stream, GameOpcodes.GS_GAMESTART); await Send(stream, MyInfoPacket());
            await Until(() => GetTree().CurrentScene is World w && (bool)Get(w, "_worldReady")!, "Native World scene builds every facet");
            var world = (World)GetTree().CurrentScene;
            await Until(() => Get(world, "_loadingLayer") == null, "Native loading screen completes");
            // Every request the client sends from here on is recorded by opcode.
            _ = Task.Run(async () =>
            {
                try { while (!stop.IsCancellationRequested) { var body = await ReadFrame(stream, stop.Token); sent.Enqueue(((GameOpcodes)body[0]).ToString()); } }
                catch (Exception) { }
            });
            await Frames(10);
            await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
            var background = new HashSet<string>(); while (sent.TryDequeue(out var idle)) background.Add(idle);

            DetailCall(world, "ToggleCommandList"); await Frames(6);
            var window = (HudWindow)Get(world, "_cmdWindow")!;
            var frame = window.GetChildren().OfType<ClassicCommandListFrame>().Single();
            await Until(() => frame.Bound, "The game's command window is the Classic window, bound to the client's list");
            Require(window.Visible && window.IsVisibleInTree(), "The command list key action opens the Classic window");
            GetViewport().PushInput(new InputEventMouseMotion { Position = new Vector2(1190, 10), GlobalPosition = new Vector2(1190, 10) }, true);
            await Frames();
            GetViewport().GetTexture().GetImage().SavePng(output + "/" + prefix + "-command-world.png");

            var chat = Get(world, "Chat")!;
            var classicChat = Descendants(world).OfType<ChatWindow>().Single();
            var classicInput = (LineEdit)typeof(ChatWindow).GetField("_input", flags)!.GetValue(classicChat)!;
            var log = (Queue<string>)Get(world, "_combatLogLines")!;
            object exitIdle = Get(world, "_battleTimer")!;
            string[] stateFields = { "_running", "_sitRequestPending", "_achShown", "_merchantMenuShown", "_seekShown", "_nationChatShown", "_marketPriceShown",
                "_tradeRequestsBlocked", "_partyRequestsBlocked", "_exitArmed", "_vipWhShown" };
            Dictionary<string, string> State() => stateFields.Where(f => typeof(World).GetField(f, flags) != null).ToDictionary(f => f, f => Get(world, f)?.ToString() ?? "");
            List<Node> Dialogs() => Descendants(GetTree().Root).Where(n => n is KnightOnlineUiClassic.Windows.MessageBox || n is AcceptDialog).Where(n => n is not CanvasItem c || c.IsVisibleInTree()).ToList();
            string Clean(string line) => System.Text.RegularExpressions.Regex.Replace(line, @"\[/?[a-z]+(=[^\]]*)?\]", "");

            async Task<Dictionary<string, object?>> Observe(string label, Func<Task> act)
            {
                log.Clear(); while (sent.TryDequeue(out _)) { }
                var before = State(); var dialogs = Dialogs();
                string? error = null;
                try { await act(); } catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }
                await Frames(8); await ToSignal(GetTree().CreateTimer(.4), SceneTreeTimer.SignalName.Timeout); await Frames(2);
                var packets = new List<string>(); while (sent.TryDequeue(out var op)) packets.Add(op);
                var after = State();
                var changed = after.Where(kv => before[kv.Key] != kv.Value).Select(kv => kv.Key + "=" + kv.Value).ToList();
                var opened = Dialogs().Except(dialogs).ToList();
                string dialogText = string.Join(" | ", opened.SelectMany(d => Descendants(d).OfType<Label>().Select(l => l.Text).Where(t => t.Length > 0)).Distinct());
                foreach (var dialog in opened) if (dialog is MessageBox box) box.QueueFree(); else if (dialog is AcceptDialog accept) accept.Hide();
                return new Dictionary<string, object?>
                {
                    ["run"] = label, ["requests"] = packets, ["messages"] = log.Select(Clean).ToList(), ["state"] = changed,
                    ["chat"] = classicInput.Text, ["dialog"] = dialogText, ["error"] = error,
                };
            }

            var model = frame.Model;
            var prefills = new List<(string Id, string Text)>();
            for (int g = 0; g < model.Count; g++)
            {
                frame.GroupList.Select(g); frame.GroupList.EmitSignal(ItemList.SignalName.ItemSelected, (long)g); await Frames(2);
                var entries = frame.CommandList!;
                for (int i = 0; i < model[g].Rows.Count; i++)
                {
                    var row = model[g].Rows[i];
                    Require(entries.GetItemText(i) == row.Text, model[g].Title + ": row " + i + " is " + row.Text);
                    if (row.Id == "Exit") Set(world, "_battleTimer", 0.0);
                    var result = await Observe("list", () => { KnightOnlineUiClassic.Native.NativeCommandList.Activate(world, i); return Task.CompletedTask; });
                    result["group"] = model[g].Title; result["id"] = row.Id; result["text"] = row.Text; result["available"] = row.Available;
                    bool prefill = row.Entry.GetType().GetProperty("Prefill")?.GetValue(row.Entry) is true;
                    result["prefill"] = prefill;
                    string chatText = classicInput.Text;
                    if (prefill && row.Available)
                    {
                        Require(chatText == "/" + row.Text + " ", row.Text + ": the Classic chat input shows the prefill '" + chatText + "'");
                        prefills.Add((row.Id, row.Text));
                    }
                    if (!row.Available) Require(((List<string>)result["requests"]!).Count == 0 && chatText.Length == 0 && ((List<string>)result["state"]!).Count == 0, row.Text + ": an unavailable command does nothing");
                    // Leave the world as it was for the next row.
                    classicInput.Clear(); classicInput.ReleaseFocus();
                    Native.TryCall(chat, "Close", out _);
                    if (row.Id == "Exit") { Set(world, "_exitArmed", Enum.ToObject(typeof(World).GetNestedType("ExitKind", flags)!, 0)); Set(world, "_battleTimer", exitIdle); }
                    foreach (var (field, close) in new[] { ("_achShown", "CloseAchievements"), ("_merchantMenuShown", "CloseMerchantMenu"), ("_seekShown", "ToggleSeekParty"), ("_nationChatShown", "CloseNationChat") })
                        if (Get(world, field) is true) DetailCall(world, close);
                    results.Add(result);
                    await Frames(2);
                }
            }
            // Prefilled commands completed with arguments and submitted from the Classic chat input.
            var arguments = new Dictionary<string, string>
            {
                ["Pm"] = "Tester hello", ["AddFriend"] = "Tester", ["BanFromClan"] = "Tester", ["AppointVice"] = "Tester", ["ClanChat"] = "hello clan",
                ["ClanNotice"] = "Siege tonight", ["RoyalOrder"] = "Gather at the castle", ["Prize"] = "Tester 100", ["ExperiencePoint"] = "10",
                ["DropRate"] = "10", ["Rain"] = "1", ["Clear"] = "1",
            };
            foreach (var (id, word) in prefills)
            {
                string line = "/" + word + " " + (arguments.TryGetValue(id, out var args) ? args : "Tester");
                var result = await Observe("chat", () =>
                {
                    classicInput.GrabFocus(); classicInput.Text = line;
                    classicInput.EmitSignal(LineEdit.SignalName.TextSubmitted, line);
                    return Task.CompletedTask;
                });
                result["id"] = id; result["text"] = line;
                results.Add(result);
                classicInput.Clear(); Native.TryCall(chat, "Close", out _);
                foreach (var (field, close) in new[] { ("_achShown", "CloseAchievements"), ("_merchantMenuShown", "CloseMerchantMenu"), ("_seekShown", "ToggleSeekParty"), ("_nationChatShown", "CloseNationChat") })
                    if (Get(world, field) is true) DetailCall(world, close);
            }
            // Context commands with a name typed after them (the client's own argument forms).
            foreach (string line in new[] { "Party Tester", "Individual_Battle Tester" })
            {
                var result = await Observe("chat", () => { classicInput.GrabFocus(); classicInput.Text = "/" + line; classicInput.EmitSignal(LineEdit.SignalName.TextSubmitted, "/" + line); return Task.CompletedTask; });
                result["id"] = line.Split(' ')[0]; result["text"] = "/" + line;
                results.Add(result);
                classicInput.Clear(); Native.TryCall(chat, "Close", out _);
            }

            Require(results.All(r => r["error"] == null), "No command throws: " + string.Join(", ", results.Where(r => r["error"] != null).Select(r => r["text"] + " " + r["error"])));
            Require(window.IsInsideTree(), "The command window survives every command");
            File.WriteAllText(output + "/" + prefix + "-command-functions.json", JsonSerializer.Serialize(new { checks, background, results, transport = "isolated loopback fixture; no target, party or clan; no premium" },
                new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            GD.Print("COMMAND_FUNCTION_AUDIT_OK " + prefix + " " + checks.Count + " checks / " + results.Count + " runs");
        }
        finally
        {
            stop.Cancel();
            net.AutoReconnect = false; net.Disconnect(expected: true);
            if (GetTree().CurrentScene is World world) { GetTree().CurrentScene = null; world.QueueFree(); await Frames(); }
            GetTree().CurrentScene = this; net.Free(); loginNet.Free(); typeof(Net).GetProperty("I")!.SetValue(null, original);
            typeof(LoginNet).GetProperty("I")!.SetValue(null, originalLogin);
        }
    }
}
