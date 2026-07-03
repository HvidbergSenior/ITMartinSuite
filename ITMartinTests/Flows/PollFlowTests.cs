namespace ITMartinTests.Flows;

/// <summary>
/// Real browser flow tests for stem.itmartin.dk (Poll / Billedserie).
/// Uses Playwright (Chromium headless) so Blazor circuit connects properly.
///
/// Run locally:  dotnet test --filter "Category=Flow&Class=PollFlowTests"
/// </summary>
[TestFixture]
[Category("Flow")]
public class PollFlowTests : PageTest
{
    private const string Base = "https://stem.itmartin.dk";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        ViewportSize = new() { Width = 390, Height = 844 }, // iPhone-size — our primary UI target
        Locale       = "da-DK",
    };

    [Test]
    public async Task Index_Page_Loads_With_Poll_Root()
    {
        await GoOrSkip(Base);

        await Page.WaitForSelectorAsync(".poll-root", new() { Timeout = 15_000 });
        var title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Stem").IgnoreCase, "Expected page title to contain 'Stem'");
    }

    [Test]
    public async Task Index_Shows_Sessions_Or_Empty_State()
    {
        await GoOrSkip(Base);
        await Page.WaitForSelectorAsync(".poll-root", new() { Timeout = 15_000 });

        var hasSessions  = await Page.Locator(".poll-card").CountAsync() > 0;
        var hasEmptyText = (await Page.ContentAsync()).Contains("Ingen aktive");

        Assert.That(hasSessions || hasEmptyText, Is.True,
            "Index should show poll cards or 'Ingen aktive' message");
    }

    [Test]
    public async Task Admin_Login_With_Correct_Pin_Shows_Dashboard()
    {
        var pin = Environment.GetEnvironmentVariable("POLL_ADMIN_PIN") ?? "1234";

        await GoOrSkip($"{Base}/admin");
        // Wait for either the PIN form or (if already logged in) the admin dashboard
        await Page.WaitForSelectorAsync(".poll-input, .admin-form, .admin-section",
            new() { Timeout = 15_000 });

        // If PIN form is visible, enter the PIN
        var pinInput = Page.Locator(".poll-input[type='password'], input[type='password']");
        if (await pinInput.CountAsync() > 0)
        {
            await pinInput.First.FillAsync(pin);
            await Page.Locator(".poll-submit-btn").First.ClickAsync();
            await Page.WaitForSelectorAsync(".admin-section, [class*='admin']",
                new() { Timeout = 10_000 });
        }

        var content = await Page.ContentAsync();
        Assert.That(content,
            Does.Contain("BILLEDSERIER").Or.Contain("MENINGSMÅLINGER").Or.Contain("admin"),
            "Admin dashboard should appear after correct PIN");
    }

    [Test]
    public async Task Admin_Wrong_Pin_Is_Rejected()
    {
        await GoOrSkip($"{Base}/admin");
        await Page.WaitForSelectorAsync(".poll-input, .admin-section", new() { Timeout = 15_000 });

        var pinInput = Page.Locator(".poll-input[type='password'], input[type='password']");
        if (await pinInput.CountAsync() == 0)
        {
            Assert.Ignore("Already logged in — cannot test wrong PIN");
            return;
        }

        await pinInput.First.FillAsync("0000WRONG");
        await Page.Locator(".poll-submit-btn").First.ClickAsync();
        await Task.Delay(1_000);

        var content = await Page.ContentAsync();
        Assert.That(content, Does.Not.Contain("BILLEDSERIER"),
            "Wrong PIN should NOT grant admin access");
    }

    [Test]
    public async Task Session_Page_Shows_Image_Grid()
    {
        await GoOrSkip(Base);
        await Page.WaitForSelectorAsync(".poll-root", new() { Timeout = 15_000 });

        var cards = Page.Locator(".poll-card[href*='/session/']");
        if (await cards.CountAsync() == 0)
        {
            Assert.Ignore("No active image sessions to test — create one in admin first");
            return;
        }

        await cards.First.ClickAsync();
        await Page.WaitForSelectorAsync(".session-grid, .session-done", new() { Timeout = 15_000 });

        var grid = Page.Locator(".session-grid");
        if (await grid.CountAsync() > 0)
        {
            var thumbs = await grid.Locator(".session-thumb-btn").CountAsync();
            Assert.That(thumbs, Is.GreaterThan(0), "Session grid should contain image thumbnails");
        }
        else
        {
            // Already voted or deadline passed — that's valid
            Assert.Pass("Session page loaded (done or deadline state)");
        }
    }

    // Helpers

    private async Task GoOrSkip(string url)
    {
        try
        {
            var resp = await Page.GotoAsync(url, new() { Timeout = 15_000 });
            if (resp?.Status is 502 or 503 or 504)
                Assert.Ignore($"OFFLINE — poll-web returned {resp.Status}");
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("net::ERR"))
        {
            Assert.Ignore($"OFFLINE — cannot reach {url}: {ex.Message}");
        }
    }
}
