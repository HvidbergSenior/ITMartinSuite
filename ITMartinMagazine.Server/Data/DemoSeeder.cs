using ITMartinMagazine.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMagazine.Server.Data;

// Minimal demo-tier seed - a couple of scanned magazine entries (generic
// invented titles, not real publications) so a visitor sees the price/value
// lookup flow immediately. Only runs when Magazine:SeedDemoData=true.
// Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(MagazineDbContext db)
    {
        if (await db.Magazines.AnyAsync())
            return;

        db.Magazines.AddRange(
            new MagazineEntry
            {
                Title = "Ugens Hobby",
                IssueDate = "Marts 1987",
                Year = 1987,
                Publisher = "Demo Forlag",
                Country = "Denmark",
                Condition = "Good",
                ValueRating = "Medium",
                AiReasoning = "Eksempel-vurdering til demoformål — velholdt eksemplar af et almindeligt oplagt blad.",
            },
            new MagazineEntry
            {
                Title = "Modelbyggeren",
                IssueDate = "Sommer 1993",
                Year = 1993,
                Publisher = "Demo Magasiner A/S",
                Country = "Denmark",
                Condition = "Fair",
                ValueRating = "Low",
                AiReasoning = "Eksempel-vurdering til demoformål — almindeligt oplag, slidt ryg.",
            });

        await db.SaveChangesAsync();
    }
}
