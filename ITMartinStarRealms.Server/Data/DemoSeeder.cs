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

        // Real scores from an actual game (session 6AQSXF, 2026-08-16) rather
        // than invented players - this is Martin's own recreational scoreboard
        // data (not a customer's), so using it directly for the demo is fine.
        // Avatars simplified to initials instead of copying the real uploaded
        // profile photos over.
        db.Players.AddRange(
            new GamePlayer { SessionId = session.Id, Name = "ITMartin", Avatar = "IM", Color = "#f1c40f", Points = 50, SortOrder = 0 },
            new GamePlayer { SessionId = session.Id, Name = "Bertil Prut", Avatar = "BP", Color = "#e74c3c", Points = 50, SortOrder = 1 });

        await db.SaveChangesAsync();
    }
}
