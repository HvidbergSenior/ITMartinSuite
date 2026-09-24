using ITMartinStrategi.Server.Data;
using ITMartinStrategi.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinStrategi.Server.Services;

/// <summary>The three games Martin plays now, with suggested topics to draft.
/// Only adds games that are missing - never touches guides.</summary>
public static class Seed
{
    public static async Task EnsureAsync(StrategiDbContext db)
    {
        Game[] games =
        [
            new()
            {
                Slug = "galciv4", Name = "Galactic Civilizations IV", Platform = "PC", Icon = "🪐", SortOrder = 0,
                Version = "Supernova med Federations & Empires (v4.x)",
                Topics = Lines(
                    "mekanik|Kerneverdener, kolonier og kontrol",
                    "mekanik|Administratorer og ledere",
                    "mekanik|Ideologi og ideologipoint",
                    "mekanik|Regeringsformer",
                    "mekanik|Forskning og tech-træet",
                    "mekanik|Indflydelse og kultur",
                    "mekanik|Skibsdesign og rumkamp",
                    "start|De første 50 ture",
                    "start|Bedste teknologier at starte med",
                    "start|Tidlig ekspansion",
                    "valg|Oligarki",
                    "valg|Demokrati",
                    "valg|Diktatur",
                    "valg|Valg af civilisation"),
            },
            new()
            {
                Slug = "dune-imperium", Name = "Dune: Imperium", Platform = "Digitalt brætspil", Icon = "🏜️", SortOrder = 1,
                Version = "Digital udgave med Rise of Ix og Immortality",
                Topics = Lines(
                    "mekanik|Agenter og felter på brættet",
                    "mekanik|Konflikter og kamp",
                    "mekanik|Indflydelse og alliancer med fraktionerne",
                    "mekanik|Spice, Solari og vand",
                    "mekanik|Intrigekort",
                    "start|De første runder",
                    "start|Hvilke kort skal man købe først",
                    "valg|Leder: Paul Atreides",
                    "valg|Leder: Baron Harkonnen",
                    "valg|Kampstrategi",
                    "valg|Indflydelsesstrategi"),
            },
            new()
            {
                Slug = "wingspan", Name = "Wingspan", Platform = "Digitalt brætspil", Icon = "🐦", SortOrder = 2,
                Version = "Digital udgave med udvidelser",
                Topics = Lines(
                    "mekanik|De fire handlinger",
                    "mekanik|Bonuskort og rundemål",
                    "mekanik|Fuglekræfter: brun, hvid og pink",
                    "start|De første to runder",
                    "valg|Æg-strategi",
                    "valg|Tuck- og cache-strategi",
                    "valg|Skov, græsmark eller vådområde"),
            },
        ];

        var existing = await db.Games.Select(g => g.Slug).ToListAsync();
        db.Games.AddRange(games.Where(g => !existing.Contains(g.Slug)));
        await db.SaveChangesAsync();
    }

    private static string Lines(params string[] lines) => string.Join('\n', lines);
}
