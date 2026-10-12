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
            StoredItem.Create("Nøgler", "Skål på entrébordet", null, null, now.AddDays(-1))!,
            StoredItem.Create("Pas", "Skuffe i soveværelset, øverst til venstre", null, null, now.AddDays(-20))!,
            StoredItem.Create("Cykel­lygte", "Cykelkurv", "Husk at oplade den", null, now.AddDays(-3))!,
            StoredItem.Create("Ekstra opladere", "Skuffe i køkkenet", null, null, now.AddDays(-12))!);

        await db.SaveChangesAsync();
    }
}
