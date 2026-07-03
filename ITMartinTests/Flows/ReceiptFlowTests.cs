namespace ITMartinTests.Flows;

[TestFixture]
[Category("Flow")]
public class ReceiptFlowTests : FlowTestBase
{
    private const string Base = "https://kvittering.itmartin.dk";

    [Test]
    public async Task Receipt_Index_Loads()
    {
        await GoOrSkip(Base);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Kvittering index");
    }

    [Test]
    public async Task Receipt_Shows_Login_Or_Content()
    {
        await GoOrSkip(Base);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        // Either a shop login form or receipt list should be visible
        var hasInput   = await Page.Locator("input").CountAsync() > 0;
        var hasContent = await Page.Locator("table, ul, .receipt, .kvittering, h1, h2").CountAsync() > 0;

        Assert.That(hasInput || hasContent, Is.True,
            "Kvittering should show a login form or receipt content");
    }
}
