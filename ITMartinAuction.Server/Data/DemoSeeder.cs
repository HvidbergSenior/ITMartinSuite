using ITMartinAuction.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinAuction.Server.Data;

// Minimal demo-tier seed - one live-ish auction with a couple of items and
// bids so a visitor sees the flow immediately. Only runs when
// Auction:SeedDemoData=true. Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(AuctionDbContext db)
    {
        if (await db.Sessions.AnyAsync())
            return;

        var session = new AuctionSession
        {
            Code = "DEMO",
            Name = "Klub-auktion",
            Status = AuctionStatus.Live,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var item1 = new AuctionItem { SessionId = session.Id, Name = "Håndlavet fuglekasse", StartingPrice = 50m, Status = AuctionItemStatus.Active, SortOrder = 0 };
        var item2 = new AuctionItem { SessionId = session.Id, Name = "Gavekort til lokal café", StartingPrice = 100m, Status = AuctionItemStatus.Pending, SortOrder = 1 };
        db.Items.AddRange(item1, item2);
        await db.SaveChangesAsync();

        var anna = new Bidder { SessionId = session.Id, Name = "Anna", BidderNumber = 1, Color = "#f5a623" };
        var bo = new Bidder { SessionId = session.Id, Name = "Bo", BidderNumber = 2, Color = "#4f8ef7" };
        db.Bidders.AddRange(anna, bo);
        await db.SaveChangesAsync();

        db.Bids.AddRange(
            new Bid { AuctionItemId = item1.Id, BidderId = anna.Id, Amount = 55m },
            new Bid { AuctionItemId = item1.Id, BidderId = bo.Id, Amount = 65m });

        session.ActiveItemId = item1.Id;

        await db.SaveChangesAsync();
    }
}
