using ITMartinAdhd.Domain.Entities;
using ITMartinAdhd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITMartinAdhd.Infrastructure;

// Minimal demo-tier seed - a handful of stored items so a visitor sees the
// "where did I put X" search working immediately. Only runs when
// Adhd:SeedDemoData=true. Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(AdhdDbContext db)
    {
        if (await db.StoredItems.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        db.StoredItems.AddRange(
            new StoredItem { Name = "Nøgler", Location = "Skål på entrébordet", StoredAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1) },
            new StoredItem { Name = "Pas", Location = "Skuffe i soveværelset, øverst til venstre", StoredAt = now.AddDays(-20), UpdatedAt = now.AddDays(-20) },
            new StoredItem { Name = "Cykel­lygte", Location = "Cykelkurv", Notes = "Husk at oplade den", StoredAt = now.AddDays(-3), UpdatedAt = now.AddDays(-3) },
            new StoredItem { Name = "Ekstra opladere", Location = "Skuffe i køkkenet", StoredAt = now.AddDays(-40), UpdatedAt = now.AddDays(-40) });

        await db.SaveChangesAsync();
    }
}
