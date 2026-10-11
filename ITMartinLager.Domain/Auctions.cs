namespace ITMartinLager.Domain;

public sealed record AuctionSessionInfo(string Code, string Name, string Status, DateTime? AuctionDate, DateTime CreatedAt, DateTime ExpiresAt, int Items);
public sealed record AuctionStatusInfo(Guid Id, string Status, decimal? WinningBid, string? BuyerName, string? BuyerPhone);

// What Live Auktion's answers mean for an item in the register.
public static class AuctionResult
{
    public static string Description(Item i) =>
        string.Join("\n", new[] { i.Interest, $"{i.Kind} · stand: {i.Condition}" }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public static void Sent(Item item, Guid auctionItemId, string code)
    {
        item.AuctionItemId = auctionItemId.ToString();
        item.AuctionCode = code;
        item.Status = Statuses.PaaAuktion;
        item.Touch();
    }

    // Pending/Active = nothing yet.
    public static bool Apply(Item item, AuctionStatusInfo s, DateTime now)
    {
        switch (s.Status)
        {
            case "Sold":
                item.Status = Statuses.Solgt;
                item.SoldPrice = s.WinningBid;
                item.SoldTo = string.Join(", ", new[] { s.BuyerName, s.BuyerPhone }.Where(x => !string.IsNullOrWhiteSpace(x)));
                item.SoldAt = now;
                item.Note = Join(item.Note, $"Solgt på auktion {item.AuctionCode} {now:d/M yyyy}" + (s.WinningBid is { } b ? $" for {b:0} kr" : ""));
                break;
            case "Passed":
            case "Gone":
                item.Status = Statuses.Lager;
                item.Note = Join(item.Note, s.Status == "Passed"
                    ? $"Ikke solgt på auktion {item.AuctionCode} {now:d/M yyyy}"
                    : $"Fjernet fra auktion {item.AuctionCode} {now:d/M yyyy}");
                item.AuctionItemId = "";
                break;
            default:
                return false;
        }
        item.Touch();
        return true;
    }

    private static string Join(string a, string b) => string.IsNullOrWhiteSpace(a) ? b : a + " · " + b;
}
