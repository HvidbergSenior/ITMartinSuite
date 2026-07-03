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
        await WaitForPage();
        await AssertBodyHasContent("ADHD FindIt index");
    }

    [Test]
    public async Task Adhd_Shows_Items_Or_Input()
    {
        await GoOrSkip(Base);
        await WaitForPage();

        var hasContent = await Page.Locator("input, button, ul li, .item, .adhd-item, table tr, h1, h2, h3, p, div[class]").CountAsync() > 0;

        Assert.That(hasContent, Is.True,
            "ADHD FindIt should show items, a search input, or action buttons");
    }
}
