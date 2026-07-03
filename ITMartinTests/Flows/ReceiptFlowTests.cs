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
        await WaitForPage();
        await AssertBodyHasContent("Kvittering index");
    }

    [Test]
    public async Task Receipt_Shows_Login_Or_Content()
    {
        await GoOrSkip(Base);
        await WaitForPage();

        var hasInput   = await Page.Locator("input").CountAsync() > 0;
        var hasContent = await Page.Locator("table, ul, .receipt, .kvittering, h1, h2, button").CountAsync() > 0;

        Assert.That(hasInput || hasContent, Is.True,
            "Kvittering should show a login form or receipt content");
    }
}
