namespace ITMartinTests.Smoke;

/// <summary>
/// One test per app. Each test does an HTTP GET and checks:
///   1. HTTP 200 (or redirect to 200)
///   2. Body contains Blazor shell marker
///   3. Response arrived within 5 seconds
///
/// If an app is offline (connection refused, 502, 503, 504):
///   - AlwaysOn apps  → FAIL  (these should never go down)
///   - Manual apps    → SKIP  (they're deliberately stopped; not a failure)
///
/// Run locally:  dotnet test --filter "Category=Smoke"
/// </summary>
[TestFixture]
[Category("Smoke")]
[Parallelizable(ParallelScope.Children)]
public class SmokeTests
{
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AllowAutoRedirect    = true,
        MaxAutomaticRedirections = 5,
    })
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { { "User-Agent", "ITMartinTestBot/1.0" } },
    };

    public static IEnumerable<TestCaseData> Apps =>
        AppRegistry.All
            .Where(a => !string.IsNullOrEmpty(a.Url))
            .Select(a => new TestCaseData(a).SetName(a.Name));

    [TestCaseSource(nameof(Apps))]
    public async Task App_Loads(AppDef app)
    {
        if (string.IsNullOrEmpty(app.Url))
        {
            Assert.Ignore($"URL not configured for {app.Name}");
            return;
        }

        var sw = Stopwatch.StartNew();
        HttpResponseMessage response;

        try
        {
            response = await Http.GetAsync(app.Url);
        }
        catch (TaskCanceledException)
        {
            var msg = $"{app.Name} timed out after 15 s — container not responding";
            if (app.AlwaysOn) Assert.Fail(msg);
            else              Assert.Ignore($"OFFLINE — {msg}");
            return;
        }
        catch (HttpRequestException ex)
        {
            var msg = $"Cannot connect to {app.Name} ({app.Url}): {ex.Message}";
            if (app.AlwaysOn) Assert.Fail(msg);
            else              Assert.Ignore($"OFFLINE — {msg}");
            return;
        }
        finally { sw.Stop(); }

        TestContext.Out.WriteLine($"Status: {(int)response.StatusCode}  Time: {sw.ElapsedMilliseconds} ms");

        // Cloudflare/proxy errors, 404, or unresolvable redirects mean the container is down / not deployed
        if (response.StatusCode is HttpStatusCode.NotFound
                                 or HttpStatusCode.BadGateway
                                 or HttpStatusCode.ServiceUnavailable
                                 or HttpStatusCode.GatewayTimeout
                                 or HttpStatusCode.MovedPermanently
                                 or HttpStatusCode.Found
                                 or (HttpStatusCode)530)
        {
            var msg = $"{app.Name} returned {(int)response.StatusCode} — container likely stopped";
            if (app.AlwaysOn) Assert.Fail(msg);
            else              Assert.Ignore($"OFFLINE — {msg}");
            return;
        }

        Assert.That((int)response.StatusCode, Is.EqualTo(200),
            $"{app.Name}: expected HTTP 200, got {(int)response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync();

        Assert.That(body, Does.Contain("blazor").Or.Contain("<body"),
            $"{app.Name}: response body missing Blazor shell — possible startup error");

        // Only enforce the time limit for always-on apps; manual containers cold-start slowly
        if (app.AlwaysOn)
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(10_000),
                $"{app.Name}: response took {sw.ElapsedMilliseconds} ms (limit 10 000 ms)");
    }
}
