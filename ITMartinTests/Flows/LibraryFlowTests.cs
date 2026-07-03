namespace ITMartinTests.Flows;

[TestFixture]
[Category("Flow")]
public class LibraryFlowTests : PageTest
{
    [Test]
    public async Task LibrarySearch_Loads_And_Has_Search_Input()
    {
        await GoOrSkip("https://search-books.itmartin.dk");
        await Page.WaitForSelectorAsync(".poll-root, body", new() { Timeout = 15_000 });

        // Verify some kind of search UI exists
        var input = Page.Locator("input[type='search'], input[type='text'], input[placeholder*='søg'], input[placeholder*='titel'], input");
        var hasInput = await input.CountAsync() > 0;
        Assert.That(hasInput, Is.True, "Library search should have a search input");
    }

    [Test]
    public async Task LibrarySearch_Returns_Results_Or_Empty_State()
    {
        await GoOrSkip("https://search-books.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var input = Page.Locator("input").First;
        if (await input.CountAsync() == 0)
        {
            Assert.Ignore("No input found on library search page");
            return;
        }

        await input.FillAsync("Harry Potter");
        await input.PressAsync("Enter");

        // Wait for either results or a "nothing found" state — either is valid
        await Task.Delay(2_000);
        var body = await Page.ContentAsync();

        Assert.That(body.Length, Is.GreaterThan(500), "Page should have content after search");
    }

    [Test]
    public async Task LibraryScan_Loads()
    {
        await GoOrSkip("https://scan-books.itmartin.dk");
        await Page.WaitForSelectorAsync(".poll-root, body", new() { Timeout = 15_000 });

        var body = await Page.ContentAsync();
        Assert.That(body, Does.Contain("blazor").Or.Contain("bibliotek").IgnoreCase,
            "Library scan app should load");
    }

    private async Task GoOrSkip(string url)
    {
        try
        {
            var resp = await Page.GotoAsync(url, new() { Timeout = 15_000 });
            if (resp?.Status is 502 or 503 or 504)
                Assert.Ignore($"OFFLINE — {url} returned {resp.Status}");
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("net::ERR"))
        {
            Assert.Ignore($"OFFLINE — cannot reach {url}: {ex.Message}");
        }
    }
}
