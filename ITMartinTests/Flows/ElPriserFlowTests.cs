namespace ITMartinTests.Flows;

/// <summary>
/// ElPriser (elpriser.itmartin.dk) in a real browser, as an ANONYMOUS visitor on a phone. Location is NOT granted, so the
/// app must still show prices (default area). Read-only: nothing is subscribed, no bill is kept (the bad PDF is refused).
///
/// Run locally:  ELPRISER_BASE=http://localhost:5120 dotnet test --filter "FullyQualifiedName~ElPriserFlowTests"
/// </summary>
[TestFixture]
[Category("Flow")]
public class ElPriserFlowTests : FlowTestBase
{
    private static string Base => Environment.GetEnvironmentVariable("ELPRISER_BASE") ?? "https://elpriser.itmartin.dk";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        ViewportSize = new() { Width = 390, Height = 844 },
        Locale = "da-DK",
        Permissions = [],   // no location: the visitor says no
    };

    private async Task Open(string path = "")
    {
        await GoOrSkip(Base + path);
        await Page.WaitForFunctionAsync("() => window.Blazor !== undefined", null, new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Prices_show_without_location_or_login()
    {
        await Open();
        await Expect(Page.Locator(".now-price").First).ToContainTextAsync("KR", new() { Timeout = 20_000 });
        await Expect(Page.Locator(".bars .bar").First).ToBeVisibleAsync();
        Assert.That(await Page.Locator(".bars .bar").CountAsync(), Is.GreaterThan(8), "the day's bars");
        Assert.That(await Page.Locator("input[type=password]").CountAsync(), Is.EqualTo(0), "no login of any kind");
    }

    [Test]
    public async Task Tapping_a_bar_selects_that_start_time()
    {
        await Open();
        var bars = Page.Locator(".bars .bar-col");
        await Expect(bars.First).ToBeVisibleAsync(new() { Timeout = 20_000 });
        await Page.WaitForTimeoutAsync(800);   // the circuit must be up before taps count
        var target = bars.Nth(await bars.CountAsync() - 1);
        await target.ClickAsync();
        await Expect(target).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bsel\b"), new() { Timeout = 5_000 });
    }

    [Test]
    public async Task The_bottom_menu_reaches_Besked_and_Tjek_regning()
    {
        await Open();
        await Page.Locator(".bottom-nav a[href='/tjek']").ClickAsync();
        await Expect(Page.Locator(".card-title").First).ToContainTextAsync("Hvad betaler du", new() { Timeout = 10_000 });
        await Page.Locator(".bottom-nav a[href='/besked']").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/besked$"));
    }

    [Test]
    public async Task A_file_that_is_not_a_bill_is_refused_kindly()
    {
        await Open("/tjek");
        await Page.WaitForTimeoutAsync(800);
        await Page.Locator("input[type=file]").First.SetInputFilesAsync(new FilePayload
        {
            Name = "regning.pdf", MimeType = "application/pdf", Buffer = "ikke en pdf"u8.ToArray(),
        });
        await Expect(Page.Locator(".lvl-expensive").First).ToContainTextAsync("PDF", new() { Timeout = 10_000 });
    }

    [Test]
    public async Task The_theme_switch_flips_every_colour()
    {
        await Open();
        var dark = await Page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.card')).backgroundColor");
        await Page.Locator("#themeToggleBtn").ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("data-theme", "light");
        var light = await Page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.card')).backgroundColor");
        Assert.That(dark, Is.Not.EqualTo(light), "cards follow the theme tokens");
        Assert.That(light, Is.EqualTo("rgb(255, 255, 255)"), "light cards are --k-card");
        await Page.Locator("#themeToggleBtn").ClickAsync();   // leave it as the visitor found it
    }
}
