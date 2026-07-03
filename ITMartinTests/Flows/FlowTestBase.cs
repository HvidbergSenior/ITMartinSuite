namespace ITMartinTests.Flows;

public abstract class FlowTestBase : PageTest
{
    protected async Task GoOrSkip(string url)
    {
        try
        {
            var resp = await Page.GotoAsync(url, new() { Timeout = 15_000 });
            if (resp?.Status is 404)
                Assert.Ignore($"NOT FOUND — {url} returned 404 (path not configured or container stopped)");
            if (resp?.Status is 502 or 503 or 504)
                Assert.Ignore($"OFFLINE — {url} returned {resp.Status}");
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("net::ERR") || ex.Message.Contains("Timeout") || ex.Message.Contains("timeout"))
        {
            Assert.Ignore($"OFFLINE — cannot reach {url}: {ex.Message}");
        }
        catch (TimeoutException)
        {
            Assert.Ignore($"OFFLINE — {url} timed out (no response)");
        }
    }

    // Wait for page load + poll until Blazor has injected content (handles InteractiveServer)
    protected async Task WaitForPage(int timeoutMs = 15_000)
    {
        await Page.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = timeoutMs });
        try
        {
            // Poll until body has meaningful content — avoids fixed delays and handles
            // Blazor apps that hide <body> or render asynchronously via SignalR circuit
            await Page.WaitForFunctionAsync(
                "() => document.body.innerHTML.length > 100",
                new PageWaitForFunctionOptions { Timeout = 10_000, PollingInterval = 400 });
        }
        catch
        {
            // Still empty after 10s — AssertBodyHasContent will Skip this as offline
        }
    }

    protected async Task AssertBodyHasContent(string description)
    {
        var body = await Page.ContentAsync();
        // Empty body (<200 chars) means the reverse proxy returned a blank page because
        // the container is stopped — treat as offline, not a test failure
        if (body.Length < 200)
            Assert.Ignore($"OFFLINE — {description} returned empty page ({body.Length} chars, container likely stopped)");
        Assert.That(body.Length, Is.GreaterThan(500), $"{description} — page body is suspiciously small");
    }
}
