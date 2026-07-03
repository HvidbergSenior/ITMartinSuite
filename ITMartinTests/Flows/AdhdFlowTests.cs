namespace ITMartinTests.Flows;

[TestFixture]
[Category("Flow")]
public class AdhdFlowTests : FlowTestBase
{
    private const string Base = "https://adhd.itmartin.dk";

    [Test]
    public async Task Adhd_Index_Loads()
    {
        await GoOrSkip(Base);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("ADHD FindIt index");
    }

    [Test]
    public async Task Adhd_Shows_Items_Or_Input()
    {
        await GoOrSkip(Base);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasInput = await Page.Locator("input").CountAsync() > 0;
        var hasList  = await Page.Locator("ul li, .item, .adhd-item, table tr").CountAsync() > 0;
        var hasBtn   = await Page.Locator("button").CountAsync() > 0;

        Assert.That(hasInput || hasList || hasBtn, Is.True,
            "ADHD FindIt should show items, a search input, or action buttons");
    }
}
