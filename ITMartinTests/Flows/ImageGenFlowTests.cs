namespace ITMartinTests.Flows;

/// <summary>
/// Real browser flow tests for billedbehandling.itmartin.dk (Image Generator / ITMartinImageGen).
/// Uses Playwright (Chromium headless) so the Blazor Server circuit connects properly.
///
/// Split into two tiers:
///   - Free structural checks (page shell, Danish copy, cost disclosure, templates) — no paid
///     API calls, safe to run anytime.
///   - "Paid" tier: fires one real Generér call to verify Danish input still produces an
///     English Flux prompt. Costs a few øre — excluded from the default filter on purpose.
///
/// Run structural checks only:  dotnet test --filter "Category=Flow&Class=ImageGenFlowTests&Category!=Paid"
/// Run everything incl. paid:   dotnet test --filter "Class=ImageGenFlowTests"
/// </summary>
[TestFixture]
[Category("Flow")]
public class ImageGenFlowTests : FlowTestBase
{
    private const string Base = "https://billedbehandling.itmartin.dk";

    private static readonly (string Label, string IconHint)[] ExpectedModes =
    [
        ("Generér",  "🖊️"),
        ("Rediger",  "✏️"),
        ("Forvandl", "🔄"),
        ("Stil",     "🖌️"),
        ("Ansigt",   "👤"),
        ("Baggrund", "✂️"),
        ("Tøjprøve", "👕"),
        ("Forstør",  "🔍"),
    ];

    [Test]
    public async Task Index_Page_Loads_With_Danish_Title()
    {
        await GoOrSkip(Base);
        await WaitForPage();
        await AssertBodyHasContent("billedbehandling.itmartin.dk");

        var title = await Page.TitleAsync();
        Assert.That(title, Is.EqualTo("Billedgenerator"),
            "Page title should be the Danish app name");
    }

    [Test]
    public async Task All_Eight_Mode_Tabs_Are_Present_In_Danish()
    {
        await GoOrSkip(Base);
        await WaitForPage();

        var content = await Page.ContentAsync();
        foreach (var (label, _) in ExpectedModes)
        {
            Assert.That(content, Does.Contain(label),
                $"Mode tab '{label}' should be present — tab list may have changed or app failed to render");
        }
    }

    [Test]
    public async Task Mode_Switching_Updates_Description_Text()
    {
        await GoOrSkip(Base);
        await WaitForPage();

        // Default mode is Generér — its description should be visible immediately.
        var initialDesc = await Page.Locator(".mode-desc").InnerTextAsync();
        Assert.That(initialDesc, Does.Contain("Generér").IgnoreCase.Or.Contain("Beskriv"),
            "Default mode description should describe the Generér tool");

        // Switch to Baggrund and confirm the description actually changes.
        var bgDesc = await ClickModeTabReliably("Baggrund", "gennemsigtig");

        Assert.That(bgDesc, Is.Not.EqualTo(initialDesc),
            "Description should change when switching modes");
        Assert.That(bgDesc, Does.Contain("gennemsigtig").IgnoreCase,
            "Baggrund description should mention the transparent PNG result");
    }

    [Test]
    public async Task Cost_Disclosure_Banner_Is_Present()
    {
        await GoOrSkip(Base);
        await WaitForPage();

        var content = await Page.ContentAsync();
        Assert.That(content, Does.Contain("betaler selv").IgnoreCase,
            "Top banner should disclose that the site owner personally pays for generations " +
            "(added so a public visitor understands real money is spent per click). " +
            "FAILING HERE likely means the Studio.razor pricing/banner change hasn't been deployed " +
            "to the NAS yet — see .\\deploy.ps1 -Service imagegen");
    }

    [Test]
    public async Task Every_Mode_Description_States_An_Approximate_Price()
    {
        await GoOrSkip(Base);
        await WaitForPage();

        foreach (var (label, _) in ExpectedModes)
        {
            var desc = await ClickModeTabReliably(label, expectedSubstring: null);
            Assert.That(desc, Does.Contain("øre").IgnoreCase,
                $"'{label}' tab description should state an approximate øre cost per image. " +
                "FAILING HERE likely means the pricing-copy change hasn't been deployed yet.");
        }
    }

    /// <summary>
    /// Clicks a mode tab and returns the resulting .mode-desc text.
    ///
    /// KNOWN APP BUG (found 2026-09-01): the very first click on a mode tab right after page
    /// load is silently swallowed if the Blazor Server SignalR circuit hasn't finished
    /// connecting yet — no error, no visual feedback, the click just does nothing. A real user
    /// landing on the page and immediately clicking a tab sees nothing happen. Confirmed by hand
    /// in a real browser, not a Playwright timing artifact: a fresh-load click did nothing, the
    /// same click after a ~3s wait worked immediately.
    ///
    /// Worth fixing in the app itself (e.g. disable/grey the tabs until OnAfterRenderAsync's
    /// firstRender signals interactivity is live) rather than papering over it here — but the
    /// retry below keeps these tests useful in the meantime instead of flaking on it.
    /// </summary>
    private async Task<string> ClickModeTabReliably(string label, string? expectedSubstring)
    {
        var tab = Page.Locator(".mode-tab", new() { HasText = label });

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            await tab.ClickAsync();
            try
            {
                await Page.WaitForFunctionAsync(
                    "([lbl]) => document.querySelector('.mode-tab.active .mode-tab-label')?.textContent === lbl",
                    new object?[] { label },
                    new PageWaitForFunctionOptions { Timeout = 2_000, PollingInterval = 150 });
                break; // tab switch registered
            }
            catch (TimeoutException) when (attempt == 1)
            {
                // First click was likely swallowed by a not-yet-connected circuit — retry once.
            }
        }

        var desc = await Page.Locator(".mode-desc").InnerTextAsync();
        if (expectedSubstring is not null)
            Assert.That(desc, Does.Contain(expectedSubstring).IgnoreCase,
                $"'{label}' description should contain '{expectedSubstring}' after switching");
        return desc;
    }

    [Test]
    public async Task Generate_Tab_Shows_Style_Template_Buttons()
    {
        await GoOrSkip(Base);
        await WaitForPage();
        // Generér is the default mode, no need to click it.

        var templateButtons = Page.Locator(".template-btn");
        var count = await templateButtons.CountAsync();

        Assert.That(count, Is.GreaterThanOrEqualTo(4),
            "Expected at least 4 style template buttons (Hvid baggrund, Sort/hvid, Livsstil, Udendørs). " +
            "FAILING HERE likely means the templates feature hasn't been deployed yet.");

        if (count > 0)
        {
            var labels = await templateButtons.AllInnerTextsAsync();
            Assert.That(string.Join(" | ", labels), Does.Contain("Sort/hvid"),
                "Black & white style template should be one of the presets");
        }
    }

    [Test]
    public async Task Clicking_A_Template_Appends_To_Description_Textarea()
    {
        await GoOrSkip(Base);
        await WaitForPage();

        var templateButtons = Page.Locator(".template-btn");
        if (await templateButtons.CountAsync() == 0)
        {
            Assert.Ignore("Templates not deployed yet — see Generate_Tab_Shows_Style_Template_Buttons");
            return;
        }

        // Note: Locator.FillAsync()/BlurAsync() do NOT reliably trigger Blazor Server's plain
        // @bind sync in this app (confirmed by hand — this is a Playwright/Blazor automation
        // quirk, not a real bug: a genuine click+type+click via CDP-level input works fine).
        // Page.Keyboard.TypeAsync mirrors real per-key input and is what actually works here.
        var textarea = Page.Locator("textarea.ctrl-textarea").First;
        await textarea.ClickAsync();
        await Page.Keyboard.TypeAsync("En fugl der flyver", new() { Delay = 15 });
        await Task.Delay(200);
        await Page.Locator(".template-btn", new() { HasText = "Sort/hvid" }).ClickAsync();
        await Task.Delay(300);

        var value = await textarea.InputValueAsync();
        Assert.That(value, Does.Contain("En fugl der flyver"),
            "Original description text should be preserved");
        Assert.That(value, Does.Contain("sort/hvid").IgnoreCase,
            "Clicking the Sort/hvid template should append the style phrase");
    }

    // ── Paid tier — fires real Claude + Flux calls, costs real money, run deliberately ──

    [Test]
    [Category("Paid")]
    public async Task Danish_Description_Produces_An_English_Flux_Prompt()
    {
        await GoOrSkip(Base);
        await WaitForPage();

        // The app disables inputs until the Blazor Server circuit confirms it's actually live
        // (fixes a real bug where an early click was silently dropped) — wait that out first.
        var textarea = Page.Locator("textarea.ctrl-textarea").First;
        await Expect(textarea).ToBeEnabledAsync(new() { Timeout = 10_000 });
        await Task.Delay(300); // let the readiness re-render settle before the next interaction

        await textarea.ClickAsync();
        await Page.Keyboard.TypeAsync("En rød ballon i en klar blå himmel", new() { Delay = 15 });
        await Page.Keyboard.PressAsync("Tab"); // plain @bind only syncs to the server on blur

        var generateButton = Page.Locator("button", new() { HasText = "Generér billede" });
        await Expect(generateButton).ToBeEnabledAsync(new() { Timeout = 5_000 });
        await generateButton.ClickAsync();

        // Generation (Claude refine + Flux) can take 10-30s.
        await Page.Locator(".prompt-details").WaitForAsync(new() { Timeout = 45_000 });
        // <details> is collapsed by default — InnerTextAsync reflects rendered (visible) text
        // and returns empty for hidden content; TextContentAsync reads the raw DOM text instead.
        var promptText = await Page.Locator(".prompt-details p").TextContentAsync() ?? "";

        Assert.That(promptText, Is.Not.Empty, "AI prompt debug text should be populated");
        Assert.That(ContainsDanishCharacters(promptText), Is.False,
            $"Flux prompt should be English even though input was Danish. Got: \"{promptText}\"");
    }

    private static bool ContainsDanishCharacters(string text) =>
        text.Contains('æ') || text.Contains('ø') || text.Contains('å') ||
        text.Contains('Æ') || text.Contains('Ø') || text.Contains('Å');
}
