namespace ITMartinTests.Flows;

[TestFixture]
[Category("Flow")]
public class R6FlowTests : FlowTestBase
{
    private const string AssistantBase = "https://r6.itmartin.dk";
    private const string IntelBase     = "https://r6intel.itmartin.dk";

    // ── R6 Assistant ──────────────────────────────────────────────────────────

    [Test]
    public async Task R6Assistant_Index_Loads()
    {
        await GoOrSkip(AssistantBase);
        await WaitForPage();
        await AssertBodyHasContent("R6 Assistant index");
    }

    [Test]
    public async Task R6Assistant_Shows_Interface()
    {
        await GoOrSkip(AssistantBase);
        await WaitForPage();

        var hasContent = await Page.Locator("input, textarea, button, .operator, .map, ul li, h1, h2, div[class]").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "R6 Assistant should show operators, maps, or an interface");
    }

    // ── R6 Intel ──────────────────────────────────────────────────────────────

    [Test]
    public async Task R6Intel_Index_Loads()
    {
        await GoOrSkip(IntelBase);
        await WaitForPage();
        await AssertBodyHasContent("R6 Intel index");
    }

    [Test]
    public async Task R6Intel_Shows_Stats_Or_Intel()
    {
        await GoOrSkip(IntelBase);
        await WaitForPage();
        await AssertBodyHasContent("R6 Intel");

        var hasContent = await Page.Locator("table, .stat, .intel, ul li, .card, h1, h2, div[class]").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "R6 Intel should show stats, intel data, or headings");
    }
}
