namespace ITMartinTests.Flows;

[TestFixture]
[Category("Flow")]
public class MusikFlowTests : FlowTestBase
{
    private const string MusikBase  = "https://musik.itmartin.dk";
    private const string StudioBase = "https://studio.itmartin.dk";

    // ── Musik ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Musik_Index_Loads()
    {
        await GoOrSkip(MusikBase);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Musik index");
    }

    [Test]
    public async Task Musik_Shows_Songs_Or_Player()
    {
        await GoOrSkip(MusikBase);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasSongs  = await Page.Locator("audio, .song, .track, .musik-item, ul li, table tr").CountAsync() > 0;
        var hasPlayer = await Page.Locator("button[class*='play'], [class*='player'], [class*='musik']").CountAsync() > 0;
        var hasHeading = await Page.Locator("h1, h2, h3").CountAsync() > 0;

        Assert.That(hasSongs || hasPlayer || hasHeading, Is.True,
            "Musik should show songs, a player, or headings");
    }

    // ── Musik Studio ──────────────────────────────────────────────────────────

    [Test]
    public async Task MusikStudio_Index_Loads()
    {
        await GoOrSkip(StudioBase);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Musik Studio index");
    }

    [Test]
    public async Task MusikStudio_Shows_Studio_Interface()
    {
        await GoOrSkip(StudioBase);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasInterface = await Page.Locator("button, input, textarea, .track, .studio, canvas").CountAsync() > 0;

        Assert.That(hasInterface, Is.True,
            "Musik Studio should render an interactive studio interface");
    }
}
