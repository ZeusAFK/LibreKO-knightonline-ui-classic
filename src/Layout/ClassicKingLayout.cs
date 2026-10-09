using Godot;

namespace KnightOnlineUiClassic.Layout;

/// <summary>
/// Editable geometry of the King election and nation treasury windows. Artwork comes from the original
/// King UIF compositions; every rectangle is in window coordinates of that composition.
/// </summary>
public static class ClassicKingLayout
{
    public const string ElectionArt = "co_king_electionmain_us";
    public const string NominateArt = "co_king_memberrecommend_us";
    public const string BoardArt = "co_king_electionboard_us";
    public const string VoteArt = "co_king_electionlist_us";
    public const string BallotArt = "co_msgboxokcancel_king_us";
    public const string TreasuryArt = "co_nationtaxnpc_us";
    public const string CloseArt = "co_questtalk_us";

    public static string Art(string id) => id switch
    {
        "kingelection" => ElectionArt,
        "kingnominate" => NominateArt,
        "kingplan" or "nationintro" => BoardArt,
        "kingvote" => VoteArt,
        "kingballot" => BallotArt,
        _ => TreasuryArt,
    };

    public static Vector2 Size(string id) => id switch
    {
        "kingelection" or "kingnominate" => new(440, 312),
        "kingplan" or "nationintro" => new(408, 296),
        "kingvote" => new(324, 332),
        "kingballot" => new(293, 104),
        _ => new(279, 306 + TreasuryExtra),
    };

    /// <summary>Opaque backing under the translucent original art, inside the outer border.</summary>
    public static Rect2 Backing(string id) => new(new Vector2(3, 3), Size(id) - new Vector2(6, 6));

    // Election officer (co_king_electionmain_us).
    public static readonly Rect2 ElectionDrag = new(0, 0, 404, 142);
    public static readonly Rect2 ElectionClose = new(410, 12, 20, 20);
    public static readonly Rect2 ElectionSpeechFull = new(140, 20, 266, 120);
    public static readonly Rect2 ElectionSpeechUpper = new(140, 20, 266, 80);
    public static readonly Rect2 ElectionSpeechLower = new(140, 102, 266, 40);
    public static readonly Rect2 ElectionSenatorsCaption = new(140, 20, 266, 36);
    public static readonly Rect2 ElectionSenators = new(140, 58, 266, 82);
    /// <summary>Option rows keep the original button art rectangles (165 x 22 at a 31 px pitch).</summary>
    public static Rect2 ElectionOption(int index) => new(140, 149 + 31 * index, 165, 22);
    /// <summary>Caption width of an option row: the original button string (154 px).</summary>
    public const float ElectionCaption = 154;

    // Nomination (co_king_memberrecommend_us).
    public static readonly Rect2 NominateDrag = new(0, 0, 440, 145);
    public static readonly Rect2 NominateText = new(140, 24, 266, 73);
    public static readonly Rect2 NominateNote = new(140, 100, 266, 42);
    public static readonly Rect2 NominateIdCaption = new(142, 154, 35, 18);
    public static readonly Rect2 NominateId = new(187, 152, 201, 22);
    public static readonly Rect2 NominateAccept = new(146, 202, 165, 21);
    public static readonly Rect2 NominateCancel = new(146, 238, 165, 21);

    // Nominee board and nation introduction editors (co_king_electionboard_us).
    public static readonly Rect2 BoardDrag = new(0, 0, 370, 38);
    public static readonly Rect2 BoardTitle = new(40, 13, 300, 17);
    public static readonly Rect2 BoardClose = new(378, 10, 20, 20);
    public static readonly Rect2 BoardText = new(40, 46, 329, 172);
    public static readonly Rect2 BoardCount = new(40, 220, 329, 16);
    public static readonly Rect2 BoardLeft = new(82, 266, 74, 21);
    public static readonly Rect2 BoardRight = new(231, 266, 74, 21);

    // Candidate list (co_king_electionlist_us).
    public const int VoteRows = 10;
    public static readonly Rect2 VoteDrag = new(0, 0, 324, 50);
    public static readonly Rect2 VoteTitle = new(30, 21, 264, 17);
    public static readonly Rect2 VoteNameHeading = new(43, 55, 117, 17);
    public static readonly Rect2 VoteClanHeading = new(168, 55, 135, 17);
    private static readonly int[] VoteRowY = { 78, 98, 117, 136, 155, 174, 193, 212, 231, 251 };
    public static Rect2 VoteSelect(int row) => new(21, 79 + 19 * row, 17, 16);
    public static Rect2 VoteNumber(int row) => new(44, VoteRowY[row] - 2, 20, 17);
    public static Rect2 VoteName(int row) => new(66, VoteRowY[row] - 2, 96, 17);
    public static Rect2 VoteClan(int row) => new(170, VoteRowY[row] - 2, 88, 17);
    public static Rect2 VotePledge(int row) => new(261, VoteRowY[row] - 1, 40, 14);
    public static readonly Rect2 VoteAccept = new(63, 302, 73, 21);
    public static readonly Rect2 VoteCancel = new(186, 302, 73, 21);
    /// <summary>Small King board button art (co_king_electionregist_us Write/Erase).</summary>
    public const string SmallButtonArt = "co_king_electionregist_us";

    // Impeachment ballot (co_msgboxokcancel_king_us).
    public static readonly Rect2 BallotDrag = new(0, 0, 293, 72);
    public static readonly Rect2 BallotText = new(12, 11, 270, 58);
    public static readonly Rect2 BallotFavour = new(21, 77, 73, 20);
    public static readonly Rect2 BallotAgainst = new(111, 77, 73, 20);
    public static readonly Rect2 BallotCancel = new(201, 77, 73, 20);

    // Grand Chamberlain (co_nationtaxnpc_us). The original has four services and Exit; LibreKO adds the
    // nation introduction, so the stretchable middle section grows by one 24 px row.
    public const int TreasuryExtra = 24;
    public const int TreasurySplit = 237;
    public static readonly Rect2 TreasuryDrag = new(0, 0, 279, 140);
    public static readonly Rect2 TreasuryHeading = new(101, 78, 166, 17);
    public static readonly Rect2 TreasuryNotice = new(81, 100, 186, 40);
    public static readonly Rect2 TreasuryCitizenNotice = new(24, 118, 236, 136);
    public static Rect2 TreasuryOption(int row) => new(62, 144 + 24 * row, 159, 20);
    public const int TreasuryOptionRows = 6;
}
