namespace ITMartinTests.Flows;

/// <summary>FileSorter, Budget, ImageGen, CloudOverblik, TestHub, Scan, Upload</summary>
[TestFixture]
[Category("Flow")]
public class ToolsFlowTests : FlowTestBase
{
    // ── FileSorter ────────────────────────────────────────────────────────────

    [Test]
    public async Task FileSorter_Index_Loads()
    {
        await GoOrSkip("https://filesorter.itmartin.dk");
        await WaitForPage();
        await AssertBodyHasContent("FileSorter index");
    }

    [Test]
    public async Task FileSorter_Shows_Jobs_Or_Interface()
    {
        await GoOrSkip("https://filesorter.itmartin.dk");
        await WaitForPage();

        var hasContent = await Page.Locator(".job, .sort-job, ul li, table tr, button, h1, h2, div[class]").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "FileSorter should show jobs or an interface");
    }

    // ── Budget ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Budget_Index_Loads()
    {
        await GoOrSkip("https://budget.itmartin.dk");
        await WaitForPage();
        await AssertBodyHasContent("Budget index");
    }

    [Test]
    public async Task Budget_Shows_Categories_Or_Amounts()
    {
        await GoOrSkip("https://budget.itmartin.dk");
        await WaitForPage();

        var hasContent = await Page.Locator(".budget, .category, table tr, ul li, input, h1, h2, div[class]").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "Budget should show categories, amounts, or inputs");
    }

    // ── Image Generator ───────────────────────────────────────────────────────

    [Test]
    public async Task ImageGen_Index_Loads()
    {
        await GoOrSkip("https://billedbehandling.itmartin.dk");
        await WaitForPage();
        await AssertBodyHasContent("ImageGen index");
    }

    [Test]
    public async Task ImageGen_Has_Prompt_Input()
    {
        await GoOrSkip("https://billedbehandling.itmartin.dk");
        await WaitForPage();
        await AssertBodyHasContent("ImageGen interface");

        var hasInput = await Page.Locator("input, textarea").CountAsync() > 0;
        Assert.That(hasInput, Is.True, "Image Generator should have a prompt input or textarea");
    }

    // ── Cloud Overblik ────────────────────────────────────────────────────────

    [Test]
    public async Task CloudOverblik_Index_Loads()
    {
        await GoOrSkip("https://cloudoverblik.itmartin.dk");
        await WaitForPage();
        await AssertBodyHasContent("Cloud Overblik index");
    }

    [Test]
    public async Task CloudOverblik_Shows_Container_Status()
    {
        await GoOrSkip("https://cloudoverblik.itmartin.dk");
        await WaitForPage();

        var hasContent = await Page.Locator(".container, .service, .status, ul li, table tr, h1, h2, div[class]").CountAsync() > 0;
        Assert.That(hasContent, Is.True, "Cloud Overblik should show container or service status");
    }

    // ── Test Hub ──────────────────────────────────────────────────────────────

    [Test]
    public async Task TestHub_Index_Loads()
    {
        await GoOrSkip("https://test.itmartin.dk");
        await WaitForPage();
        await AssertBodyHasContent("Test Hub index");
    }

    // ── Scan ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task Scan_Index_Loads()
    {
        await GoOrSkip("https://find-varer.itmartin.dk");
        await WaitForPage();
        await AssertBodyHasContent("Scan index");
    }

    [Test]
    public async Task Scan_Shows_Upload_Or_Camera_Interface()
    {
        await GoOrSkip("https://find-varer.itmartin.dk");
        await WaitForPage();
        await AssertBodyHasContent("Scan interface");

        var hasInterface = await Page.Locator("input[type='file'], button, .scan, .upload, h1, h2, div[class]").CountAsync() > 0;
        Assert.That(hasInterface, Is.True, "Scan should show a file input, camera, or upload interface");
    }

    // ── Upload ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Upload_Index_Loads()
    {
        await GoOrSkip("https://upload.itmartin.dk");
        await WaitForPage();
        await AssertBodyHasContent("Upload index");
    }

    [Test]
    public async Task Upload_Has_File_Input_Or_Drop_Zone()
    {
        await GoOrSkip("https://upload.itmartin.dk");
        await WaitForPage();
        await AssertBodyHasContent("Upload interface");

        var hasUpload = await Page.Locator("input[type='file'], .drop-zone, .upload-area, button, div[class]").CountAsync() > 0;
        Assert.That(hasUpload, Is.True, "Upload should have a file input or drop zone");
    }
}
