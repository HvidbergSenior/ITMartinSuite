namespace ITMartinTests.Flows;

/// <summary>
/// Bogshoppen Lager in a real browser on a phone, against a LOCAL copy with an empty data folder (it creates boxes,
/// so it never runs against the live register). No AI and no price sites are reached.
///
/// Run:  .\test-app.ps1 -App Lager   (sets LAGER_BASE, LAGER_PIN and a temp Lager__DataDir)
/// </summary>
[TestFixture]
[Category("Flow")]
public class LagerFlowTests : FlowTestBase
{
    private static string Base => Environment.GetEnvironmentVariable("LAGER_BASE") ?? "http://localhost:5149";
    private static string Pin => Environment.GetEnvironmentVariable("LAGER_PIN") ?? "";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        ViewportSize = new() { Width = 390, Height = 844 },
        Locale = "da-DK",
    };

    [SetUp]
    public void LocalOnly()
    {
        if (!Base.Contains("localhost")) Assert.Ignore("Lager flow tests write boxes - local copy only");
        if (Pin == "") Assert.Ignore("LAGER_PIN not set");
    }

    private async Task LogIn()
    {
        await GoOrSkip(Base + "/login");
        await Page.Locator("input[name=pin]").FillAsync(Pin);
        await Page.Locator("button[type=submit]").ClickAsync();
        await Expect(Page.Locator("h1")).ToContainTextAsync("Find en vare", new() { Timeout = 10_000 });
    }

    [Test]
    public async Task Wrong_pin_stays_out()
    {
        await GoOrSkip(Base + "/kasser");
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login"));
        await Page.Locator("input[name=pin]").FillAsync("forkert-pin");
        await Page.Locator("button[type=submit]").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("fejl=1"));
    }

    [Test]
    public async Task New_box_gets_a_number_a_place_and_a_label()
    {
        await LogIn();
        await Page.GotoAsync(Base + "/kasser");
        await Page.Locator("input[placeholder^='Hvor står den']").FillAsync("Testreol 1");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Ny kasse" }).ClickAsync();
        await Expect(Page.Locator("h1")).ToContainTextAsync("Kasse K", new() { Timeout = 10_000 });
        var code = (await Page.Locator("h1").InnerTextAsync()).Split(' ').Last();

        await Page.Locator("input[placeholder='Hvor står den?']").FillAsync("Testreol 2");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Gem placering" }).ClickAsync();
        await Expect(Page.Locator(".lg-ok")).ToContainTextAsync("gemt");

        await Page.GotoAsync(Base + "/kasser");
        await Expect(Page.Locator(".lg-list")).ToContainTextAsync("Testreol 2");
        await Page.GotoAsync($"{Base}/kasse/{code}/label");
        await Expect(Page.Locator(".lg-label-code")).ToHaveTextAsync(code);
        await Expect(Page.Locator(".lg-label-qr svg")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Every_page_opens_and_says_what_to_do()
    {
        await LogIn();
        foreach (var (path, heading) in new[]
        {
            ("/registrer", "Registrér en bunke"), ("/boeger", "Hvilke bøger"), ("/kolofon", "kolofonen"),
            ("/magic", "Magic-kort"), ("/kasser", "Kasser"),
        })
        {
            await Page.GotoAsync(Base + path);
            await Expect(Page.Locator("h1")).ToContainTextAsync(heading, new() { Timeout = 10_000 });
        }
        await Page.GotoAsync(Base + "/kolofon");
        await Expect(Page.Locator(".lg-answer").First).ToContainTextAsync("Ingen lige nu");
    }
}
