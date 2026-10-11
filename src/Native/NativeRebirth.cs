using System.Runtime.CompilerServices;
using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using LibreKO.Plugins;

namespace KnightOnlineUiClassic.Native;

public static partial class NativeSetup
{
    static partial void RegisterRebirth(PluginContext context) => NativeWindows.Prepare("rebirth", NativeRebirth.Prepare);
}

/// <summary>
/// The Classic rebirth window: the original Stat/Current/Add/Total columns, availability texts, and an
/// allocation confirmation that freezes the chosen points before the client sends them. The same window
/// redistributes the existing rebirth points (the client's reset mode). Requirements, the request and
/// its result stay with the client.
/// </summary>
public static class NativeRebirth
{
    public const int QualificationItem = 900579000;

    private sealed class State
    {
        public HudWindow Window = null!;
        public Label[] Totals = Array.Empty<Label>();
        public Notice? Notice;
        public int Revision;
        public string? Warning;
    }

    private static readonly ConditionalWeakTable<World, State> _states = new();

    public static Notice? Confirmation(World world) => _states.TryGetValue(world, out var state) ? state.Notice : null;

    public static void Prepare(HudWindow window, World world)
    {
        if (!NativeIdentity.Begin(window, "native_rebirth")) return;
        var bonus = Native.Get<Label[]>(world, "_rebirthBonusLbls");
        var picked = Native.Get<Label[]>(world, "_rebirthPickLbls");
        var add = Native.Get<Button[]>(world, "_rebirthAddBtns");
        var remove = Native.Get<Button[]>(world, "_rebirthRemoveBtns");
        var accept = Native.Get<Button>(world, "_rebirthBtn");
        var status = Native.Get<Label>(world, "_rebirthStatus");
        var level = Native.Get<Label>(world, "_rebirthLevelLbl");
        var points = Native.Get<Label>(world, "_rebirthPointsLbl");
        var cancel = accept?.GetParent()?.GetChildren().OfType<Button>().FirstOrDefault(b => b != accept);
        if (bonus == null || picked == null || add == null || remove == null || accept == null || status == null
            || level == null || points == null || cancel == null || bonus.Length != RebirthPick.StatCount) return;

        var state = new State { Window = window, Totals = new Label[RebirthPick.StatCount] };
        level.Name = "rebirth_level"; points.Name = "rebirth_points";
        accept.Name = "rebirth_accept"; cancel.Name = "rebirth_cancel"; status.Name = "rebirth_status";
        for (int row = 0; row < RebirthPick.StatCount; row++)
        {
            int index = row;
            var line = bonus[row].GetParent();
            if (line.GetChild(0) is Label stat) stat.Name = "rebirth_stat_" + row;
            bonus[row].Name = "rebirth_current_" + row;
            picked[row].Name = "rebirth_picked_" + row;
            remove[row].Name = "rebirth_remove_" + row;
            add[row].Name = "rebirth_add_" + row;
            var total = NativeIdentity.HudLabel(13, HorizontalAlignment.Center);
            total.Name = "rebirth_total_" + row;
            line.AddChild(total);
            state.Totals[row] = total;
            RewireStep(remove[row], () => EditPoint(world, index, false));
            RewireStep(add[row], () => EditPoint(world, index, true));
        }
        NativeIdentity.Rewire(accept, () => Pressed(world));
        _states.AddOrUpdate(world, state);
        window.VisibilityChanged += () =>
        {
            if (window.Visible) Refresh(world);
            else Dismiss(state);
        };
        window.SetMeta("classic_rebirth_controls", 1);
        NativeWindows.Sync(window, () => Refresh(world));
    }

    /// <summary>
    /// The client's step buttons add a point on press and repeat while held; the Classic step keeps that
    /// rhythm but goes through the confirmation lock.
    /// </summary>
    private static void RewireStep(BaseButton button, Action step)
    {
        foreach (var signal in new[] { BaseButton.SignalName.Pressed, BaseButton.SignalName.ButtonDown })
            foreach (var connection in button.GetSignalConnectionList(signal))
                button.Disconnect(signal, connection["callable"].AsCallable());
        HoldRepeat.Attach(button, step);
    }

    private static CharacterSheet? Sheet(World world) => Native.Get<CharacterSheet>(world, "Sheet");

    /// <summary>The client's reset mode redistributes the points already earned instead of adding a stage.</summary>
    public static bool Redistributing(World world) => Native.Get<bool>(world, "_rebirthResetMode");

    private static bool Unavailable(World world, CharacterSheet sheet) =>
        Native.Get<bool>(world, "_selfDead")
        || !Redistributing(world) && (sheet.Level < CharacterSheet.MaxLevel || sheet.RebirthLevel >= RebirthPick.MaxRebirthLevel);

    /// <summary>The fork's RefreshRebirthUI: columns, availability and the lock while a confirmation is open.</summary>
    public static void Refresh(World world)
    {
        if (!_states.TryGetValue(world, out var state) || Sheet(world) is not { } sheet
            || Native.Get<RebirthPick>(world, "_rebirthPick") is not { } pick) return;
        int level = sheet.RebirthLevel;
        bool reset = Redistributing(world);
        bool capped = !reset && level >= RebirthPick.MaxRebirthLevel;
        bool locked = Native.Get<bool>(world, "_rebirthInFlight") || state.Notice != null || Unavailable(world, sheet);
        var labels = Native.Get<Label[]>(world, "_rebirthBonusLbls")!;
        var picks = Native.Get<Label[]>(world, "_rebirthPickLbls")!;
        var add = Native.Get<Button[]>(world, "_rebirthAddBtns")!;
        var remove = Native.Get<Button[]>(world, "_rebirthRemoveBtns")!;
        Native.Get<Label>(world, "_rebirthLevelLbl")!.Text = reset ? $"Rebirth Lv {level}  ·  Redistribution"
            : capped ? $"Rebirth Lv {level} / {RebirthPick.MaxRebirthLevel}" : $"Rebirth Lv {level}  →  Lv {level + 1}";
        Native.Get<Label>(world, "_rebirthPointsLbl")!.Text = capped ? "No further bonus points are available."
            : $"Bonus points: {pick.Capacity}   Remaining: {pick.Remaining}";
        for (int row = 0; row < RebirthPick.StatCount; row++)
        {
            int current = sheet.RebirthBonusAtRow(row), placed = capped ? 0 : pick.PickedAt(row);
            labels[row].Text = $"+{current}";
            picks[row].Text = $"+{placed}";
            // Redistributing replaces the earned points with the new placement.
            state.Totals[row].Text = $"+{(reset ? placed : current + placed)}";
            add[row].Disabled = locked || !pick.CanAdd(row);
            remove[row].Disabled = locked || !pick.CanRemove(row);
        }
        var accept = Native.Get<Button>(world, "_rebirthBtn")!;
        accept.Disabled = locked || !pick.Complete;
        accept.Text = reset ? "Redistribute" : "Rebirth";
        string availability = capped ? "The maximum rebirth level has been reached."
            : sheet.Level < CharacterSheet.MaxLevel ? $"Level {CharacterSheet.MaxLevel} is required."
            : Native.Get<bool>(world, "_selfDead") ? "You cannot rebirth while dead." : "";
        var status = Native.Get<Label>(world, "_rebirthStatus")!;
        if (availability.Length > 0)
        {
            if (status.Text != availability) Native.Call(world, "SetRebirthStatus", availability, true);
            state.Warning = availability;
        }
        else if (state.Warning != null)
        {
            if (status.Text == state.Warning) Native.Call(world, "SetRebirthStatus", "", false);
            state.Warning = null;
        }
    }

    /// <summary>Adds or removes a bonus point unless the allocation is frozen.</summary>
    public static void EditPoint(World world, int row, bool add)
    {
        if (!_states.TryGetValue(world, out var state) || Sheet(world) is not { } sheet
            || Native.Get<RebirthPick>(world, "_rebirthPick") is not { } pick) return;
        if (!Native.Get<bool>(world, "_rebirthShown") || Native.Get<bool>(world, "_rebirthInFlight") || state.Notice != null
            || Unavailable(world, sheet)) return;
        if (add) pick.Add(row); else pick.Remove(row);
        Native.Call(world, "SetRebirthStatus", "", false);
        state.Warning = null;
        Native.Call(world, "RefreshRebirthUI");
        Refresh(world);
    }

    /// <summary>Opens the allocation confirmation; nothing is sent until it is accepted.</summary>
    public static void Pressed(World world)
    {
        if (!_states.TryGetValue(world, out var state) || Sheet(world) is not { } sheet
            || Native.Get<RebirthPick>(world, "_rebirthPick") is not { } pick) return;
        if (!Native.Get<bool>(world, "_rebirthShown") || Native.Get<bool>(world, "_rebirthInFlight") || state.Notice != null
            || Unavailable(world, sheet)) return;
        if (!pick.Complete)
        {
            Native.Call(world, "SetRebirthStatus", $"Place all {pick.Capacity} points first.", true);
            state.Warning = null;
            return;
        }
        int revision = ++state.Revision;
        var picks = pick.Payload();
        var layer = Native.Get<Node>(world, "_rebirthLayer") ?? world;
        state.Notice = Redistributing(world)
            ? Notice.Confirm(layer, $"Redistribute your {pick.Capacity} rebirth bonus points?\nThe new placement replaces the current one.",
                "Redistribute", "Cancel", () => Send(world, revision, picks), () => Cancel(world, revision), title: "Rebirth")
            : Notice.Confirm(layer,
                $"Your {ItemData.DisplayName(QualificationItem).Trim()} will be used.\nApply {RebirthPick.PointsPerRebirth} bonus points?\nRebirth Lv {sheet.RebirthLevel} → Lv {sheet.RebirthLevel + 1}",
                "Rebirth", "Cancel", () => Send(world, revision, picks), () => Cancel(world, revision), title: "Rebirth");
        Refresh(world);
    }

    private static void Send(World world, int revision, byte[] picks)
    {
        if (!_states.TryGetValue(world, out var state) || revision != state.Revision || !Native.Get<bool>(world, "_rebirthShown")
            || Native.Get<bool>(world, "_rebirthInFlight") || state.Notice == null) return;
        state.Notice = null;
        // The picks stayed frozen while the confirmation was open; the client sends them and handles a refused send.
        if (Sheet(world) is { } sheet && !Unavailable(world, sheet)
            && Native.Get<RebirthPick>(world, "_rebirthPick") is { } pick && pick.Payload().SequenceEqual(picks))
            Native.Call(world, "OnRebirthPressed");
        state.Warning = null;
        Refresh(world);
    }

    private static void Cancel(World world, int revision)
    {
        if (!_states.TryGetValue(world, out var state) || revision != state.Revision || Native.Get<bool>(world, "_rebirthInFlight")
            || state.Notice == null) return;
        state.Notice = null;
        Refresh(world);
    }

    private static void Dismiss(State state)
    {
        state.Revision++;
        if (state.Notice is { } notice && GodotObject.IsInstanceValid(notice)) notice.Close();
        state.Notice = null;
    }
}
