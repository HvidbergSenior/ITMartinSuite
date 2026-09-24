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
                Slug = "galciv4", Name = "Galactic Civilizations IV", Platform = "PC", Icon = "🪐", SortOrder = 0, SteamAppId = 1357210,
                Version = "Supernova med Federations & Empires (v4.x)",
                Links = Lines(
                    "Stardock: udvikler-journaler og nyheder|https://www.galciv4.com/news",
                    "Dev journal: Oligarkiet|https://www.galciv4.com/article/541655/galciv-iv-dev-journal-118-oligarchies-in-federations-empires",
                    "Federations & Empires: regeringsformerne|https://www.galciv4.com/article/541465/galactic-civilizations-iv-federations-empires-brings-distinct-government-fo",
                    "Steam-guides fra spillere|https://steamcommunity.com/app/1357210/guides/",
                    "Reddit r/GalCiv|https://www.reddit.com/r/GalCiv/"),
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
                Slug = "dune-imperium", Name = "Dune: Imperium", Platform = "Digitalt brætspil", Icon = "🏜️", SortOrder = 1, SteamAppId = 1689500,
                Version = "Digital udgave med Rise of Ix og Immortality",
                Links = Lines(
                    "BoardGameGeek: cymerdowns store strategiguide|https://boardgamegeek.com/thread/2623493/cymerdowns-comprehensive-dune-imperium-strategy-gu",
                    "BoardGameGeek: strategi-forum|https://boardgamegeek.com/boardgame/316554/dune-imperium/forums/66",
                    "Steam-guides fra spillere|https://steamcommunity.com/app/1689500/guides/"),
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
                Slug = "wingspan", Name = "Wingspan", Platform = "Digitalt brætspil", Icon = "🐦", SortOrder = 2, SteamAppId = 1054490,
                Version = "Digital udgave med udvidelser",
                Links = Lines(
                    "BoardGameGeek: strategi-forum|https://boardgamegeek.com/boardgame/266192/wingspan/forums/66",
                    "Steam-guides fra spillere|https://steamcommunity.com/app/1054490/guides/"),
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

        var existing = await db.Games.ToListAsync();
        db.Games.AddRange(games.Where(g => existing.All(e => e.Slug != g.Slug)));
        // The seeded games keep their curated links and Steam id up to date.
        foreach (var e in existing)
        {
            var s = games.FirstOrDefault(g => g.Slug == e.Slug);
            if (s is null) continue;
            e.Links = s.Links;
            e.SteamAppId ??= s.SteamAppId;
        }
        await db.SaveChangesAsync();
    }

    private static string Lines(params string[] lines) => string.Join('\n', lines);
}
