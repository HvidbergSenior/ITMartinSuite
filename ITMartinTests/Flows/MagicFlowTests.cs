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
        await WaitForPage();
        await AssertBodyHasContent("Magic Card Pricing index");
    }

    [Test]
    public async Task MagicCard_Has_Search_Input()
    {
        await GoOrSkip(CardBase);
        await WaitForPage();

        var hasInput = await Page.Locator("input").CountAsync() > 0;
        Assert.That(hasInput, Is.True, "Magic Card should have a card search input");
    }

    [Test]
    public async Task MagicCard_Search_Returns_Results_Or_Empty()
    {
        await GoOrSkip(CardBase);
        await WaitForPage();

        var input = Page.Locator("input[type='text'], input[type='search'], input:not([type='radio']):not([type='checkbox']):not([type='hidden'])").First;
        if (await input.CountAsync() == 0)
        {
            Assert.Ignore("No text search input found on Magic Card page");
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
        await WaitForPage();
        await AssertBodyHasContent("Magic Collection index");
    }

    [Test]
    public async Task MagicCollection_Shows_Cards_Or_Login()
    {
        await GoOrSkip(CollectionBase);
        await WaitForPage();
        await AssertBodyHasContent("Magic Collection");

        var hasContent = await Page.Locator(".card, .magic-card, table tr, ul li, img, input, button, h1, h2, div[class]").CountAsync() > 0;

        Assert.That(hasContent, Is.True,
            "Magic Collection should show cards or a login/search form");
    }
}
