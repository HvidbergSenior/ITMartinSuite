using ITMartinTestHub.Server.Data;
using ITMartinTestHub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinTestHub.Server.Services;

public static class SeedService
{
    public static async Task SeedAppsAsync(TestHubDbContext db)
    {
        var existingNames = (await db.Apps.Select(a => a.Name).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var apps = new[]
        {
            new AppEntry { Name = "FileSorter",      Icon = "📦", Url = "https://filesorter.itmartin.dk",       Description = "Media cleanup and enhancement pipeline", SortOrder = 1 },
            new AppEntry { Name = "Gallery",          Icon = "🎬", Url = "https://gallery.itmartin.dk",          Description = "Media viewer and collections",           SortOrder = 2 },
            new AppEntry { Name = "Budget",           Icon = "💰", Url = "https://budget.itmartin.dk",           Description = "Personal finance tracker",               SortOrder = 3 },
            new AppEntry { Name = "Receipt",          Icon = "🧾", Url = "https://receipt.itmartin.dk",          Description = "Receipt scanner and organizer",          SortOrder = 4 },
            new AppEntry { Name = "Library",          Icon = "📚", Url = "https://library.itmartin.dk",          Description = "Book and movie tracker",                 SortOrder = 5 },
            new AppEntry { Name = "BarTab",           Icon = "🍺", Url = "https://bartab.itmartin.dk",           Description = "Group bar bill splitter with AI drinks", SortOrder = 6 },
            new AppEntry { Name = "Auction",          Icon = "🔨", Url = "https://auction.itmartin.dk",          Description = "Live bidding for collectibles",          SortOrder = 7 },
            new AppEntry { Name = "Magic",            Icon = "✨", Url = "https://magic.itmartin.dk",            Description = "AI tools",                               SortOrder = 8 },
            new AppEntry { Name = "FindIt",           Icon = "📍", Url = "https://adhd.itmartin.dk",             Description = "Item location tracker",                  SortOrder = 9 },
            new AppEntry { Name = "Family Planner",   Icon = "👨‍👩‍👧", Url = "https://family.itmartin.dk",           Description = "Family planning and coordination",       SortOrder = 10 },
            new AppEntry { Name = "Market",           Icon = "🛍️", Url = "https://market.itmartin.dk",           Description = "Marketplace",                            SortOrder = 11 },
            new AppEntry { Name = "R6 Assistant",     Icon = "🎮", Url = "https://r6.itmartin.dk",               Description = "Rainbow Six Siege assistant",            SortOrder = 12 },
            new AppEntry { Name = "Portal",           Icon = "🏠", Url = "https://martin.itmartin.dk",           Description = "Main portal and index",                  SortOrder = 13 },
            new AppEntry
            {
                Name = "Library Scan", Icon = "📷", Url = "https://library.itmartin.dk",
                Description = "Scan a bookcase shelf with the camera — AI identifies the books", SortOrder = 14,
                Steps =
                [
                    new TestStep { Order = 1, Instruction = "Open the app", ExpectedResult = "Camera starts automatically and the shelf capture view is shown" },
                    new TestStep { Order = 2, Instruction = "Point the camera at a bookcase shelf", ExpectedResult = "Camera feed shows shelf clearly" },
                    new TestStep { Order = 3, Instruction = "Tap 'Capture Shelf'", ExpectedResult = "Spinner appears with 'Analyzing shelf…' message" },
                    new TestStep { Order = 4, Instruction = "Wait for analysis to complete", ExpectedResult = "Results appear in 'Recent Scans' with identified book titles and authors" },
                    new TestStep { Order = 5, Instruction = "Capture a second shelf and verify it is added as Shelf 2", ExpectedResult = "Second scan appears at the top of the list" },
                ]
            },
            new AppEntry
            {
                Name = "Library Search", Icon = "🔍", Url = "https://library-search.itmartin.dk",
                Description = "Search books identified from scanned bookcases", SortOrder = 15,
                Steps =
                [
                    new TestStep { Order = 1, Instruction = "Open the app", ExpectedResult = "Search page loads showing total item count" },
                    new TestStep { Order = 2, Instruction = "Type a book title from a previously scanned shelf", ExpectedResult = "Matching results appear immediately" },
                    new TestStep { Order = 3, Instruction = "Clear the search and type an author name", ExpectedResult = "Books by that author are shown" },
                    new TestStep { Order = 4, Instruction = "Type something that matches nothing", ExpectedResult = "'No items match your search' message is shown" },
                ]
            },
            new AppEntry
            {
                Name = "Club", Icon = "🏛️", Url = "https://club.itmartin.dk",
                Description = "Group organizer with boards, calendar, and documents", SortOrder = 16,
                Steps =
                [
                    new TestStep { Order = 1, Instruction = "Open the app", ExpectedResult = "App loads — shows group list or join prompt" },
                    new TestStep { Order = 2, Instruction = "Join or enter an existing group", ExpectedResult = "Group home page is shown with member count" },
                    new TestStep { Order = 3, Instruction = "Open the Board", ExpectedResult = "Board with assignments or tasks is shown" },
                    new TestStep { Order = 4, Instruction = "Open the Calendar", ExpectedResult = "Calendar view loads with events or empty state" },
                    new TestStep { Order = 5, Instruction = "Open Documents", ExpectedResult = "Documents list or upload option is shown" },
                ]
            },
            new AppEntry
            {
                Name = "Magic Scan", Icon = "🃏", Url = "https://magic.itmartin.dk",
                Description = "AI-powered MTG card scanner with price lookup", SortOrder = 17,
                Steps =
                [
                    new TestStep { Order = 1, Instruction = "Open the app", ExpectedResult = "Scanner page loads directly — no redirect" },
                    new TestStep { Order = 2, Instruction = "Select a set from the dropdown (e.g. search 'MOM')", ExpectedResult = "Set is selected and a green confirmation banner appears" },
                    new TestStep { Order = 3, Instruction = "Tap 'Start Camera'", ExpectedResult = "Camera feed starts" },
                    new TestStep { Order = 4, Instruction = "Hold a Magic card in front of the camera and tap 'Scan Card'", ExpectedResult = "Spinner appears while AI processes" },
                    new TestStep { Order = 5, Instruction = "Wait for result", ExpectedResult = "Card name, set, collector number and EUR price are shown" },
                ]
            },
        };

        var toAdd = apps.Where(a => !existingNames.Contains(a.Name)).ToList();
        if (toAdd.Count == 0) return;

        db.Apps.AddRange(toAdd);
        await db.SaveChangesAsync();
    }
}
