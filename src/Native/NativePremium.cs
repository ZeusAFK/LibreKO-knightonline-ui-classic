using Godot;
using LibreKO;
using LibreKO.Domain;
using LibreKO.Network;

namespace KnightOnlineUiClassic.Native;

/// <summary>
/// The account and clan premium as the client's chips show them (<c>World.Premium</c>, <c>World.ClanPremium</c>), and
/// the client's game clock (<c>_clockLabel</c>: the server's time of day and the weather), for HUD pieces that show
/// them elsewhere. The client keeps requesting and receiving every value.
/// </summary>
public static class NativePremium
{
    private const int PcRoomStatus = 2;

    public readonly record struct Account(bool Active, string Name, int Hours, IReadOnlyList<PremiumBonus> Bonuses);

    public readonly record struct Clan(bool InClan, bool Active, long EndsAt);

    public static Account Current(Net? net)
    {
        if (net == null || net.PremiumAccountStatus == 0 || net.PremiumHours <= 0) return new(false, "No Premium", 0, Array.Empty<PremiumBonus>());
        string name = net.PremiumAccountStatus == PcRoomStatus ? "PC Room" : PremiumName(net.PremiumType);
        return new(true, name, net.PremiumHours, net.PremiumBonuses);
    }

    /// <summary>The premium name as the client's chip composes it (3277df5): the type's text and the "Premium" word.</summary>
    private static string PremiumName(int type)
    {
        string premium = ItemData.Text(PremiumNames.PremiumText, PremiumNames.PremiumFallback);
        return PremiumNames.Name(type) is { } name ? $"{ItemData.Text(name.Text, name.Fallback)} {premium}" : premium;
    }

    public static Clan ClanOf(World? world, Net? net) =>
        new(world != null && Native.Get<bool>(world, "_clanPremiumInClan"), net?.ClanPremiumActive == true, net?.ClanPremiumEndsAt ?? 0);

    /// <summary>"2 days 4 hours", "5 hours", "45 minutes".</summary>
    public static string Duration(long seconds)
    {
        var left = TimeSpan.FromSeconds(Math.Max(0, seconds));
        static string Unit(int n, string unit) => $"{n} {unit}{(n == 1 ? "" : "s")}";
        if (left.TotalDays >= 1) return left.Hours > 0 ? Unit(left.Days, "day") + " " + Unit(left.Hours, "hour") : Unit(left.Days, "day");
        if (left.TotalHours >= 1) return Unit((int)left.TotalHours, "hour");
        return Unit(Math.Max(1, left.Minutes), "minute");
    }

    /// <summary>The name, the time left and the benefits the client's chip tooltip lists, one per line.</summary>
    public static string Details(Account account)
    {
        if (!account.Active) return "No Premium\nNo premium is active on this account.";
        var lines = new List<string> { account.Name, Duration(account.Hours * 3600L) + " left" };
        lines.AddRange(account.Bonuses.Select(b => b.Text).OfType<string>());
        return string.Join("\n", lines);
    }

    public static string ClanDetails(Clan clan)
    {
        if (!clan.Active) return "Clan: No Premium\nYour clan has no premium.";
        long left = clan.EndsAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (clan.EndsAt <= 0 || left <= 0) return "Clan Premium";
        return $"Clan Premium\n{Duration(left)} left\nEnds {DateTimeOffset.FromUnixTimeSeconds(clan.EndsAt).LocalDateTime:yyyy-MM-dd HH:mm}";
    }

    /// <summary>The client's clock text ("12:00  Clear"): the time of day the server sends and the weather.</summary>
    public static string? Clock(World? world) =>
        world != null && Native.Get<RichTextLabel>(world, "_clockLabel") is { } label && GodotObject.IsInstanceValid(label) ? label.GetParsedText().Trim() : null;

    /// <summary>
    /// Hides the client's clan premium chip and clock while the group shows them. The account premium chip stays as an
    /// invisible spacer <paramref name="width"/> wide: the client lines its remaining top icons up left of it.
    /// </summary>
    public static void HideClientChips(World world, float width)
    {
        foreach (var field in new[] { "_clanPremiumChip", "_clockLabel" })
            if (Native.Get<CanvasItem>(world, field) is { Visible: true } native) native.Visible = false;
        if (Native.Get<Control>(world, "_premiumChip") is not { } chip || chip.Modulate.A == 0) return;
        chip.Modulate = Colors.Transparent;
        chip.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var child in Native.Descendants(chip).OfType<Control>()) child.MouseFilter = Control.MouseFilterEnum.Ignore;
        chip.CustomMinimumSize = new Vector2(width, chip.CustomMinimumSize.Y);
        Native.TryCall(world, "PlaceTopIcons", out _);
    }
}
