using ITMartinBarTab.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBarTab.Server.Data;

// Minimal demo-tier seed - one bar session with a few participants and
// drinks so a visitor sees the running tab immediately. Only runs when
// BarTab:SeedDemoData=true. Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(BarTabDbContext db)
    {
        if (await db.Sessions.AnyAsync())
            return;

        var session = new Session { Code = "DEMO", Name = "Fredagsbar" };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var anna = new Participant { SessionId = session.Id, Name = "Anna", Color = "#4f8ef7" };
        var bo = new Participant { SessionId = session.Id, Name = "Bo", Color = "#f5a623" };
        db.Participants.AddRange(anna, bo);
        await db.SaveChangesAsync();

        db.Drinks.AddRange(
            new DrinkEntry { SessionId = session.Id, AddedByParticipantId = anna.Id, Description = "Fadøl", Price = 45m },
            new DrinkEntry { SessionId = session.Id, AddedByParticipantId = bo.Id, Description = "Rødvin", Price = 65m },
            new DrinkEntry { SessionId = session.Id, AddedByParticipantId = anna.Id, Description = "Chips til bordet", Price = 30m, IsRound = true });

        await db.SaveChangesAsync();
    }
}
