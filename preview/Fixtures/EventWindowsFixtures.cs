using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;

/// <summary>
/// Event windows and states the client's own preview builders do not cover: the Monster Stone prompt, the event
/// schedule before its first reply and the siege war panel before the castle is held.
/// Each builds the client's own window, feeds it the state a live session shows and detaches it from its layer.
/// </summary>
public static partial class PreviewFixtures
{
    private static HudWindow Detached(HudWindow window)
    {
        window.GetParent()?.RemoveChild(window);
        window.Visible = true;
        return window;
    }

    public static HudWindow BuildNestUiPreview(World world, string view)
    {
        ItemData.EnsureLoaded();
        Call(world, "BuildNestWindow");
        // A Monster Stone from the item table, with its own description as the explanation.
        var stone = ItemData.All().Where(i => i.Name.Contains("Monster Stone") || i.Name.Contains("Monster's Stone")).OrderBy(i => i.Id).FirstOrDefault();
        var window = Field<HudWindow>(world, "_nestWindow");
        SetField(world, "_nestStone", stone?.Id ?? 0);
        window.Title = stone != null ? ItemData.DisplayName(stone.Id) : "Monster Stone";
        Field<Label>(world, "_nestExplain").Text = view == "long"
            ? string.Join(" ", Enumerable.Repeat(stone?.Desc is { Length: > 0 } desc ? desc : "Summons the monsters sealed in the stone into a private dungeon.", 3))
            : stone?.Desc is { Length: > 0 } text ? text : "Summons the monsters sealed in the stone into a private dungeon for you and your party.";
        return Detached(window);
    }

    /// <summary>
    /// The client's daily rank preview with the board list it reads from the installed game content (absent from the
    /// audit packs): the picker lists the boards and the information lines describe the shown board.
    /// </summary>
    public static HudWindow BuildDailyRankBoardsUiPreview(World world, string view)
    {
        var window = (HudWindow)Call(world, "BuildDailyRankUiPreview", view)!;
        var boards = Field<List<DailyRankBoardInfo>>(world, "_drBoards");
        if (boards.Count == 0)
        {
            boards.Add(new DailyRankBoardInfo(DailyRank.AdonisBoard, "Adonis",
                new[] { "Ranks the characters who defeated the most enemies yesterday.", "The top three receive the Adonis title for one day." }));
            boards.Add(new DailyRankBoardInfo(DailyRank.ClanBoard, "Clan",
                new[] { "Ranks the clans that earned the most clan points yesterday.", "Clan ranks are shared by every member." }));
            var picker = Field<OptionButton>(world, "_drPicker");
            foreach (var board in boards) picker.AddItem(board.Title);
            Call(world, "RenderDailyRank", view == "daily-rank-clan" ? DailyRank.ClanBoard : DailyRank.AdonisBoard);
        }
        return window;
    }

    public static HudWindow BuildEventsScheduleUiPreview(World world, string view)
    {
        Call(world, "BuildEventsWindow");
        Call(world, "RefreshEventsWindow");
        return Detached(Field<HudWindow>(world, "_eventsPanel"));
    }

    public static HudWindow BuildSiegeWarStateUiPreview(World world, string view)
    {
        Call(world, "BuildSiegeWarWindow");
        bool held = view == "held";
        Call(world, "ShowSiegeStatus", new SiegeWarStatus(held ? (byte)2 : SiegeWarStatus.Unheld, 1, 0, 0, held ? 845 : 0, 2_400,
            held ? "TheVeryLongClanNameOfAll" : ""));
        return Detached(Field<HudWindow>(world, "_siegeWarPanel"));
    }
}
