using ITMartinMusic.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMusic.Server.Data;

// Minimal demo-tier seed - a couple of songs with placeholder (not real)
// lyrics/chords, so a visitor sees the practice/chart view populated. Only
// runs when Musik:SeedDemoData=true. Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(MusicDbContext db)
    {
        if (await db.Songs.AnyAsync())
            return;

        var song1 = new Song
        {
            Title = "Demo-sang 1",
            Key = "G",
            Tempo = 96,
            ChordChart = "G - C - D - G",
            Lyrics = "[Eksempel-tekst til demoformål]",
            Notes = "Eksempel-sang, indlæst til demoformål.",
        };
        var song2 = new Song
        {
            Title = "Demo-sang 2",
            Key = "Am",
            Tempo = 120,
            ChordChart = "Am - F - C - G",
            Lyrics = "[Eksempel-tekst til demoformål]",
        };
        db.Songs.AddRange(song1, song2);
        await db.SaveChangesAsync();

        db.PracticeEntries.Add(new PracticeEntry
        {
            SongId = song1.Id,
        });

        await db.SaveChangesAsync();
    }
}
