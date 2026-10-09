using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;
using KnightOnlineUiClassic.Native;

/// <summary>
/// Achievements, attendance, rankings and genie fixtures. Each builds the client's own window through its
/// native initializer, feeds it the server replies a live session would receive, then runs this group's
/// preparer as the client's window flow would before the skin is applied.
/// </summary>
public static partial class PreviewFixtures
{
    private static HudWindow DetachWindow(World world, string layerField, string windowField)
    {
        var layer = Field<CanvasLayer>(world, layerField);
        var window = Field<HudWindow>(world, windowField);
        layer.RemoveChild(window);
        NativeProgression.Prepare(window, world);
        return window;
    }

    /// <summary>Real achievement ids from the client's definitions, grouped by tab.</summary>
    public static List<int> AchievementIds(AchievementTab tab) =>
        AchievementData.Ids.Where(id => AchievementData.Get(id)?.Tab == tab).OrderBy(id => id).ToList();

    public static HudWindow BuildAchievementsUiPreview(World world, string view)
    {
        ItemData.EnsureLoaded();
        Call(world, "AchievementInit");
        Call(world, "ToggleAchievements");
        var entries = new List<AchievementEntry>();
        if (view != "empty")
        {
            foreach (var tab in AchievementData.Tabs)
            {
                var ids = AchievementIds(tab);
                for (int i = 0; i < ids.Count; i++)
                {
                    int state = i % 5 == 0 ? Net.AchievementStateAchieved : i % 5 == 1 ? Net.AchievementStateClaimed : 0;
                    entries.Add(new AchievementEntry { Id = ids[i], State = state, Progress = state != 0 ? 10 : i % 10, Target = 10 });
                }
            }
            Call(world, "OnAchievementList", entries);
            var recent = entries.Where(e => e.Claimed || e.Claimable).Take(3).Select(e => e.Id).ToArray();
            Call(world, "OnAchievementSummary", new AchievementSummary
            {
                PlayMinutes = 9_754, MonstersDefeated = 48_211, PlayersDefeated = 1_337, Deaths = 212, Points = 2_450,
                RecentlyAchieved = recent, AchievedPerTab = new int[AchievementData.Tabs.Length],
            });
        }
        if (view is "list" or "claimed") Call(world, "SelectAchievementTab", AchievementTab.War);
        if (view == "claimed") Field<CheckButton>(world, "_achHideClaimed").ButtonPressed = true;
        return DetachWindow(world, "_achLayer", "_achPanel");
    }

    public static HudWindow BuildAttendanceUiPreview(World world, string view)
    {
        ItemData.EnsureLoaded();
        Call(world, "AttendanceInit");
        Net.I.AttendanceAutoOpened = true;
        int total = Net.AttendanceDailySlots + Net.AttendanceBonusSlots;
        var slots = new int[total]; var states = new byte[total];
        int attended = view switch { "empty" => 0, "complete" => Net.AttendanceDailySlots, _ => 12 };
        for (int i = 0; i < Net.AttendanceDailySlots; i++)
        {
            slots[i] = i + 1;
            states[i] = i < attended - 1 ? Net.AttendanceStateClaimed
                : i == attended - 1 ? (view == "complete" ? Net.AttendanceStateClaimed : Net.AttendanceStateClaimable)
                : i == attended ? Net.AttendanceStateClaimableExtra : (byte)0;
            if (view == "empty") states[i] = i == 0 ? Net.AttendanceStateClaimable : (byte)0;
        }
        for (int i = 0; i < Net.AttendanceBonusSlots; i++)
        {
            int index = Net.AttendanceDailySlots + i;
            slots[index] = Net.AttendanceBonusFirstSlot + i;
            states[index] = view == "complete" ? Net.AttendanceStateClaimed : i == 0 && view != "empty" ? Net.AttendanceStateClaimable : (byte)0;
        }
        Call(world, "OnAttendanceBoard", slots, states);
        Call(world, "OpenAttendance");
        if (view == "failed") Call(world, "OnAttendanceFailed", Net.EventBoardAttendanceClaim, Net.AttendanceClaimInventoryFull);
        return DetachWindow(world, "_attendanceLayer", "_attendancePanel");
    }

    public static List<RankEntry> RankRows(int nation, int count)
    {
        string[] names = { "Rikka", "LongCharacterName1299", "Zeus", "Ariel", "Marduk", "Ranger", "Guardian", "Magician", "Warrior", "Priestess" };
        string[] clans = { "canCLAN", "", "TheVeryLongClanNameOfAll", "Moradon", "Guardians" };
        return Enumerable.Range(0, count).Select(i => new RankEntry
        {
            Name = names[i % names.Length] + (i >= names.Length ? (i / names.Length).ToString() : ""), Nation = nation,
            ClanName = clans[i % clans.Length], Loyalty = 25_000 - i * 731, Place = i + 1,
        }).ToList();
    }

    public static HudWindow BuildRankUiPreview(World world, string view)
    {
        Call(world, "RankInit");
        Call(world, "OpenRank");
        if (view != "loading")
        {
            // The player's own nation carries the requested list; the other nation always has a short one.
            int own = view == "empty" ? 0 : view == "short" ? 4 : 30, other = view == "empty" ? 0 : 7;
            bool karus = Net.I.LastEnter.Nation == Nations.Karus;
            Call(world, "OnRankData", RankRows(1, karus ? own : other), RankRows(2, karus ? other : own), view == "empty" ? 0 : 6, 21_344);
        }
        return DetachWindow(world, "_rankLayer", "_rankPanel");
    }

    public static HudWindow BuildGenieUiPreview(World world, string view)
    {
        ItemData.EnsureLoaded();
        Call(world, "GenieInit");
        if (view != "empty")
        {
            var skills = Field<int[]>(world, "_genieSkills");
            SkillData.EnsureLoaded();
            var known = SkillData.ForClass(KnightOnlineUiClassic.Plugin.Kit.Nation == 1 ? 105 : 205).Where(s => SkillData.Icon(s.Id) != null).Select(s => s.Id).Take(12).ToArray();
            for (int i = 0; i < known.Length && i < skills.Length; i++) skills[i * 2 % skills.Length] = known[i];
            Call(world, "RefreshGenieSlots");
            foreach (var name in new[] { "Bulture", "Dark Mare", "Ash Knight Warrior With A Very Long Name", "Treant", "Lich", "Bone Dragon" })
                Field<HashSet<string>>(world, "_genieMonsters").Add(name);
            Call(world, "RefreshGenieMonsters");
        }
        Call(world, "ToggleGenie");
        Call(world, "OnGenieStatus", view == "empty" ? "..." : "Your genie whispers: keep your potions close and your enemies in range.", view != "claimed");
        if (view == "running")
        {
            SetField(world, "_genieWasRunning", true); SetField(world, "_genieWantsRunning", true);
            Native.Set(Net.I, "GenieRunning", true);
            Call(world, "OnGenieSystemState", true, 118);
        }
        else if (view == "pending")
        {
            SetField(world, "_genieRequestPending", true);
            Field<Button>(world, "_genieStart").Disabled = true;
            Field<Label>(world, "_genieRunLabel").Text = "Waiting for server response…";
        }
        else Call(world, "OnGenieSystemState", false, view == "empty" ? 0 : 240);
        return DetachWindow(world, "_genieLayer", "_geniePanel");
    }
}
