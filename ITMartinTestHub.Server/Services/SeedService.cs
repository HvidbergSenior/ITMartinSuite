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
        ["Library Scan"] =
        [
            (1, "Åbn appen", "Kameraet starter og 'Scan hylde 1'-knappen vises"),
            (2, "Peg kameraet mod en bogreol og tryk 'Scan hylde 1'", "Et grønt 'Hylde 1 ✓'-mærke vises og knappen skifter til 'Scan hylde 2'"),
            (3, "Scan endnu en hylde ved at trykke 'Scan hylde 2'", "Et grønt 'Hylde 2 ✓'-mærke tilføjes ved siden af det første"),
            (4, "Tryk 'Færdig – analyser 2 hylde(r)'", "Spinner vises med teksten 'Analyserer 2 hylde(r)…' mens AI behandler begge"),
            (5, "Vent på at analysen er færdig", "Grøn besked vises: '✓ 2 hylde(r) gemt. Find bøgerne i Søg Bøger ↗'"),
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
            (1, "Åbn appen og opret en gruppe med navn, beskrivelse, invitationskode og admin PIN", "Gruppen oprettes og du sendes automatisk til tilmeldingssiden – ingen URL-slug skal indtastes"),
            (2, "Tilmeld dig gruppen med dit navn og invitationskoden", "Gruppens forside vises med dit navn som medlem"),
            (3, "Åbn Opslagstavlen og opret et opslag", "Opslaget vises på tavlen med navn og tidspunkt"),
            (4, "Åbn Kalenderen og opret en begivenhed", "Begivenheden vises i kalenderen"),
            (5, "Åbn Dokumenter og upload eller se dokumenter", "Dokumentlisten vises korrekt"),
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
            (1, "Åbn appen og tryk på 📷-knappen", "Kameraet starter og vejledningsskærmen vises"),
            (2, "Peg kameraet mod en genstand (f.eks. nøgler, briller eller tegnebog) og tryk for at optage", "AI analyserer billedet og foreslår automatisk genstandens navn og placering"),
            (3, "Bekræft eller ret navn og placering og tryk 'Gem'", "Genstanden gemmes og vises på forsiden med et thumbnail af billedet"),
            (4, "Gå tilbage til forsiden og søg efter genstanden ved navn", "Søgeresultatet vises med billede-thumbnail, navn og placering"),
            (5, "Registrer en genstand manuelt via tekstfeltet uden foto", "Genstanden gemmes og vises på listen uden thumbnail"),
        ],
        ["TestHub"] =
        [
            (1, "Åbn appen og skriv dit navn", "Forsiden vises med din tester-profil og dine aktive testopgaver"),
            (2, "Åbn en testopgave", "Opgavesiden vises med formålsbar øverst, testrin til venstre og 'Fejl & ideer' til højre"),
            (3, "Marker et trin som OK og et trin som Fejl", "OK-trin får grønt flueben, fejl-trin får rødt kryds – næste trin åbnes automatisk"),
            (4, "Tilføj en fejlrapport via 'Fejl & ideer'", "Feedback gemmes og vises i listen nedenfor med navn og tidspunkt"),
            (5, "Fuldfør alle trin og bekræft at 'Test færdig' vises", "'Test færdig'-banneret vises med tidsstempel"),
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
            var def        = ManagedSteps[app.Name];
            var firstDef   = def[0];
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
