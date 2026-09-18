namespace ITMartinTests.Flows;

/// <summary>
/// Real browser flow tests for Forløbet (forloebet.itmartin.dk).
/// Phone-sized viewport - it is read on the phone.
///
/// Read-only against whatever FORLOEBET_BASE points at (default: the live app),
/// except <see cref="Create_Forloeb_Rewrite_Add_Step_Close"/>, which creates a
/// forløb (unshared, so it never lands on the front page) and spends 3-4 Claude
/// calls - it only runs when FORLOEBET_WRITE_TESTS=1.
///
/// Run locally:  FORLOEBET_BASE=http://localhost:5132 dotnet test --filter "Class=ForloebetFlowTests"
/// </summary>
[TestFixture]
[Category("Flow")]
public class ForloebetFlowTests : FlowTestBase
{
    private static string Base => Environment.GetEnvironmentVariable("FORLOEBET_BASE") ?? "https://forloebet.itmartin.dk";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        ViewportSize = new() { Width = 390, Height = 844 },
        Locale       = "da-DK",
    };

    private async Task ClickUntil(ILocator target, string expectedSelector, int attempts = 5)
    {
        for (var i = 0; i < attempts; i++)
        {
            await target.ClickAsync();
            try { await Page.WaitForSelectorAsync(expectedSelector, new() { Timeout = 4_000 }); return; }
            catch (TimeoutException) { }
        }
        Assert.Fail($"Clicking did not produce '{expectedSelector}' after {attempts} attempts");
    }

    [Test]
    public async Task Front_Page_Is_A_Feed_With_The_Movia_Case()
    {
        await GoOrSkip(Base);
        await Page.WaitForSelectorAsync(".card-line", new() { Timeout = 15_000 });
        Assert.That(await Page.Locator(".headline").InnerTextAsync(), Is.Not.Empty, "AI headline on top");
        var first = Page.Locator(".card-line").First;
        Assert.That(await first.InnerTextAsync(), Does.Contain("Movia"));
        Assert.That(await Page.Locator("nav").CountAsync(), Is.EqualTo(0), "no menu");
        Assert.That(await Page.Locator("a.btn-primary", new() { HasText = "Fortæl dit forløb" }).CountAsync(), Is.EqualTo(1));

        await ClickUntil(first, ".card.open .analysis");
        Assert.That(await Page.Locator(".card.open .ai-label").InnerTextAsync(), Does.Contain("AI-ANALYSE").IgnoreCase);
        Assert.That(await Page.Locator(".card.open .q").CountAsync(), Is.GreaterThanOrEqualTo(6), "six questions");
    }

    [Test]
    public async Task Case_Page_Shows_Timeline_Quotes_And_Analysis_Without_Owner_Tools()
    {
        await GoOrSkip($"{Base}/sag/kontrolafgift-movia-2026");
        await Page.WaitForSelectorAsync(".timeline .step", new() { Timeout = 15_000 });
        Assert.That(await Page.Locator(".timeline .step").CountAsync(), Is.GreaterThanOrEqualTo(10));
        Assert.That(await Page.Locator(".step-quote").Filter(new() { HasText = "sikret" }).CountAsync(), Is.GreaterThanOrEqualTo(1));
        Assert.That(await Page.Locator(".closed").InnerTextAsync(), Does.Contain("Hvad lærte jeg"));
        Assert.That(await Page.Locator(".owner-block").CountAsync(), Is.EqualTo(0), "no edit tools without the key");
        Assert.That(await Page.Locator(".analysis .q").CountAsync(), Is.GreaterThanOrEqualTo(6));
    }

    [Test]
    public async Task Om_Page_States_The_Purpose()
    {
        await GoOrSkip($"{Base}/om");
        var text = await Page.Locator(".doc").InnerTextAsync();
        Assert.That(text, Does.Contain("ikke til for at stemple enkeltpersoner"));
        Assert.That(text, Does.Contain("Beskriv hændelsen"));
    }

    [Test]
    public async Task Documents_Are_Not_Served_Without_The_Key()
    {
        await GoOrSkip(Base);
        var resp = await Page.APIRequest.GetAsync($"{Base}/dok/kontrolafgift-movia-2026/1");
        Assert.That(resp.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task Create_Forloeb_Rewrite_Add_Step_Close()
    {
        if (Environment.GetEnvironmentVariable("FORLOEBET_WRITE_TESTS") != "1")
            Assert.Ignore("Creates a forløb and spends Claude calls - set FORLOEBET_WRITE_TESTS=1 to run");

        await GoOrSkip($"{Base}/ny");
        await Page.WaitForSelectorAsync("textarea.big", new() { Timeout = 15_000 });
        await Page.Locator("textarea.big").FillAsync(
            "Fik en kontrolafgift fra Movia for 29/6 men jeg var slet ikke i bussen. Sagsbehandler Stefan Jørgensen " +
            "siger jeg svarede rigtigt på et kontrolspørgsmål, det er jo løgn. Har politianmeldt det, journalnr 0100-74257-00184-26. " +
            "Mit cpr er 010170-1234. TEST-FORLØB");
        await Page.Locator("textarea.big").DispatchEventAsync("change");
        // One click only: the rewrite is a Claude call (10-20 s), and a retry would
        // land on "Gem og fortsæt" once the preview appears.
        await Page.Locator("button.btn-primary").ClickAsync();
        await Page.WaitForSelectorAsync(".doc h1:has-text('Sådan bliver det vist')", new() { Timeout = 60_000 });

        // The rewrite must drop the name and the CPR and keep the substance.
        var rewritten = await Page.Locator("textarea.big").InputValueAsync();
        Assert.That(rewritten, Does.Not.Contain("Stefan"), "staff name removed");
        Assert.That(rewritten, Does.Not.Contain("010170"), "CPR removed");
        Assert.That(rewritten, Does.Not.Contain("løgn"), "accusation removed");
        Assert.That(rewritten.ToLowerInvariant(), Does.Contain("movia"));

        // Never share the test case.
        var share = Page.Locator("label.check input[type=checkbox]");
        if (await share.IsCheckedAsync()) await share.UncheckAsync();
        await Page.Locator("button.btn-primary", new() { HasText = "Gem og fortsæt" }).ClickAsync();
        // Blazor navigates in-place (no Load event) - watch the location instead.
        await Page.WaitForFunctionAsync("() => location.pathname.startsWith('/sag/') && location.search.includes('k=')", null, new() { Timeout = 15_000 });
        await Page.WaitForSelectorAsync(".keybox", new() { Timeout = 15_000 });
        Assert.That(await Page.Locator(".timeline .step").CountAsync(), Is.EqualTo(1));

        // Add a step through the rewrite.
        await Page.Locator(".owner-block textarea.big").First.FillAsync("Movia svarede i dag at de fastholder og at videoen er sikret hos operatøren.");
        await Page.Locator(".owner-block textarea.big").First.DispatchEventAsync("change");
        await Page.Locator(".owner-block button.btn-primary").First.ClickAsync();
        await Page.WaitForSelectorAsync("button:has-text('Gem trin')", new() { Timeout = 60_000 });
        await Page.Locator("button:has-text('Gem trin')").ClickAsync();
        await Page.WaitForFunctionAsync("() => document.querySelectorAll('.timeline .step').length === 2", null, new() { Timeout = 15_000 });

        // Analysis arrives in the background.
        await Page.WaitForSelectorAsync(".analysis .q", new() { Timeout = 90_000 });
        Assert.That(await Page.Locator(".analysis .q").CountAsync(), Is.GreaterThanOrEqualTo(6));

        // Close it.
        await Page.Locator("button:has-text('Afslut forløbet')").ClickAsync();
        await Page.Locator(".owner-block textarea.big").Nth(2).FillAsync("Movia frafaldt afgiften efter klage til Ankenævnet. TEST.");
        await Page.Locator(".owner-block textarea.big").Nth(2).DispatchEventAsync("change");
        await Page.Locator(".owner-block textarea.big").Nth(3).FillAsync("Bed om dokumentationen.");
        await Page.Locator(".owner-block textarea.big").Nth(3).DispatchEventAsync("change");
        var shareAtClose = Page.Locator(".owner-block label.check input[type=checkbox]");
        if (await shareAtClose.IsCheckedAsync()) await shareAtClose.UncheckAsync();
        await Page.Locator("button:has-text('Afslut og lås')").ClickAsync();
        await Page.WaitForSelectorAsync(".closed", new() { Timeout = 15_000 });
        Assert.That(await Page.Locator(".owner-block textarea").CountAsync(), Is.EqualTo(0), "locked after close");
    }
}
