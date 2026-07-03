namespace ITMartinTests;

public record AppDef(
    string Name,
    string ContainerName,
    string Url,
    bool   AlwaysOn = false
);

public static class AppRegistry
{
    static string E(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) ?? fallback;

    // All apps, ordered by importance.
    // Url defaults to live itmartin.dk — same from GitHub Actions and your local PC.
    // AlwaysOn=true  → no docker profile, always running → test failure counts
    // AlwaysOn=false → profile:manual → if offline the test is Skipped (yellow), not Failed (red)
    public static readonly AppDef[] All =
    [
        // ── Always-on ─────────────────────────────────────────────────────────
        new("Daily Brief",        "dailybrief-web",       E("DAILYBRIEF_URL", "https://dagligenyheder.itmartin.dk")),

        // ── Real shops / real users ────────────────────────────────────────────
        new("Kvittering",         "receipt-web",          "https://kvittering.itmartin.dk"),
        new("Library Scan",       "library-web",          "https://scan-books.itmartin.dk"),
        new("Library Search",     "library-search-web",   "https://search-books.itmartin.dk"),
        new("ADHD FindIt",        "adhd-web",             "https://adhd.itmartin.dk"),
        new("Magic Card",         "magic-web",            "https://magic-card-pricing.itmartin.dk"),
        new("Magic Collection",   "magic-collection-web", "https://magic-collection.itmartin.dk"),

        // ── Social / hobby ────────────────────────────────────────────────────
        new("Poll / Stem",        "poll-web",             "https://stem.itmartin.dk"),
        new("Gallery",            "gallery-web",          "https://gallery.itmartin.dk"),
        new("Musik",              "musik-web",            "https://musik.itmartin.dk"),
        new("Musik Studio",       "musik-studio-web",     "https://studio.itmartin.dk"),
        new("Club (Lions)",       "club-web",             "https://lions-club.itmartin.dk"),
        new("Club (R6 Oldboyz)",  "club-web",             "https://r6OldBoyz.itmartin.dk"),
        new("BarTab",             "bartab-web",           "https://bartab.itmartin.dk"),
        new("Auction",            "auction-web",          "https://auction.itmartin.dk"),
        new("Market",             "market-web",           "https://market.itmartin.dk"),
        new("Family Planner",     "family-web",           "https://idag.itmartin.dk"),

        // ── Tools ─────────────────────────────────────────────────────────────
        new("Portal",             "index-web",            "https://all-apps.itmartin.dk"),
        new("FileSorter",         "filesorter-web",       "https://filesorter.itmartin.dk"),
        new("Budget",             "budget-web",           "https://budget.itmartin.dk"),
        new("Magazine",           "magazine-web",         "https://magazine.itmartin.dk"),
        new("Magazine Search",    "magazine-search-web",  "https://magazine-search.itmartin.dk"),
        new("Image Generator",    "imagegen-web",         "https://billedbehandling.itmartin.dk"),
        new("Cloud Overblik",     "cloudoverblik-web",    "https://cloudoverblik.itmartin.dk"),
        new("Test Hub",           "testhub-web",          "https://test.itmartin.dk"),
        new("Scan / Find Varer",  "scan-web",             "https://find-varer.itmartin.dk"),
        new("Upload",             "upload-web",           "https://upload.itmartin.dk"),
        new("R6 Assistant",       "r6assistant-web",      "https://r6.itmartin.dk"),
        new("R6 Intel",           "r6intel-web",          "https://r6intel.itmartin.dk"),
        new("Media Seller",       "mediaseller-web",      "https://mediaseller.itmartin.dk"),
    ];
}
