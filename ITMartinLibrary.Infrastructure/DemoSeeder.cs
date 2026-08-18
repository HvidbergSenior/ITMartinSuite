using ITMartinLibrary.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLibrary.Infrastructure;

// Minimal demo-tier seed - a demo group with a handful of scanned items
// (generic invented titles, not real book/movie data) so a visitor sees a
// populated collection immediately. Only runs when Library:SeedDemoData=true.
// Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(LibraryDbContext db)
    {
        if (await db.Items.AnyAsync())
            return;

        var group = new LibraryGroup { Id = Guid.NewGuid(), Slug = "demo", Name = "Demo-samling", CreatedAt = DateTime.UtcNow };
        db.Groups.Add(group);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;

        db.Items.AddRange(
            new InventoryItem
            {
                GroupId = group.Id,
                Barcode = "0000000000001",
                Quantity = 1,
                Title = "Rejsen til de syv øer",
                Type = "Bog",
                AuthorOrDirector = "A. Forfatter",
                Genre = "Eventyr",
                ReleaseYear = "2016",
                ShelfLocation = "Reol 2, hylde B",
                LookupStatus = "Done",
                FirstScannedAt = now.AddDays(-30),
                LastScannedAt = now.AddDays(-30),
                DetailsUpdatedAt = now.AddDays(-30),
            },
            new InventoryItem
            {
                GroupId = group.Id,
                Barcode = "0000000000002",
                Quantity = 2,
                Title = "Nattens Kortlægger",
                Type = "Film",
                AuthorOrDirector = "B. Instruktør",
                Genre = "Thriller",
                Runtime = "118 min",
                ReleaseYear = "2021",
                ShelfLocation = "Reol 4, hylde A",
                LookupStatus = "Done",
                FirstScannedAt = now.AddDays(-10),
                LastScannedAt = now.AddDays(-10),
                DetailsUpdatedAt = now.AddDays(-10),
            });

        await db.SaveChangesAsync();
    }
}
