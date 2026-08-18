using ITMartinMusikStudio.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMusikStudio.Server.Data;

// Minimal demo-tier seed - one song with placeholder (not real) lyrics/
// chords, so a visitor sees the practice/step view populated. Only runs
// when MusikStudio:SeedDemoData=true. Idempotent.
public static class DemoSeeder
{
    public static async Task SeedAsync(StudioDbContext db)
    {
        if (await db.Songs.AnyAsync())
            return;

        db.Songs.Add(new StudioSong
        {
            Key = "demo-song",
            Title = "Demo-sang",
            Artist = "Demo Kunstner",
            MusicKey = "C",
            Tempo = 100,
            Lyrics = "[Eksempel-tekst til demoformål]",
            ChordChart = "C - G - Am - F",
            Notes = "Eksempel-sang, indlæst til demoformål.",
        });

        await db.SaveChangesAsync();
    }
}
