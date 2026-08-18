using ITMartinDreamReader.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinDreamReader.Server.Data;

// Minimal demo-tier seed - a couple of dream entries with example AI
// interpretation text already filled in (no live Claude call needed for
// seed data). Reuses the app's own already-seeded category taxonomy
// (Who/Where/Doing/Feeling/Reception, inserted unconditionally in
// Program.cs on every startup) instead of creating duplicate categories.
// Only runs when DreamReader:SeedDemoData=true. Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(DreamDbContext db)
    {
        if (await db.Entries.AnyAsync())
            return;

        var flying = await db.Categories.FirstOrDefaultAsync(c => c.Name == "Flyver" && c.Layer == "Doing");
        var home = await db.Categories.FirstOrDefaultAsync(c => c.Name == "Hjem" && c.Layer == "Where");
        var family = await db.Categories.FirstOrDefaultAsync(c => c.Name == "Familie" && c.Layer == "Who");

        var entry1 = new DreamEntry
        {
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            Note = "Jeg fløj hen over huset og kunne se hele haven ovenfra.",
            Rating = "Nice",
            AiTitle = "Flyvning over hjemmet",
            AiInterpretation = "At flyve i drømme forbindes ofte med en følelse af frihed eller overblik over sit eget liv lige nu.",
        };
        if (flying is not null) entry1.Categories.Add(flying);
        if (home is not null) entry1.Categories.Add(home);

        var entry2 = new DreamEntry
        {
            CreatedAt = DateTime.UtcNow.AddDays(-6),
            Note = "Hele familien var samlet til middag, men køkkenet blev ved med at ændre form.",
            Rating = "Medium",
            AiTitle = "Middag i det foranderlige køkken",
            AiInterpretation = "Et rum der skifter form kan afspejle en følelse af, at noget velkendt i familielivet er i forandring.",
            AiFunny = "Måske er det bare på tide at renovere køkkenet.",
        };
        if (family is not null) entry2.Categories.Add(family);
        if (home is not null) entry2.Categories.Add(home);

        db.Entries.AddRange(entry1, entry2);
        await db.SaveChangesAsync();
    }
}
