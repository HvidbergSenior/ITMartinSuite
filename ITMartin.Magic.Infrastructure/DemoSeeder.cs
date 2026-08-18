using ITMartin.Magic.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Magic.Infrastructure.Persistence;

// Minimal demo-tier seed - a couple of scanned cards with example prices, so
// a visitor sees the pricing/collection view populated. Card names/set codes
// are real (factual game reference data, not reproduced creative text) but
// prices are made-up example values. Only runs when
// MagicPriser:SeedDemoData=true. Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(MagicDbContext db)
    {
        if (await db.Cards.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        db.Cards.AddRange(
            new MagicCard
            {
                Id = Guid.NewGuid(),
                Name = "Lightning Bolt",
                SetCode = "M11",
                CollectorNumber = "146",
                Owner = "Demo",
                Quantity = 4,
                EurPrice = 1.20m,
                UsdPrice = 1.30m,
                FirstSeenAt = now.AddDays(-20),
                LastSeenAt = now.AddDays(-20),
            },
            new MagicCard
            {
                Id = Guid.NewGuid(),
                Name = "Counterspell",
                SetCode = "7ED",
                CollectorNumber = "68",
                Owner = "Demo",
                Quantity = 2,
                EurPrice = 0.80m,
                UsdPrice = 0.90m,
                FirstSeenAt = now.AddDays(-20),
                LastSeenAt = now.AddDays(-20),
            });

        await db.SaveChangesAsync();
    }
}
