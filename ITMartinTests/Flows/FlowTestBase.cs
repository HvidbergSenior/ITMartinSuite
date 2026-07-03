namespace ITMartinTests.Flows;

public abstract class FlowTestBase : PageTest
{
    protected async Task GoOrSkip(string url)
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

    protected async Task AssertBodyHasContent(string description)
    {
        var body = await Page.ContentAsync();
        Assert.That(body.Length, Is.GreaterThan(500), $"{description} — page body is suspiciously empty");
    }
}
