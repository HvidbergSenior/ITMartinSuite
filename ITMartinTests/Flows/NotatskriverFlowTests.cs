namespace ITMartinTests.Flows;

/// <summary>
/// Notatskriver (notatskriver.itmartin.dk) in a real browser, as an ANONYMOUS visitor on a phone.
/// None of these tests reaches the AI: they check what the free version offers and that personal data is
/// stopped BEFORE anything is sent - so they cost nothing and are safe against the live app.
///
/// Run locally:  NOTATSKRIVER_BASE=http://localhost:5145 dotnet test --filter "Class=NotatskriverFlowTests"
/// </summary>
[TestFixture]
[Category("Flow")]
public class NotatskriverFlowTests : FlowTestBase
{
    private static string Base => Environment.GetEnvironmentVariable("NOTATSKRIVER_BASE") ?? "https://notatskriver.itmartin.dk";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        ViewportSize = new() { Width = 390, Height = 844 },
        Locale = "da-DK",
    };

    private ILocator T(string name) => Page.Locator($"[data-test={name}]");

    private async Task Open()
    {
        await GoOrSkip(Base);
        // The buttons work only once the Blazor circuit is up; the notes box is enabled from the first render.
        await T("notes").WaitForAsync(new() { Timeout = 15_000 });
        await Page.WaitForFunctionAsync("() => window.Blazor !== undefined", null, new() { Timeout = 15_000 });
        await Page.WaitForTimeoutAsync(800);
    }

    private async Task TypeNotes(string text)
    {
        await T("notes").FillAsync(text);
        await T("notes").DispatchEventAsync("input");
        await Expect(T("write")).ToBeEnabledAsync(new() { Timeout = 5_000 });
    }

    [Test]
    public async Task Free_version_offers_text_and_pictures_only()
    {
        await Open();
        await Expect(T("pick-images")).ToBeVisibleAsync();
        await Expect(T("free-note")).ToContainTextAsync("op til 3 billeder");
        Assert.That(await T("pick-files").CountAsync(), Is.EqualTo(0), "no PDF/Word in the free version");
        Assert.That(await T("dictate").CountAsync(), Is.EqualTo(0), "no dictation in the free version");
        Assert.That(await Page.Locator("input[type=password]").CountAsync(), Is.EqualTo(0), "no login of any kind");
        await Expect(T("privacy")).ToContainTextAsync("Intet gemmes");
    }

    [Test]
    public async Task Write_is_disabled_until_there_are_notes()
    {
        await Open();
        await Expect(T("write")).ToBeDisabledAsync();
        await TypeNotes("møde om ny printer");
        await Expect(T("write")).ToBeEnabledAsync();
    }

    [Test]
    public async Task Patient_notes_are_stopped_before_anything_is_sent()
    {
        await Open();
        await TypeNotes("Patienten har smerter og får panodil to gange dagligt");
        await T("write").ClickAsync();
        await Expect(T("stopped")).ToContainTextAsync("⛔", new() { Timeout = 10_000 });
        Assert.That(await T("hide-and-continue").CountAsync(), Is.EqualTo(0), "patient notes can not be sent even with names hidden");
        Assert.That(await T("result").CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task Phone_numbers_stop_it_until_the_visitor_chooses_to_hide_them()
    {
        await Open();
        await TypeNotes("Husk at ringe til Bo på 12 34 56 78 om tilbuddet");
        await T("write").ClickAsync();
        await Expect(T("stopped")).ToContainTextAsync("Telefonnummer", new() { Timeout = 10_000 });
        await Expect(T("hide-and-continue")).ToBeVisibleAsync();
    }

    [Test]
    public async Task A_pdf_is_refused_in_the_free_version()
    {
        await Open();
        await Page.Locator("#pics").SetInputFilesAsync(new FilePayload
        {
            Name = "rapport.pdf", MimeType = "application/pdf", Buffer = "%PDF-1.4 test"u8.ToArray(),
        });
        await Expect(T("error")).ToContainTextAsync("kun billeder", new() { Timeout = 10_000 });
        Assert.That(await T("attachments").CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task A_screenshot_is_taken_and_must_be_confirmed()
    {
        await Open();
        // 1x1 PNG
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        await Page.Locator("#pics").SetInputFilesAsync(new FilePayload { Name = "skaerm.png", MimeType = "image/png", Buffer = png });
        await Expect(T("attachments").Locator("li")).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Expect(T("write")).ToBeDisabledAsync();   // not until the pictures are confirmed
        await T("confirm-visuals").CheckAsync();
        await Expect(T("write")).ToBeEnabledAsync();
    }

    [Test]
    public async Task Dark_mode_follows_the_shared_design()
    {
        await Page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await Open();
        var bg = await Page.EvaluateAsync<string>("() => getComputedStyle(document.body).backgroundColor");
        var text = await Page.EvaluateAsync<string>("() => getComputedStyle(document.body).color");
        Assert.That(bg, Is.Not.EqualTo("rgb(255, 255, 255)"), "dark background in dark mode");
        Assert.That(bg, Is.Not.EqualTo(text), "text must differ from the background");
    }
}
