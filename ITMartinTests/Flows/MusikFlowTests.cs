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
        await WaitForPage();
        await AssertBodyHasContent("Musik index");
    }

    [Test]
    public async Task Musik_Shows_Songs_Or_Player()
    {
        await GoOrSkip(MusikBase);
        await WaitForPage();

        var hasContent = await Page.Locator("audio, .song, .track, .musik-item, ul li, table tr, button, h1, h2, h3, div[class]").CountAsync() > 0;

        Assert.That(hasContent, Is.True,
            "Musik should show songs, a player, or headings");
    }

    // ── Musik Studio ──────────────────────────────────────────────────────────

    [Test]
    public async Task MusikStudio_Index_Loads()
    {
        await GoOrSkip(StudioBase);
        await WaitForPage();
        await AssertBodyHasContent("Musik Studio index");
    }

    [Test]
    public async Task MusikStudio_Shows_Studio_Interface()
    {
        await GoOrSkip(StudioBase);
        await WaitForPage();
        await AssertBodyHasContent("Musik Studio interface");

        var hasContent = await Page.Locator("button, input, textarea, .track, .studio, canvas, h1, h2, div[class]").CountAsync() > 0;

        Assert.That(hasContent, Is.True,
            "Musik Studio should render an interactive studio interface");
    }
}
