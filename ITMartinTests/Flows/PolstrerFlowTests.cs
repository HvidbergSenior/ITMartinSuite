namespace ITMartinTests.Flows;

/// <summary>
/// Real browser flow tests for polstrer.itmartin.dk (Møbelpolstrer).
/// Phone-sized viewport - the app is used from a phone in the workshop.
///
/// Everything here is read-only against the live app, except
/// <see cref="Create_Piece_Add_Step_Search_Share_Delete"/> which writes a
/// piece and removes it again - it only runs when POLSTRER_WRITE_TESTS=1,
/// so a nightly run never leaves test furniture in her real list.
///
/// Run locally:  dotnet test --filter "Category=Flow&Class=PolstrerFlowTests"
/// </summary>
[TestFixture]
[Category("Flow")]
public class PolstrerFlowTests : FlowTestBase
{
    private const string Base = "https://polstrer.itmartin.dk";
    private static string Pin => Environment.GetEnvironmentVariable("POLSTRER_ADMIN_PIN") ?? "Vibz1";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        ViewportSize = new() { Width = 390, Height = 844 },
        Locale       = "da-DK",
    };

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task Login()
    {
        await GoOrSkip($"{Base}/login");
        await Page.WaitForSelectorAsync(".login-field", new() { Timeout = 15_000 });
        // Submit can be lost while the circuit attaches and re-renders the form
        for (var attempt = 0; attempt < 4 && Page.Url.Contains("/login"); attempt++)
        {
            await Page.Locator(".login-field").FillAsync(Pin);
            await Page.Locator("button[type=submit]").ClickAsync();
            try { await Page.WaitForURLAsync(u => !u.Contains("/login"), new() { Timeout = 5_000 }); }
            catch (TimeoutException) { }
        }
        Assert.That(Page.Url, Does.Not.Contain("/login"), "Login with the correct PIN should succeed");
        await Page.WaitForSelectorAsync("h1", new() { Timeout = 15_000 });
    }

    // Blazor Server prerenders every full page load and attaches the circuit a
    // moment later; a click that lands in that gap is lost. Click, wait briefly
    // for the expected result, and click again if nothing happened.
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

    // ── login gate ────────────────────────────────────────────────────────────

    [Test]
    public async Task Anonymous_Is_Redirected_To_Login()
    {
        await GoOrSkip(Base);
        await WaitForPage();

        Assert.That(Page.Url, Does.Contain("/login"), "Front page must require the PIN");
        Assert.That(await Page.Locator(".login-field").CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Wrong_Pin_Is_Rejected()
    {
        await GoOrSkip($"{Base}/login");
        await Page.WaitForSelectorAsync(".login-field", new() { Timeout = 15_000 });

        await Page.Locator(".login-field").FillAsync("forkert-pin");
        await Page.Locator("button[type=submit]").ClickAsync();
        await Page.WaitForSelectorAsync(".login-error", new() { Timeout = 10_000 });

        Assert.That(Page.Url, Does.Contain("error=1"));
        Assert.That(await Page.Locator(".login-error").InnerTextAsync(), Does.Contain("Forkert PIN"));
    }

    [Test]
    public async Task Correct_Pin_Shows_Piece_List()
    {
        await Login();

        Assert.That(await Page.Locator("h1").InnerTextAsync(), Is.EqualTo("Møbler"));
        // Either pieces or the empty state, never a blank page
        var cards = await Page.Locator(".piece-card").CountAsync();
        var empty = await Page.Locator(".empty").CountAsync();
        Assert.That(cards > 0 || empty > 0, Is.True, "Expected piece cards or the empty state");
    }

    [Test]
    public async Task Logout_Requires_Pin_Again()
    {
        await Login();
        await Page.GotoAsync($"{Base}/api/auth/logout");
        await Page.WaitForURLAsync(u => u.Contains("/login"), new() { Timeout = 15_000 });

        await Page.GotoAsync(Base);
        await WaitForPage();
        Assert.That(Page.Url, Does.Contain("/login"));
    }

    // ── navigation ────────────────────────────────────────────────────────────

    [Test]
    public async Task Bottom_Nav_Reaches_New_Piece_And_Erfaring()
    {
        await Login();

        await ClickUntil(Page.Locator(".bottom-nav a[href='/ny']"), ".camera-btn");
        Assert.That(await Page.Locator("h1").InnerTextAsync(), Is.EqualTo("Nyt møbel"));
        Assert.That(await Page.Locator("input[capture='environment']").CountAsync(), Is.EqualTo(1),
            "Camera button must open the rear camera directly");

        await ClickUntil(Page.Locator(".bottom-nav a[href='/erfaring']"), "input[type=search]");
        Assert.That(await Page.Locator("h1").InnerTextAsync(), Is.EqualTo("Erfaring"));
    }

    [Test]
    public async Task Erfaring_Search_For_Nonsense_Shows_No_Match_Message()
    {
        await Login();
        await Page.GotoAsync($"{Base}/erfaring");
        await Page.WaitForSelectorAsync("input[type=search]", new() { Timeout = 15_000 });

        // The page is prerendered before the Blazor circuit attaches, so typing
        // too early is lost - retry the input until the interactive result shows.
        var found = false;
        for (var attempt = 0; attempt < 5 && !found; attempt++)
        {
            await Page.Locator("input[type=search]").FillAsync("");
            await Page.Locator("input[type=search]").PressSequentiallyAsync("xyzzy-findes-ikke-42");
            try
            {
                await Page.WaitForSelectorAsync("text=Ingen møbler matcher", new() { Timeout = 3_000 });
                found = true;
            }
            catch (TimeoutException) { }
        }
        Assert.That(found, Is.True, "Erfaring should report no match for a nonsense query");
    }

    // ── public share page ─────────────────────────────────────────────────────

    [Test]
    public async Task Share_Page_Is_Public_And_Handles_Unknown_Piece()
    {
        await GoOrSkip($"{Base}/vis/findes-ikke-000000");
        await WaitForPage();

        Assert.That(Page.Url, Does.Not.Contain("/login"), "Share pages must not require the PIN");
        await Page.WaitForSelectorAsync("text=Møblet findes ikke", new() { Timeout = 15_000 });
    }

    // ── full round trip (opt-in) ──────────────────────────────────────────────

    [Test]
    public async Task Create_Piece_Add_Step_Search_Share_Delete()
    {
        if (Environment.GetEnvironmentVariable("POLSTRER_WRITE_TESTS") != "1")
            Assert.Ignore("Writes to the live app - set POLSTRER_WRITE_TESTS=1 to run");

        var title = $"Testmøbel {DateTime.UtcNow:HHmmss}";
        var photo = MakeJpeg();

        await Login();
        await Page.GotoAsync($"{Base}/ny");
        await Page.WaitForSelectorAsync(".camera-btn", new() { Timeout = 15_000 });

        // Photo first, like she would. Retried because the page is prerendered
        // before the Blazor circuit attaches and an early change event is lost.
        var chosen = false;
        for (var attempt = 0; attempt < 5 && !chosen; attempt++)
        {
            await Page.Locator("input[capture='environment']").SetInputFilesAsync(new FilePayload
                { Name = "IMG_test.jpg", MimeType = "image/jpeg", Buffer = photo });
            try { await Page.WaitForSelectorAsync(".chosen-files", new() { Timeout = 3_000 }); chosen = true; }
            catch (TimeoutException) { }
        }
        Assert.That(chosen, Is.True, "Photo should register as chosen");

        await Page.GetByPlaceholder("fx Lænestol").FillAsync(title);
        await Page.GetByPlaceholder("Navn").FillAsync("Playwright");
        await Page.GetByPlaceholder("fx kunden").FillAsync("testen");
        await Page.Locator("textarea").FillAsync("Behold nakkerullen, nyt betræk");
        await Page.GetByText("Gem møbel").ClickAsync();

        // Lands on the piece page with instructions pinned and the first photo in the timeline
        await Page.WaitForURLAsync(u => u.Contains("/moebel/"), new() { Timeout = 20_000 });
        await Page.WaitForSelectorAsync(".instructions-text", new() { Timeout = 15_000 });
        Assert.That(await Page.Locator("h1").InnerTextAsync(), Is.EqualTo(title));
        Assert.That(await Page.Locator(".instructions-text").InnerTextAsync(), Does.Contain("nakkerullen"));
        Assert.That(await Page.Locator(".media-thumb img").CountAsync(), Is.EqualTo(1), "First photo should show as a thumbnail");
        var shareUrl = Page.Url.Replace("/moebel/", "/vis/");

        // Add a step with a note - status flips to "I gang"
        await Page.GetByPlaceholder("Hvad har du gjort?").FillAsync("Afmonteret betræk");
        await Page.GetByText("Gem trin").ClickAsync();
        await Page.WaitForSelectorAsync("text=Afmonteret betræk", new() { Timeout = 15_000 });
        Assert.That(await Page.Locator(".step").CountAsync(), Is.EqualTo(2));

        // Erfaring finds the step note
        await Page.GotoAsync($"{Base}/erfaring?q=Afmonteret");
        await Page.WaitForSelectorAsync(".hit", new() { Timeout = 15_000 });
        Assert.That(await Page.Locator(".hit-title").First.InnerTextAsync(), Does.Contain(title));

        // Public share page shows the story without a login
        var anon = await Browser.NewContextAsync(ContextOptions());
        var anonPage = await anon.NewPageAsync();
        await anonPage.GotoAsync(shareUrl);
        await anonPage.WaitForSelectorAsync(".step", new() { Timeout = 15_000 });
        Assert.That(anonPage.Url, Does.Not.Contain("/login"));
        Assert.That(await anonPage.Locator("h1").InnerTextAsync(), Is.EqualTo(title));
        await anon.CloseAsync();

        // Clean up: delete the piece (confirm() dialog accepted)
        await Page.GotoAsync(shareUrl.Replace("/vis/", "/moebel/"));
        await Page.WaitForSelectorAsync("h1", new() { Timeout = 15_000 });
        Page.Dialog += (_, d) => d.AcceptAsync();
        // Full navigation = prerender first; keep clicking "Ret" until the
        // interactive edit form actually opens.
        await ClickUntil(Page.GetByText("Ret", new() { Exact = true }), "text=Slet møblet");
        await Page.GetByText("Slet møblet").ClickAsync();
        await Page.WaitForURLAsync(u => u.TrimEnd('/') == Base, new() { Timeout = 15_000 });
        await Page.WaitForSelectorAsync("h1:text-is(\"Møbler\")", new() { Timeout = 15_000 });
        Assert.That(await Page.Locator($"text={title}").CountAsync(), Is.EqualTo(0), "Test piece should be gone");
    }

    // Smallest valid JPEG that ImageSharp will decode - a 1x1 grey pixel.
    private static byte[] MakeJpeg() => Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/" +
        "wAALCAABAAEBAREA/8QAFAABAAAAAAAAAAAAAAAAAAAACf/EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAD8AKp//2Q==");
}
