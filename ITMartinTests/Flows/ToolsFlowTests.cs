namespace ITMartinTests.Flows;

/// <summary>Portal, FileSorter, Budget, Magazine, Magazine Search, ImageGen, CloudOverblik, TestHub, Scan, Upload</summary>
[TestFixture]
[Category("Flow")]
public class ToolsFlowTests : FlowTestBase
{
    // ── Portal ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Portal_Index_Loads_With_Links()
    {
        await GoOrSkip("https://martin.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var linkCount = await Page.Locator("a[href]").CountAsync();
        Assert.That(linkCount, Is.GreaterThan(0), "Portal should have navigation links to other apps");
    }

    // ── FileSorter ────────────────────────────────────────────────────────────

    [Test]
    public async Task FileSorter_Index_Loads()
    {
        await GoOrSkip("https://filesorter.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("FileSorter index");
    }

    [Test]
    public async Task FileSorter_Shows_Jobs_Or_Interface()
    {
        await GoOrSkip("https://filesorter.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasContent = await Page.Locator(".job, .sort-job, ul li, table tr, button, h1, h2").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "FileSorter should show jobs or an interface");
    }

    // ── Budget ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Budget_Index_Loads()
    {
        await GoOrSkip("https://budget.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Budget index");
    }

    [Test]
    public async Task Budget_Shows_Categories_Or_Amounts()
    {
        await GoOrSkip("https://budget.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasContent = await Page.Locator(".budget, .category, table tr, ul li, input, h1, h2").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "Budget should show categories, amounts, or inputs");
    }

    // ── Magazine ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Magazine_Index_Loads()
    {
        await GoOrSkip("https://magazine.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Magazine index");
    }

    [Test]
    public async Task Magazine_Shows_Articles_Or_Content()
    {
        await GoOrSkip("https://magazine.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasContent = await Page.Locator("article, .article, .blad, ul li, .card, h1, h2").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "Magazine should show articles or headings");
    }

    // ── Magazine Search ───────────────────────────────────────────────────────

    [Test]
    public async Task MagazineSearch_Has_Search_Input()
    {
        await GoOrSkip("https://magazine-search.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasInput = await Page.Locator("input").CountAsync() > 0;
        Assert.That(hasInput, Is.True, "Magazine Search should have a search input");
    }

    [Test]
    public async Task MagazineSearch_Returns_Results_Or_Empty()
    {
        await GoOrSkip("https://magazine-search.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var input = Page.Locator("input").First;
        if (await input.CountAsync() == 0)
        {
            Assert.Ignore("No search input on Magazine Search page");
            return;
        }

        await input.FillAsync("natur");
        await input.PressAsync("Enter");
        await Task.Delay(2_000);

        await AssertBodyHasContent("Magazine Search results");
    }

    // ── Image Generator ───────────────────────────────────────────────────────

    [Test]
    public async Task ImageGen_Index_Loads()
    {
        await GoOrSkip("https://imagegen.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("ImageGen index");
    }

    [Test]
    public async Task ImageGen_Has_Prompt_Input()
    {
        await GoOrSkip("https://imagegen.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasInput = await Page.Locator("input, textarea").CountAsync() > 0;
        Assert.That(hasInput, Is.True, "Image Generator should have a prompt input or textarea");
    }

    // ── Cloud Overblik ────────────────────────────────────────────────────────

    [Test]
    public async Task CloudOverblik_Index_Loads()
    {
        await GoOrSkip("https://cloudoverblik.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Cloud Overblik index");
    }

    [Test]
    public async Task CloudOverblik_Shows_Container_Status()
    {
        await GoOrSkip("https://cloudoverblik.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasContent = await Page.Locator(".container, .service, .status, ul li, table tr, h1, h2").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "Cloud Overblik should show container or service status");
    }

    // ── Test Hub ──────────────────────────────────────────────────────────────

    [Test]
    public async Task TestHub_Index_Loads()
    {
        await GoOrSkip("https://test.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Test Hub index");
    }

    // ── Scan ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task Scan_Index_Loads()
    {
        await GoOrSkip("https://scan.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Scan index");
    }

    [Test]
    public async Task Scan_Shows_Upload_Or_Camera_Interface()
    {
        await GoOrSkip("https://scan.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasInterface = await Page.Locator("input[type='file'], button, .scan, .upload, h1, h2").CountAsync() > 0;
        Assert.That(hasInterface, Is.True, "Scan should show a file input, camera, or upload interface");
    }

    // ── Upload ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Upload_Index_Loads()
    {
        await GoOrSkip("https://upload.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });
        await AssertBodyHasContent("Upload index");
    }

    [Test]
    public async Task Upload_Has_File_Input_Or_Drop_Zone()
    {
        await GoOrSkip("https://upload.itmartin.dk");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var hasUpload = await Page.Locator("input[type='file'], .drop-zone, .upload-area, button").CountAsync() > 0;
        Assert.That(hasUpload, Is.True, "Upload should have a file input or drop zone");
    }
}
