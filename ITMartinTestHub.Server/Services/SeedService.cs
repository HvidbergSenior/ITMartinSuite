using ITMartinTestHub.Server.Data;
using ITMartinTestHub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinTestHub.Server.Services;

public static class SeedService
{
    // ── App catalogue (idempotent by name) ───────────────────────────────

    public static async Task SeedAppsAsync(TestHubDbContext db)
    {
        var existingNames = (await db.Apps.Select(a => a.Name).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var apps = new[]
        {
            new AppEntry { Name = "FileSorter",     Icon = "📦", Url = "https://filesorter.itmartin.dk",       Description = "Medierydning og forbedringspipeline",  SortOrder = 1  },
            new AppEntry { Name = "Gallery",         Icon = "🎬", Url = "https://gallery.itmartin.dk",          Description = "Medieviser og samlinger",              SortOrder = 2  },
            new AppEntry { Name = "Budget",          Icon = "💰", Url = "https://budget.itmartin.dk",           Description = "Personlig økonomi-tracker",            SortOrder = 3  },
            new AppEntry { Name = "Receipt",         Icon = "🧾", Url = "https://receipt.itmartin.dk",          Description = "Kvitterings-scanner og -organisator",  SortOrder = 4  },
            new AppEntry { Name = "Library",         Icon = "📚", Url = "https://library.itmartin.dk",          Description = "Bog- og filmsamling",                  SortOrder = 5  },
            new AppEntry { Name = "BarTab",          Icon = "🍺", Url = "https://bartab.itmartin.dk",           Description = "Grupperegning med AI-drinks",          SortOrder = 6  },
            new AppEntry { Name = "Auction",         Icon = "🔨", Url = "https://auction.itmartin.dk",          Description = "Live-budgivning for samleobjekter",    SortOrder = 7  },
            new AppEntry { Name = "Magic",           Icon = "✨", Url = "https://magic.itmartin.dk",            Description = "AI-kortscanner",                       SortOrder = 8  },
            new AppEntry { Name = "FindIt",          Icon = "📍", Url = "https://adhd.itmartin.dk",            Description = "Placerings-tracker for genstande",     SortOrder = 9  },
            new AppEntry { Name = "Idag",             Icon = "📋", Url = "https://idag.itmartin.dk",             Description = "Fælles opgavetavle for grupper – fokus på i dag", SortOrder = 10 },
            new AppEntry { Name = "Market",          Icon = "🛍️", Url = "https://market.itmartin.dk",           Description = "Markedsplads",                         SortOrder = 11 },
            new AppEntry { Name = "R6 Assistant",    Icon = "🎮", Url = "https://r6.itmartin.dk",               Description = "Rainbow Six Siege-assistent",          SortOrder = 12 },
            new AppEntry { Name = "Portal",          Icon = "🏠", Url = "https://martin.itmartin.dk",           Description = "Hovedportal og indeks",                SortOrder = 13 },
            new AppEntry { Name = "Library Scan",    Icon = "📷", Url = "https://scan-books.itmartin.dk",       Description = "Scan en bogreol med kameraet – AI identificerer bøgerne",   SortOrder = 14 },
            new AppEntry { Name = "Library Search",  Icon = "🔍", Url = "https://search-books.itmartin.dk",    Description = "Søg i bøger identificeret fra scannede bogreoler",          SortOrder = 15 },
            new AppEntry { Name = "Club",            Icon = "🏛️", Url = "https://lions-club.itmartin.dk",       Description = "Gruppeorganisator med opslagstavle, kalender og dokumenter", SortOrder = 16 },
            new AppEntry { Name = "Magic Scan",      Icon = "🃏", Url = "https://magic-card-pricing.itmartin.dk", Description = "AI-drevet MTG-kortscanner med prisopslag",              SortOrder = 17 },
            new AppEntry { Name = "TestHub",          Icon = "🧪", Url = "https://test.itmartin.dk",               Description = "Test-app til kvalitetstestning af ITMartin-apps",       SortOrder = 18 },
        };

        var toAdd = apps.Where(a => !existingNames.Contains(a.Name)).ToList();
        if (toAdd.Count == 0) return;

        db.Apps.AddRange(toAdd);
        await db.SaveChangesAsync();
    }

    // ── Correct URLs for existing apps ───────────────────────────────────

    private static readonly Dictionary<string, string> CorrectUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Library Scan"]   = "https://scan-books.itmartin.dk",
        ["Library Search"] = "https://search-books.itmartin.dk",
        ["Club"]           = "https://lions-club.itmartin.dk",
        ["Magic Scan"]     = "https://magic-card-pricing.itmartin.dk",
        ["FindIt"]         = "https://adhd.itmartin.dk",
    };

    public static async Task UpdateAppUrlsAsync(TestHubDbContext db)
    {
        var names = CorrectUrls.Keys.ToList();
        var apps  = await db.Apps.Where(a => names.Contains(a.Name)).ToListAsync();

        var changed = false;
        foreach (var app in apps)
        {
            if (CorrectUrls.TryGetValue(app.Name, out var url) && app.Url != url)
            {
                app.Url  = url;
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync();
    }

    // ── Managed test steps (Danish, always kept in sync) ─────────────────
    // For these apps the steps are defined here. On startup the DB is
    // updated if the first step doesn't match the expected Danish text.

    private static readonly Dictionary<string, List<(int Order, string Instruction, string Expected)>> ManagedSteps = new()
    {
        ["Idag"] =
        [
            (1,  "Åbn idag.itmartin.dk",
                 "Velkomstsiden vises med 'Opret ny gruppe' og 'Gå til eksisterende gruppe'"),
            (2,  "Opret en ny gruppe med et navn og en adgangskode, og tryk 'Opret'",
                 "Gruppen oprettes og join-siden vises med titlen 'Hvem er du?'"),
            (3,  "Vælg dit navn i kortlisten (eller tryk 'Ny person' og skriv et navn), skriv adgangskoden og tryk 'Kom ind'",
                 "I dag-siden vises med dit navn i hilsenen øverst og en tom opgaveliste"),
            (4,  "Tryk '+ Tilføj →' og opret en opgave på opgavesiden",
                 "Opgaven vises på I dag-siden som et kort med badget '⚡ Ingen har taget den' og knappen 'Jeg tager den'"),
            (5,  "Tryk 'Jeg tager den' på en åben opgave",
                 "Opgavekortet skifter til '✋ [dit navn] klarer det' og knappen 'Markér færdig ✓' vises"),
            (6,  "Tryk 'Markér færdig ✓' på den opgave du har taget",
                 "Opgavekortet vises med '✅ Færdig af [dit navn]' og kortet tonet ned"),
            (7,  "Luk fanen og åbn idag.itmartin.dk igen",
                 "Appen husker din session og omdirigerer direkte til I dag-siden uden at vise join-formularen"),
            (8,  "Tryk tilbage-knappen fra I dag-siden (på telefon)",
                 "Du sendes IKKE tilbage til join-siden — appen reloader i stedet og forbliver på I dag"),
            (9,  "Åbn appen i en anden fane og vælg et andet navn med samme adgangskode",
                 "Den anden bruger kan se de samme opgaver og clajme dem — begge navne vises korrekt"),
            (10, "Tag en opgave i fane 1, skift til fane 2 uden at genindlæse",
                 "Fane 2 opdateres automatisk i realtid og viser den nye status uden reload"),
            (11, "Log ud ved at trykke ↩ øverst til højre",
                 "Join-siden vises igen og sessionen er slettet — du skal vælge navn og skrive kode igen"),
        ],
    };

    public static async Task SeedStepsAsync(TestHubDbContext db)
    {
        // Remove steps for any app not in ManagedSteps
        var managedNames = ManagedSteps.Keys.ToList();
        var unmanagedApps = await db.Apps
            .Include(a => a.Steps)
            .Where(a => !managedNames.Contains(a.Name) && a.Steps.Any())
            .ToListAsync();
        foreach (var app in unmanagedApps)
            db.Steps.RemoveRange(app.Steps);

        // Sync steps for managed apps
        var apps = await db.Apps
            .Include(a => a.Steps)
            .Where(a => managedNames.Contains(a.Name))
            .ToListAsync();

        var changed = unmanagedApps.Any(a => a.Steps.Any());

        foreach (var app in apps)
        {
            var def         = ManagedSteps[app.Name];
            var firstDef    = def[0];
            var firstActual = app.Steps.OrderBy(s => s.Order).FirstOrDefault();

            if (firstActual?.Instruction    == firstDef.Instruction &&
                firstActual?.ExpectedResult == firstDef.Expected    &&
                app.Steps.Count             == def.Count) continue;

            db.Steps.RemoveRange(app.Steps);
            db.Steps.AddRange(def.Select(d => new TestStep
            {
                AppEntryId     = app.Id,
                Order          = d.Order,
                Instruction    = d.Instruction,
                ExpectedResult = d.Expected,
            }));
            changed = true;
        }

        if (changed) await db.SaveChangesAsync();
    }
}
