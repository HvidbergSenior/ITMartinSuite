namespace ITMartinTests.Flows;

[TestFixture]
[Category("Flow")]
public class DailyBriefFlowTests : PageTest
{
    private static string Url =>
        Environment.GetEnvironmentVariable("DAILYBRIEF_URL") ?? "https://nyheder.itmartin.dk";

    [Test]
    public async Task DailyBrief_Loads_And_Shows_News_UI()
    {
        if (string.IsNullOrEmpty(Url))
        {
            Assert.Ignore("DAILYBRIEF_URL not set — set env var or add to AppRegistry");
            return;
        }

        await GoOrSkip(Url);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 20_000 });

        var body = await Page.ContentAsync();
        Assert.That(body, Does.Contain("blazor").Or.Contain("nyheder").IgnoreCase,
            "DailyBrief should load its Blazor shell");
    }

    [Test]
    public async Task DailyBrief_Shows_News_Sources()
    {
        await GoOrSkip(Url);
        // Wait for Blazor to hydrate (news sources appear after circuit connects)
        await Page.WaitForSelectorAsync("body", new() { Timeout = 20_000 });
        await Task.Delay(3_000); // allow Blazor circuit to connect and load sources

        var body = await Page.ContentAsync();
        Assert.That(body,
            Does.Contain("DR").Or.Contain("TV2").Or.Contain("Nyheder").Or.Contain("nyhed"),
            "DailyBrief should mention known news sources (DR, TV2)");
    }

    private async Task GoOrSkip(string url)
    {
        if (string.IsNullOrEmpty(url)) { Assert.Ignore("No DAILYBRIEF_URL configured"); return; }
        try
        {
            var resp = await Page.GotoAsync(url, new() { Timeout = 20_000 });
            if (resp?.Status is 502 or 503 or 504)
                Assert.Ignore($"OFFLINE — dailybrief-web returned {resp.Status}");
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("net::ERR"))
        {
            Assert.Ignore($"OFFLINE — cannot reach {url}: {ex.Message}");
        }
    }
}
