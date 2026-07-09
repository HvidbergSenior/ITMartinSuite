using ITMartinStarRealms.Server.Data;
using ITMartinStarRealms.Server.Data.Entities;

namespace ITMartinStarRealms.Server.Services;

// Real official Star Realms game modes, researched from starrealms.com and
// ultraboardgames.com (2026-07-09) - not invented. Community "variants" found
// (Trade Stacks, Events/Gambits, etc.) are card-mechanic house rules that don't
// change player count/Authority/elimination structure, so they don't apply to
// a score tracker and aren't included as separate modes here.
public static class RulesetSeeder
{
    public static async Task SeedAsync(StarRealmsDbContext db)
    {
        if (db.Rulesets.Any()) return;

        db.Rulesets.AddRange(
            new GameRuleset
            {
                Name = "Standard (1v1)",
                Description = "Den klassiske duel. To spillere starter med 50 Authority hver. Første spiller til 0 taber.",
                MinPlayers = 2,
                MaxPlayers = 2,
                IsTeamMode = false,
                DefaultStartingPoints = 50,
                IsBuiltIn = true
            },
            new GameRuleset
            {
                Name = "Free-for-All",
                Description = "3-6 spillere. Alle kan angribe alle og deres baser. Sidste spiller tilbage vinder.",
                MinPlayers = 3,
                MaxPlayers = 6,
                IsTeamMode = false,
                DefaultStartingPoints = 50,
                IsBuiltIn = true
            },
            new GameRuleset
            {
                Name = "Team Play – Hydra (2v2)",
                Description = "4 spillere i 2 hold. Hvert hold deler én fælles Authority-pulje (normalt 75). Holdkammerater kan bruge hinandens Trade og Combat til at angribe/købe sammen.",
                MinPlayers = 4,
                MaxPlayers = 4,
                IsTeamMode = true,
                PlayersPerTeam = 2,
                SharedTeamPool = true,
                DefaultStartingPoints = 75,
                IsBuiltIn = true
            },
            new GameRuleset
            {
                Name = "Team Play – Emperor (6 spillere)",
                Description = "6 spillere i 2 hold à 3. Én spiller pr. hold er \"Emperor\" (60 Authority), de to andre er \"Admirals\" (50 Authority hver, individuelt). Admirals må kun angribe den modstående Admiral. Holdet taber hvis Emperoren falder.",
                MinPlayers = 6,
                MaxPlayers = 6,
                IsTeamMode = true,
                PlayersPerTeam = 3,
                SharedTeamPool = false,
                DefaultStartingPoints = 50,
                IsBuiltIn = true
            },
            new GameRuleset
            {
                Name = "Hunter Mode",
                Description = "3+ spillere. Du må kun angribe spilleren til venstre for dig (og deres baser). Besejrer du dem, går du videre til den næste spiller på venstre hånd.",
                MinPlayers = 3,
                MaxPlayers = 6,
                IsTeamMode = false,
                DefaultStartingPoints = 50,
                IsBuiltIn = true
            }
        );

        await db.SaveChangesAsync();
    }
}
