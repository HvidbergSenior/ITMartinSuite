namespace ITMartinTests.Flows;

[TestFixture]
[Category("Flow")]
public class MagicFlowTests : FlowTestBase
{
    private const string CardBase       = "https://magic-card-pricing.itmartin.dk";
    private const string CollectionBase = "https://magic-collection.itmartin.dk";

    // ── Magic Card Pricing ────────────────────────────────────────────────────

    [Test]
    public async Task MagicCard_Index_Loads()
    {
        await GoOrSkip(CardBase);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Magic Card Pricing index");
    }

    [Test]
    public async Task MagicCard_Has_Search_Input()
    {
        await GoOrSkip(CardBase);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var input = await Page.Locator("input[type='search'], input[type='text'], input[placeholder*='kort'], input[placeholder*='card'], input").CountAsync();
        Assert.That(input, Is.GreaterThan(0), "Magic Card should have a card search input");
    }

    [Test]
    public async Task MagicCard_Search_Returns_Results_Or_Empty()
    {
        await GoOrSkip(CardBase);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var input = Page.Locator("input").First;
        if (await input.CountAsync() == 0)
        {
            Assert.Ignore("No search input found on Magic Card page");
            return;
        }

        await input.FillAsync("Lightning Bolt");
        await input.PressAsync("Enter");
        await Task.Delay(2_000);

        await AssertBodyHasContent("Magic Card search results");
    }

    // ── Magic Collection ──────────────────────────────────────────────────────

    [Test]
    public async Task MagicCollection_Index_Loads()
    {
        await GoOrSkip(CollectionBase);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Magic Collection index");
    }

    [Test]
    public async Task MagicCollection_Shows_Cards_Or_Login()
    {
        await GoOrSkip(CollectionBase);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasCards  = await Page.Locator(".card, .magic-card, table tr, ul li, img").CountAsync() > 0;
        var hasInput  = await Page.Locator("input").CountAsync() > 0;

        Assert.That(hasCards || hasInput, Is.True,
            "Magic Collection should show cards or a login/search form");
    }
}
