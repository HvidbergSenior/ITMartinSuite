using ITMartinTestHub.Server.Data;
using ITMartinTestHub.Server.Data.Entities;

namespace ITMartinTestHub.Server.Services;

public static class SeedService
{
    public static async Task SeedAppsAsync(TestHubDbContext db)
    {
        if (db.Apps.Any()) return;

        var apps = new[]
        {
            new AppEntry { Name = "FileSorter",     Icon = "📦", Url = "https://filesorter.itmartin.dk",  Description = "Media cleanup and enhancement pipeline", SortOrder = 1 },
            new AppEntry { Name = "Gallery",         Icon = "🎬", Url = "https://gallery.itmartin.dk",     Description = "Media viewer and collections",           SortOrder = 2 },
            new AppEntry { Name = "Budget",          Icon = "💰", Url = "https://budget.itmartin.dk",      Description = "Personal finance tracker",               SortOrder = 3 },
            new AppEntry { Name = "Receipt",         Icon = "🧾", Url = "https://receipt.itmartin.dk",     Description = "Receipt scanner and organizer",          SortOrder = 4 },
            new AppEntry { Name = "Library",         Icon = "📚", Url = "https://library.itmartin.dk",     Description = "Book and movie tracker",                 SortOrder = 5 },
            new AppEntry { Name = "BarTab",          Icon = "🍺", Url = "https://bartab.itmartin.dk",      Description = "Group bar bill splitter with AI drinks", SortOrder = 6 },
            new AppEntry { Name = "Auction",         Icon = "🔨", Url = "https://auction.itmartin.dk",     Description = "Live bidding for collectibles",          SortOrder = 7 },
            new AppEntry { Name = "Magic",           Icon = "✨", Url = "https://magic.itmartin.dk",       Description = "AI tools",                               SortOrder = 8 },
            new AppEntry { Name = "FindIt",          Icon = "📍", Url = "https://adhd.itmartin.dk",        Description = "Item location tracker",                  SortOrder = 9 },
            new AppEntry { Name = "Family Planner",  Icon = "👨‍👩‍👧", Url = "https://family.itmartin.dk",      Description = "Family planning and coordination",       SortOrder = 10 },
            new AppEntry { Name = "Market",          Icon = "🛍️", Url = "https://market.itmartin.dk",      Description = "Marketplace",                            SortOrder = 11 },
            new AppEntry { Name = "R6 Assistant",    Icon = "🎮", Url = "https://r6.itmartin.dk",          Description = "Rainbow Six Siege assistant",            SortOrder = 12 },
            new AppEntry { Name = "Portal",          Icon = "🏠", Url = "https://martin.itmartin.dk",      Description = "Main portal and index",                  SortOrder = 13 },
        };

        db.Apps.AddRange(apps);
        await db.SaveChangesAsync();
    }
}
