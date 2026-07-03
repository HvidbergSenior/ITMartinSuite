namespace ITMartinTests.Flows;

[TestFixture]
[Category("Flow")]
public class ClubFlowTests : FlowTestBase
{
    private const string LionsBase   = "https://lions-club.itmartin.dk";
    private const string OldboyzBase = "https://r6oldboyz.itmartin.dk";

    // ── Lions Club ────────────────────────────────────────────────────────────

    [Test]
    public async Task Lions_Index_Loads()
    {
        await GoOrSkip(LionsBase);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Lions Club index");
    }

    [Test]
    public async Task Lions_Shows_Members_Or_Events()
    {
        await GoOrSkip(LionsBase);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasContent = await Page.Locator("ul li, table tr, .member, .event, .club, h1, h2").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "Lions Club should show members, events, or a heading");
    }

    [Test]
    public async Task Lions_Has_Navigation()
    {
        await GoOrSkip(LionsBase);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasNav = await Page.Locator("nav, a[href], button").CountAsync() > 0;
        Assert.That(hasNav, Is.True, "Lions Club should have navigation links or buttons");
    }

    // ── R6 Oldboyz ────────────────────────────────────────────────────────────

    [Test]
    public async Task Oldboyz_Index_Loads()
    {
        await GoOrSkip(OldboyzBase);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("R6 Oldboyz index");
    }

    [Test]
    public async Task Oldboyz_Shows_Members_Or_Events()
    {
        await GoOrSkip(OldboyzBase);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasContent = await Page.Locator("ul li, table tr, .member, .event, .club, h1, h2").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "R6 Oldboyz should show members, events, or a heading");
    }
}
