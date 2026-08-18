using ITMartinMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMarket.Infrastructure;

// Minimal demo-tier seed - a couple of listings with a bid, so a visitor
// sees the marketplace populated immediately. Only runs when
// Market:SeedDemoData=true. Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(MarketDbContext db)
    {
        if (await db.Items.AnyAsync())
            return;

        var item1 = new SaleItem
        {
            Title = "Børnecykel, str. 20\"",
            Description = "God stand, næsten ikke brugt.",
            AskingPrice = 400m,
            SellerName = "Anna",
        };
        var item2 = new SaleItem
        {
            Title = "Havebord med 4 stole",
            Description = "Lidt slidt, men solidt.",
            AskingPrice = 600m,
            SellerName = "Bo",
        };
        db.Items.AddRange(item1, item2);
        await db.SaveChangesAsync();

        db.Bids.Add(new Bid { SaleItemId = item1.Id, BuyerName = "Cecilie", Amount = 350m });

        await db.SaveChangesAsync();
    }
}
