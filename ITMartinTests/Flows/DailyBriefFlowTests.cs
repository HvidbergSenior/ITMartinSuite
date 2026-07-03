namespace ITMartinTests.Flows;

[TestFixture]
[Category("Flow")]
public class DailyBriefFlowTests : FlowTestBase
{
    private static string Url =>
        Environment.GetEnvironmentVariable("DAILYBRIEF_URL") ?? "https://dagligenyheder.itmartin.dk";

    [Test]
    public async Task DailyBrief_Loads_And_Shows_News_UI()
    {
        await GoOrSkip(Url);
        await WaitForPage(20_000);
        await AssertBodyHasContent("DailyBrief");

        var body = await Page.ContentAsync();
        Assert.That(body, Does.Contain("blazor").Or.Contain("nyheder").IgnoreCase,
            "DailyBrief should load its Blazor shell");
    }

    [Test]
    public async Task DailyBrief_Shows_News_Sources()
    {
        await GoOrSkip(Url);
        await WaitForPage(20_000);
        await Task.Delay(3_000); // allow Blazor circuit to connect and load sources
        await AssertBodyHasContent("DailyBrief news sources");

        var body = await Page.ContentAsync();
        Assert.That(body,
            Does.Contain("DR").Or.Contain("TV2").Or.Contain("Nyheder").Or.Contain("nyhed"),
            "DailyBrief should mention known news sources (DR, TV2)");
    }
}
