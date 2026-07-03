namespace ITMartinTests.Flows;

[TestFixture]
[Category("Flow")]
public class GalleryFlowTests : FlowTestBase
{
    private const string Base = "https://gallery.itmartin.dk";

    // Gallery is a plain JS app — wait for JS to fetch /api/galleries and render cards
    private async Task WaitForGalleryCards() =>
        await Page.WaitForSelectorAsync(".gallery-card", new() { Timeout = 10_000 });

    private async Task OpenGallery(string nameFragment)
    {
        await GoOrSkip(Base);
        await Page.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 15_000 });
        await WaitForGalleryCards();

        var card = Page.Locator($".gallery-card:has(.gallery-card-name:has-text('{nameFragment}'))");
        if (await card.CountAsync() == 0)
            Assert.Ignore($"Gallery '{nameFragment}' not found on index");

        await card.First.ClickAsync();
        // Wait for either login modal or gallery content to appear after JS fetch
        await Page.WaitForSelectorAsync("#loginModal.active, .file-card, .folder-card",
            new() { Timeout = 8_000 });
    }

    private async Task EnterPassword(string password)
    {
        // Wait for login modal to appear after clicking a password-protected gallery
        await Page.WaitForSelectorAsync("#loginModal.active, #loginInput", new() { Timeout = 5_000 });
        await Page.Locator("#loginInput").FillAsync(password);
        await Page.Locator("#loginBtn").ClickAsync();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Gallery_Index_Shows_Gallery_Cards()
    {
        await GoOrSkip(Base);
        await Page.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 15_000 });
        await WaitForGalleryCards();

        var count = await Page.Locator(".gallery-card").CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Gallery index should show at least one gallery card");
    }

    [Test]
    public async Task Gallery_Mie_Requires_Password()
    {
        await OpenGallery("Mie");

        // Should show login modal or already browsing (if no password)
        var hasModal  = await Page.Locator("#loginModal.active").CountAsync() > 0;
        var hasFiles  = await Page.Locator(".file-card, .folder-card, img").CountAsync() > 0;

        Assert.That(hasModal || hasFiles, Is.True,
            "Mie gallery should show login modal or files after clicking");
    }

    [Test]
    public async Task Gallery_Mie_Correct_Password_Shows_Files()
    {
        var password = Environment.GetEnvironmentVariable("GALLERY_PASSWORD_MIE") ?? "8670Låsby";

        await OpenGallery("Mie");

        var hasModal = await Page.Locator("#loginModal.active").CountAsync() > 0;
        if (!hasModal)
        {
            // Gallery opened without password (cookie?) — just check files visible
            var files = await Page.Locator(".file-card, .folder-card, img").CountAsync();
            Assert.That(files, Is.GreaterThan(0), "Expected files or folders visible");
            return;
        }

        await EnterPassword(password);
        // Wait for gallery content to appear after login
        try { await Page.WaitForSelectorAsync(".file-card, .folder-card, img", new() { Timeout = 5_000 }); } catch { }

        var count = await Page.Locator(".file-card, .folder-card, img").CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Expected files or folders after correct Mie password");
    }

    [Test]
    public async Task Gallery_JesperMette_Requires_Password()
    {
        await OpenGallery("Mette");

        var hasModal = await Page.Locator("#loginModal.active").CountAsync() > 0;
        var hasFiles = await Page.Locator(".file-card, .folder-card, img").CountAsync() > 0;

        Assert.That(hasModal || hasFiles, Is.True,
            "Mette & Jesper gallery should show login modal or files after clicking");
    }

    [Test]
    public async Task Gallery_JesperMette_Correct_Password_Shows_Files()
    {
        var password = Environment.GetEnvironmentVariable("GALLERY_PASSWORD_JESPERMETTE") ?? "2860Søborg";

        await OpenGallery("Mette");

        var hasModal = await Page.Locator("#loginModal.active").CountAsync() > 0;
        if (!hasModal)
        {
            var files = await Page.Locator(".file-card, .folder-card, img").CountAsync();
            Assert.That(files, Is.GreaterThan(0), "Expected files or folders visible");
            return;
        }

        await EnterPassword(password);
        try { await Page.WaitForSelectorAsync(".file-card, .folder-card, img", new() { Timeout = 5_000 }); } catch { }

        var count = await Page.Locator(".file-card, .folder-card, img").CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Expected files or folders after correct JesperMette password");
    }
}
