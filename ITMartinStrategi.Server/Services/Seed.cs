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
                Wiki = "https://wiki.galciv.com|3006|0:GC4",
                Version = "Supernova with Federations & Empires (v4.x)",
                Links = Lines(
                    "Stardock: dev journals and news|https://www.galciv4.com/news",
                    "Dev journal: Oligarchies|https://www.galciv4.com/article/541655/galciv-iv-dev-journal-118-oligarchies-in-federations-empires",
                    "Federations & Empires: the government types|https://www.galciv4.com/article/541465/galactic-civilizations-iv-federations-empires-brings-distinct-government-fo",
                    "Steam guides by players|https://steamcommunity.com/app/1357210/guides/",
                    "Reddit r/GalCiv|https://www.reddit.com/r/GalCiv/"),
                Topics = Lines(
                    "mekanik|Core worlds, colonies and control",
                    "mekanik|Administrators and leaders",
                    "mekanik|Ideology and ideology points",
                    "mekanik|Government types",
                    "mekanik|Research and the tech tree",
                    "mekanik|Influence and culture",
                    "mekanik|Ship design and space combat",
                    "start|The first 50 turns",
                    "start|Best technologies to start with",
                    "start|Early expansion",
                    "valg|Oligarchy",
                    "valg|Democracy",
                    "valg|Dictatorship",
                    "valg|Choosing a civilization"),
            },
            new()
            {
                Slug = "dune-imperium", Name = "Dune: Imperium", Platform = "Digital board game", Icon = "🏜️", SortOrder = 1, SteamAppId = 1689500,
                Version = "Digital edition with Rise of Ix and Immortality",
                Links = Lines(
                    "BoardGameGeek: cymerdown's comprehensive strategy guide|https://boardgamegeek.com/thread/2623493/cymerdowns-comprehensive-dune-imperium-strategy-gu",
                    "BoardGameGeek: strategy forum|https://boardgamegeek.com/boardgame/316554/dune-imperium/forums/66",
                    "Steam guides by players|https://steamcommunity.com/app/1689500/guides/"),
                Topics = Lines(
                    "mekanik|Agents and board spaces",
                    "mekanik|Conflicts and combat",
                    "mekanik|Influence and faction alliances",
                    "mekanik|Spice, Solari and water",
                    "mekanik|Intrigue cards",
                    "start|The first rounds",
                    "start|Which cards to buy first",
                    "valg|Leader: Paul Atreides",
                    "valg|Leader: Baron Harkonnen",
                    "valg|Combat strategy",
                    "valg|Influence strategy"),
            },
            new()
            {
                Slug = "wingspan", Name = "Wingspan", Platform = "Digital board game", Icon = "🐦", SortOrder = 2, SteamAppId = 1054490,
                Version = "Digital edition with expansions",
                Links = Lines(
                    "BoardGameGeek: strategy forum|https://boardgamegeek.com/boardgame/266192/wingspan/forums/66",
                    "Steam guides by players|https://steamcommunity.com/app/1054490/guides/"),
                Topics = Lines(
                    "mekanik|The four actions",
                    "mekanik|Bonus cards and round goals",
                    "mekanik|Bird powers: brown, white and pink",
                    "start|The first two rounds",
                    "valg|Egg strategy",
                    "valg|Tuck and cache strategy",
                    "valg|Forest, grassland or wetland"),
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
            e.Topics = s.Topics;
            if (s.Wiki.Length > 0) e.Wiki = s.Wiki;
            e.SteamAppId ??= s.SteamAppId;
        }
        await db.SaveChangesAsync();
    }

    private static string Lines(params string[] lines) => string.Join('\n', lines);
}
