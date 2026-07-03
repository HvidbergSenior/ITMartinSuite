namespace ITMartinTests.Flows;

/// <summary>BarTab, Auction, Market, Family Planner</summary>
[TestFixture]
[Category("Flow")]
public class SocialFlowTests : FlowTestBase
{
    // ── BarTab ────────────────────────────────────────────────────────────────

    [Test]
    public async Task BarTab_Index_Loads()
    {
        await GoOrSkip("https://bartab.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("BarTab index");
    }

    [Test]
    public async Task BarTab_Shows_Tabs_Or_Products()
    {
        await GoOrSkip("https://bartab.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasContent = await Page.Locator("ul li, table, .tab, .product, .bartab, button, h1, h2").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "BarTab should show tabs, products, or headings");
    }

    // ── Auction ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Auction_Index_Loads()
    {
        await GoOrSkip("https://auction.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Auction index");
    }

    [Test]
    public async Task Auction_Shows_Items_Or_Empty_State()
    {
        await GoOrSkip("https://auction.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasItems = await Page.Locator(".auction-item, .lot, ul li, table tr, .card, h1, h2").CountAsync() > 0;
        Assert.That(hasItems, Is.True, "Auction should show items or a heading");
    }

    // ── Market ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Market_Index_Loads()
    {
        await GoOrSkip("https://market.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Market index");
    }

    [Test]
    public async Task Market_Shows_Listings_Or_Empty_State()
    {
        await GoOrSkip("https://market.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasContent = await Page.Locator(".listing, .product, .market-item, ul li, table tr, .card, h1, h2").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "Market should show listings or a heading");
    }

    // ── Family Planner ────────────────────────────────────────────────────────

    [Test]
    public async Task FamilyPlanner_Index_Loads()
    {
        await GoOrSkip("https://idag.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Family Planner index");
    }

    [Test]
    public async Task FamilyPlanner_Shows_Calendar_Or_Tasks()
    {
        await GoOrSkip("https://idag.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasContent = await Page.Locator(".calendar, .task, .event, .dag, ul li, table, h1, h2").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "Family Planner should show calendar, tasks, or a heading");
    }
}
