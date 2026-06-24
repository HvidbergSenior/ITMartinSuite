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
            new AppEntry { Name = "Family Planner",  Icon = "👨‍👩‍👧", Url = "https://family.itmartin.dk",           Description = "Familieplanlægning og koordinering",  SortOrder = 10 },
            new AppEntry { Name = "Market",          Icon = "🛍️", Url = "https://market.itmartin.dk",           Description = "Markedsplads",                         SortOrder = 11 },
            new AppEntry { Name = "R6 Assistant",    Icon = "🎮", Url = "https://r6.itmartin.dk",               Description = "Rainbow Six Siege-assistent",          SortOrder = 12 },
            new AppEntry { Name = "Portal",          Icon = "🏠", Url = "https://martin.itmartin.dk",           Description = "Hovedportal og indeks",                SortOrder = 13 },
            new AppEntry { Name = "Library Scan",    Icon = "📷", Url = "https://scan-books.itmartin.dk",       Description = "Scan en bogreol med kameraet – AI identificerer bøgerne",   SortOrder = 14 },
            new AppEntry { Name = "Library Search",  Icon = "🔍", Url = "https://search-books.itmartin.dk",    Description = "Søg i bøger identificeret fra scannede bogreoler",          SortOrder = 15 },
            new AppEntry { Name = "Club",            Icon = "🏛️", Url = "https://lions-club.itmartin.dk",       Description = "Gruppeorganisator med opslagstavle, kalender og dokumenter", SortOrder = 16 },
            new AppEntry { Name = "Magic Scan",      Icon = "🃏", Url = "https://magic-card-pricing.itmartin.dk", Description = "AI-drevet MTG-kortscanner med prisopslag",              SortOrder = 17 },
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
        ["Library Scan"] =
        [
            (1, "Åbn appen", "Kameraet starter automatisk og 'Scan Hylde'-skærmen vises"),
            (2, "Peg kameraet mod en bogreol", "Kamerabilledet viser hylden klart og tydeligt"),
            (3, "Tryk på 'Capture Shelf'", "Spinner vises med teksten 'Analyzing shelf…'"),
            (4, "Vent på at analysen er færdig", "Resultater vises under 'Recent Scans' med bognavne og forfattere"),
            (5, "Scan endnu en hylde og bekræft at den vises som Shelf 2", "Andet scan vises øverst på listen med korrekt hyldenummer"),
        ],
        ["Library Search"] =
        [
            (1, "Åbn appen", "Søgesiden indlæses med det samlede antal elementer vist"),
            (2, "Skriv et bogtitel fra en tidligere scannet hylde", "Matchende resultater vises straks"),
            (3, "Ryd søgningen og skriv et forfatternavn", "Bøger af den pågældende forfatter vises"),
            (4, "Skriv noget der ikke matcher noget", "Beskeden 'No items match your search' vises"),
        ],
        ["Club"] =
        [
            (1, "Åbn appen", "Appen indlæses og viser gruppeliste eller tilmeldingsskærm"),
            (2, "Tilmeld dig eller gå ind i en eksisterende gruppe", "Gruppens forside vises med korrekt medlemsantal"),
            (3, "Åbn Opslagstavlen", "Opslagstavlen med opgaver eller aktiviteter vises"),
            (4, "Åbn Kalenderen", "Kalendervisning indlæses med begivenheder eller tom tilstand"),
            (5, "Åbn Dokumenter", "Dokumentliste eller upload-mulighed vises"),
        ],
        ["Magic Scan"] =
        [
            (1, "Åbn appen", "Scannersiden indlæses direkte – ingen omdirigering"),
            (2, "Vælg et sæt fra rullelisten (f.eks. søg 'MOM')", "Sæt er valgt og et grønt bekræftelsesbanner vises"),
            (3, "Tryk på 'Start Camera'", "Kameraet starter"),
            (4, "Hold et Magic-kort foran kameraet og tryk 'Scan Card'", "Spinner vises mens AI behandler kortet"),
            (5, "Vent på resultat", "Kortnavn, sæt, samler-nummer og EUR-pris vises"),
        ],
        ["FindIt"] =
        [
            (1, "Åbn appen", "Forsiden vises med liste over gemte genstande eller en tom tilstand"),
            (2, "Opret en ny genstand (f.eks. 'Nøgler') og angiv en placering", "Genstanden gemmes og vises på listen"),
            (3, "Tryk på en genstand for at se detaljer", "Detailvisning åbnes med placering og eventuelle noter"),
            (4, "Rediger placeringen på en eksisterende genstand", "Den nye placering gemmes og vises korrekt"),
            (5, "Søg efter en genstand ved navn", "Korrekte søgeresultater vises"),
        ],
    };

    public static async Task SeedStepsAsync(TestHubDbContext db)
    {
        var appNames = ManagedSteps.Keys.ToList();
        var apps = await db.Apps
            .Include(a => a.Steps)
            .Where(a => appNames.Contains(a.Name))
            .ToListAsync();

        var changed = false;

        foreach (var app in apps)
        {
            var def = ManagedSteps[app.Name];
            var firstExpected = def[0].Instruction;
            var firstActual   = app.Steps.OrderBy(s => s.Order).FirstOrDefault()?.Instruction;

            // Skip if already seeded with the correct Danish steps
            if (firstActual == firstExpected && app.Steps.Count == def.Count) continue;

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
