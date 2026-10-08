using System.Net;
using System.Net.Http.Json;

namespace ITMartinTests.Flows;

/// <summary>
/// PC-tjek (pctjek.itmartin.dk) as anonymous visitors. Since 2026-10-08 every browser has its own secret code and only
/// sees - and can only delete - its own saved runs. Before, /historik listed every device of every visitor.
/// Writes test runs under random device names (harmless: nobody else can see them).
///
/// Run locally:  TJEK_BASE=http://localhost:5136 dotnet test --filter "FullyQualifiedName~TjekFlowTests"
/// </summary>
[TestFixture]
[Category("Flow")]
public class TjekFlowTests : FlowTestBase
{
    private static string Base => Environment.GetEnvironmentVariable("TJEK_BASE") ?? "https://pctjek.itmartin.dk";

    public override BrowserNewContextOptions ContextOptions() => new() { ViewportSize = new() { Width = 390, Height = 844 }, Locale = "da-DK" };

    /// <summary>Opens the page in a page of its own context, names the device, runs the check and saves it.</summary>
    private static async Task SaveRun(IPage page, string device)
    {
        await page.GotoAsync(Base);
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined && window.tjek", null, new() { Timeout = 15_000 });
        await page.WaitForTimeoutAsync(1000);
        await page.Locator("input.device-name").FillAsync(device);
        await page.Locator("input.device-name").DispatchEventAsync("change");
        await page.Locator("button.btn-primary", new() { HasTextRegex = new System.Text.RegularExpressions.Regex("^Tjek (nu|igen)$") }).First.ClickAsync();
        var save = page.GetByRole(AriaRole.Button, new() { Name = "Gem i historik" });
        await save.WaitForAsync(new() { Timeout = 20_000 });
        await save.ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Gemt i historik" }).WaitForAsync(new() { Timeout = 10_000 });
    }

    [Test]
    public async Task Each_browser_sees_only_its_own_history()
    {
        await GoOrSkip(Base);
        var a = "Test-A-" + Guid.NewGuid().ToString("N")[..6];
        var b = "Test-B-" + Guid.NewGuid().ToString("N")[..6];

        await SaveRun(Page, a);
        await using var other = await Browser.NewContextAsync(ContextOptions());
        var pageB = await other.NewPageAsync();
        await SaveRun(pageB, b);

        await Page.GotoAsync(Base + "/historik");
        await Expect(Page.Locator("body")).ToContainTextAsync(a, new() { Timeout = 15_000 });
        Assert.That(await Page.Locator("body").InnerTextAsync(), Does.Not.Contain(b), "browser A must not see browser B's device");

        await pageB.GotoAsync(Base + "/historik");
        await Expect(pageB.Locator("body")).ToContainTextAsync(b, new() { Timeout = 15_000 });
        Assert.That(await pageB.Locator("body").InnerTextAsync(), Does.Not.Contain(a), "browser B must not see browser A's device");
    }

    [Test]
    public async Task The_page_shows_this_browsers_code_for_the_deep_check()
    {
        await GoOrSkip(Base);
        await Expect(Page.Locator(".tjek-key")).ToContainTextAsync("Din kode", new() { Timeout = 15_000 });
    }

    [Test]
    public async Task A_deep_result_without_a_code_is_refused()
    {
        using var http = new HttpClient();
        try
        {
            var r = await http.PostAsJsonAsync(Base + "/api/deep", new { device = "Test", checks = Array.Empty<object>() });
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            var ok = await http.PostAsJsonAsync(Base + "/api/deep", new { device = "Test-" + Guid.NewGuid().ToString("N")[..6], key = "TESTKODE2345", checks = Array.Empty<object>() });
            Assert.That(ok.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        catch (HttpRequestException e) { Assert.Ignore("OFFLINE - " + e.Message); }
    }
}
