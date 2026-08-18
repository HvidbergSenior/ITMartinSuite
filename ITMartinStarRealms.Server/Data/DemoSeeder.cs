using ITMartinStarRealms.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinStarRealms.Server.Data;

// Minimal demo-tier seed - one in-progress game session with two players
// mid-way through so a visitor sees live point tracking immediately. Only
// runs when StarRealms:SeedDemoData=true. Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(StarRealmsDbContext db)
    {
        if (await db.Sessions.AnyAsync())
            return;

        var session = new GameSession
        {
            Code = "DEMO",
            RulesetName = "Standard (1v1)",
            HasStarted = true,
            IsRanked = false,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        db.Players.AddRange(
            new GamePlayer { SessionId = session.Id, Name = "Anna", Avatar = "🚀", Color = "#e74c3c", Points = 32, SortOrder = 0 },
            new GamePlayer { SessionId = session.Id, Name = "Bo", Avatar = "🛸", Color = "#3498db", Points = 41, SortOrder = 1 });

        await db.SaveChangesAsync();
    }
}
