using ITMartinPlayer.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinPlayer.Server.Data;

// Minimal demo-tier seed - one karaoke session with a couple of queued
// songs (placeholder titles, no real lyrics) so a visitor sees the queue
// flow immediately. Only runs when Player:SeedDemoData=true. Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(PlayerDbContext db)
    {
        if (await db.Sessions.AnyAsync())
            return;

        var session = new KaraokeSession { Code = "DEMO" };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        db.QueueEntries.AddRange(
            new QueueEntry { SessionId = session.Id, SingerName = "Anna", Title = "Demo-sang 1", Artist = "Demo Kunstner", Status = "Playing" },
            new QueueEntry { SessionId = session.Id, SingerName = "Bo", Title = "Demo-sang 2", Artist = "Demo Kunstner", Status = "Queued" });

        await db.SaveChangesAsync();
    }
}
