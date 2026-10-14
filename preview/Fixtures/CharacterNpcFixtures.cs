using LibreKO;
using LibreKO.Network;

/// <summary>
/// Character, quest, NPC and party fixtures. A server reply reaches both the client and the plugin's
/// native layer at runtime; these entry points do the same when an audit drives the client directly.
/// </summary>
public static partial class PreviewFixtures
{
    public static void ShowQuestView(WorldRoot world, QuestView view)
    {
        Call(world, "ShowQuestView", view);
        NativeNpc.QuestViewShown(world, view);
    }

    public static void OnQuestView(WorldRoot world, QuestView view)
    {
        Call(world, "OnQuestView", view);
        if (!view.Notification && view.Open && view.Page != QuestPageKind.Conversation) NativeNpc.QuestViewShown(world, view);
    }

    /// <summary>
    /// Adds an NPC choice the way the client does: numbered labels go in as written (the client drops the
    /// number), and a quest offer written as "[In progress] name" or "[Ready] name" passes its state as the
    /// client's state tag with the offer icon, as the client's own offer list does.
    /// </summary>
    public static void AddNpcMenuButton(WorldRoot world, string label, Action pressed)
    {
        var offer = System.Text.RegularExpressions.Regex.Match(label, @"^(?:\d+\.\s*)?\[(In progress|Ready|Completed)\]\s*(.+)$");
        if (!offer.Success) { Call(world, "AddNpcMenuButton", label, pressed, null, "", false); return; }
        string state = offer.Groups[1].Value;
        string icon = state switch { "In progress" => "system/clock", "Ready" => "system/gift", _ => "system/scroll" };
        Call(world, "AddNpcMenuButton", offer.Groups[2].Value, pressed, icon, state, state == "Ready");
    }

    public static void BuildInZoneLeaveUi(WorldRoot world)
    {
        Call(world, "BuildInZoneLeaveUi");
        NativeEvents.Prepare(world);
    }

    public static void RefreshInZoneLeaveUi(WorldRoot world)
    {
        Call(world, "RefreshInZoneLeaveUi");
        NativeEvents.Refresh(world);
    }

    public static void OnBbsList(WorldRoot world, int page, int total, List<PartyBbsEntry> entries)
    {
        Call(world, "OnBbsList", page, total, entries);
        NativeParty.Listed(world, entries);
    }
}

/// <summary>Reads the talking NPC's portrait descriptor for one audit world.</summary>
public sealed class NpcPortraitProbe(WorldRoot world)
{
    public GameNpcPortrait? NpcPortrait => NativeNpc.DialogPortrait(world);
}
