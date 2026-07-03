namespace ITMartinTests.Flows;

[TestFixture]
[Category("Flow")]
public class LibraryFlowTests : FlowTestBase
{
    [Test]
    public async Task LibrarySearch_Loads_And_Has_Search_Input()
    {
        await GoOrSkip("https://search-books.itmartin.dk");
        await WaitForPage();
        await AssertBodyHasContent("Library Search index");

        var input = Page.Locator("input[type='search'], input[type='text'], input[placeholder*='søg'], input[placeholder*='titel'], input");
        var hasInput = await input.CountAsync() > 0;
        Assert.That(hasInput, Is.True, "Library search should have a search input");
    }

    [Test]
    public async Task LibrarySearch_Returns_Results_Or_Empty_State()
    {
        await GoOrSkip("https://search-books.itmartin.dk");
        await WaitForPage();
        await AssertBodyHasContent("Library Search");

        var input = Page.Locator("input").First;
        if (await input.CountAsync() == 0)
        {
            Assert.Ignore("No input found on library search page");
            return;
        }

        await input.FillAsync("Harry Potter");
        await input.PressAsync("Enter");
        await Task.Delay(2_000);

        var body = await Page.ContentAsync();
        Assert.That(body.Length, Is.GreaterThan(500), "Page should have content after search");
    }

    [Test]
    public async Task LibraryScan_Loads()
    {
        await GoOrSkip("https://scan-books.itmartin.dk");
        await WaitForPage();
        await AssertBodyHasContent("Library Scan");
    }
}
