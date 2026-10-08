namespace ITMartinTests.Flows;

/// <summary>
/// Tilbud (tilbud.itmartin.dk) in a real browser, as an ANONYMOUS visitor on a phone. The search test uses the real
/// leaflet service (Tjek) through the app - free, no key. Nothing is written on the server: "Mine varer" lives in the
/// browser, and the faste-tilbud test only tries a wrong PIN.
///
/// Run locally:  TILBUD_BASE=http://localhost:5150 dotnet test --filter "FullyQualifiedName~TilbudFlowTests"
/// </summary>
[TestFixture]
[Category("Flow")]
public class TilbudFlowTests : FlowTestBase
{
    private static string Base => Environment.GetEnvironmentVariable("TILBUD_BASE") ?? "https://tilbud.itmartin.dk";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        ViewportSize = new() { Width = 390, Height = 844 },
        Locale = "da-DK",
    };

    private async Task SetPlace()
    {
        await Page.Locator("#postnr").FillAsync("8200");
        await Page.Locator("#sted-form button[type=submit]").ClickAsync();
        await Expect(Page.Locator("#sted-nu")).ToContainTextAsync("8200", new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Front_page_has_the_search_and_no_login()
    {
        await GoOrSkip(Base);
        await Expect(Page.Locator("#q")).ToBeVisibleAsync();
        Assert.That(await Page.Locator("input[type=password]").CountAsync(), Is.EqualTo(0), "no login of any kind on the front page");
        await Expect(Page.Locator("#info-kort")).ToContainTextAsync("Ingen reklamer");
    }

    [Test]
    public async Task Postcode_then_search_shows_offers_with_prices()
    {
        await GoOrSkip(Base);
        await SetPlace();
        await Page.Locator("#q").FillAsync("kaffe");
        await Page.Locator("#soeg-form button[type=submit]").ClickAsync();
        try { await Page.Locator("#resultater .tb-offer").First.WaitForAsync(new() { Timeout = 25_000 }); }
        catch (TimeoutException)
        {
            var info = await Page.Locator("#soeg-info").InnerTextAsync();
            if (info.Contains("svarer ikke")) Assert.Ignore("The leaflet service (Tjek) is down: " + info);
            throw;
        }
        var first = Page.Locator("#resultater .tb-offer").First;
        await Expect(first.Locator(".tb-price")).ToContainTextAsync("kr");
        await Expect(first.Locator(".tb-chain")).Not.ToBeEmptyAsync();
    }

    [Test]
    public async Task Mine_varer_stay_in_this_browser_only()
    {
        await GoOrSkip(Base);
        await SetPlace();
        await Page.Locator("#q").FillAsync("smør");
        await Page.Locator("#soeg-form button[type=submit]").ClickAsync();
        await Expect(Page.Locator("#gem")).ToBeVisibleAsync(new() { Timeout = 25_000 });
        await Page.Locator("#gem").ClickAsync();
        await Expect(Page.Locator("#mine-liste")).ToContainTextAsync("smør", new() { IgnoreCase = true, Timeout = 10_000 });

        // A fresh browser (another visitor) sees nothing of it.
        await using var other = await Browser.NewContextAsync(ContextOptions());
        var page = await other.NewPageAsync();
        await page.GotoAsync(Base);
        await Expect(page.Locator("#mine-tom")).ToBeVisibleAsync();
        Assert.That(await page.Locator("#mine-liste").InnerTextAsync(), Does.Not.Contain("smør"));
    }

    [Test]
    public async Task Faste_tilbud_can_be_read_but_not_changed_without_the_PIN()
    {
        await GoOrSkip($"{Base}/faste");
        await Expect(Page.Locator("#faste-liste")).Not.ToContainTextAsync("Henter", new() { Timeout = 15_000 });
        await Expect(Page.Locator("#ny-kort")).ToBeHiddenAsync();
        await Page.Locator("#pin").FillAsync("forkert-pin");
        await Page.Locator("#pin-form button[type=submit]").ClickAsync();
        await Expect(Page.Locator("#pin-fejl")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Expect(Page.Locator("#ny-kort")).ToBeHiddenAsync();
    }

    [Test]
    public async Task The_phones_dark_mode_does_not_half_darken_the_page()
    {
        // Kolibri is light by default (user decision 2026-09-10); dark only when an app opts in with data-theme="dark".
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await GoOrSkip(Base);
        var bg = await Page.EvaluateAsync<string>("() => getComputedStyle(document.body).backgroundColor");
        var text = await Page.EvaluateAsync<string>("() => getComputedStyle(document.body).color");
        Assert.That(bg, Is.Not.EqualTo(text), "text must differ from the background");
    }

    [Test]
    public async Task Every_colour_follows_the_theme_tokens()
    {
        // Switch the shared dark theme on: anything still white was hard-coded instead of using a Kolibri token.
        await GoOrSkip(Base);
        await Page.EvaluateAsync("() => document.documentElement.setAttribute('data-theme', 'dark')");
        var bg = await Page.EvaluateAsync<string>("() => getComputedStyle(document.body).backgroundColor");
        var input = await Page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#q')).backgroundColor");
        var text = await Page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#q')).color");
        Assert.That(bg, Is.EqualTo("rgb(18, 25, 23)"), "the page uses --k-bg");
        Assert.That(input, Is.Not.EqualTo("rgb(255, 255, 255)"), "the input uses a token, not a hard-coded white");
        Assert.That(text, Is.Not.EqualTo(input), "readable text in the input");
    }
}
