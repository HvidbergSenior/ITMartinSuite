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
                 "Gruppen oprettes og join-siden for den nye gruppe vises med gruppe-URL"),
            (3,  "Skriv dit navn og adgangskoden og tryk 'Tilmeld'",
                 "Boardet vises med dit navn øverst og en tom opgaveliste"),
            (4,  "Tilføj en opgave via tekstfeltet og tryk 'Tilføj'",
                 "Opgaven vises på listen med 'Tag'-knap og ingen claimet"),
            (5,  "Tryk 'Tag' på opgaven",
                 "Dit navn vises på opgaven og knappen skifter til 'Aflever'"),
            (6,  "Tryk 'Aflever' på opgaven",
                 "Opgaven markeres som færdig med tidsstempel og flyttes til afsluttet-listen"),
            (7,  "Tryk 'Gør det i morgen' på en taget men ikke afleveret opgave",
                 "En toast-besked med en fast dansk tekst vises — opgaven udsættes IKKE"),
            (8,  "Luk fanen og åbn idag.itmartin.dk igen",
                 "Appen husker din gruppe og omdirigerer automatisk til boardet uden at du skal skrive gruppenavnet"),
            (9,  "Åbn appen i en anden fane og tilmeld et andet navn med samme adgangskode",
                 "Det andet navn vises som andet medlem på boardet"),
            (10, "Åbn Påmindelser og opret en påmindelse til i morgen",
                 "Påmindelsen gemmes og vises på listen med dato"),
            (11, "Åbn Chat og send en besked",
                 "Beskeden vises i chatten med dit navn og tidspunkt"),
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
