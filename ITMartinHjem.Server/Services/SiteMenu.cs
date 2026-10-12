using ITMartinHjem.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinHjem.Server.Services;

// The top menu, the same everywhere (Blazor layout + /api/menu for the hand-made pilot page):
// Forside first, then the pages marked "Vis i menuen" by their Rækkefølge, with "Mine apps" slotted in
// at MineAppsSort. Since 2026-10-07 (user) the menu is just Forside + Mine apps; Om mig and Bliv pilot are
// choices on the front page, not tabs.
public static class SiteMenu
{
    public const int MineAppsSort = 50;
    // "📅 I dag" tab (user 2026-10-12: the day plan box moves off the front page into its own tab) - the ITKolibrien page.
    public const int TodaySort = 40;  // user 2026-10-05: Forside, Hvad laver ITKolibrien, Om mig, Særlige behov, Priser, Mine apps (50), Følg samarbejdet, Bliv pilot

    public sealed record Item(string Title, string Href, bool Pilot = false);

    public static async Task<List<Item>> BuildAsync(HjemDb db)
    {
        var pages = await db.Pages.AsNoTracking().Where(p => p.InMenu)
            .Select(p => new { p.Sort, p.Title, p.Slug }).ToListAsync();
        var middle = pages.Select(p => (Sort: p.Sort, Item: new Item(p.Title, "/" + p.Slug)))
            .Append((Sort: TodaySort, Item: new Item("📅 I dag", "/" + Components.NowCard.PageSlug)))
            .Append((Sort: MineAppsSort, Item: new Item("📱 Mine apps", "/mine-apps")))
            .Append((Sort: MineAppsSort + 1, Item: new Item("💬 Spørgsmål", "/spoergsmaal")))   // Q&A tab (user 2026-10-12: "where is the Q/A?")
            .OrderBy(x => x.Sort).ThenBy(x => x.Item.Title)
            .Select(x => x.Item);
        return [new Item("🏠 Forside", "/"), .. middle];
    }
}
