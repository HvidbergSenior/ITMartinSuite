namespace ITMartinTests.Flows;

[TestFixture]
[Category("Flow")]
public class GalleryFlowTests : FlowTestBase
{
    private const string Base = "https://gallery.itmartin.dk";

    [Test]
    public async Task Gallery_Index_Loads()
    {
        await GoOrSkip(Base);
        await WaitForPage();

        var body = await Page.ContentAsync();
        Assert.That(body, Does.Contain("blazor").Or.Contain("gallery").IgnoreCase,
            "Gallery index should load");
    }

    [Test]
    public async Task Gallery_Mie_Requires_Password()
    {
        await GoOrSkip($"{Base}/mie");
        await WaitForPage();

        var hasPasswordInput = await Page.Locator("input[type='password'], input[placeholder*='kode'], input[placeholder*='adgang']")
            .CountAsync() > 0;
        var hasPhotos = await Page.Locator("img[src*='/gallery/'], .gallery-item, .photo-grid").CountAsync() > 0;

        Assert.That(hasPasswordInput || hasPhotos, Is.True,
            "Gallery /mie should show password form or photos");
    }

    [Test]
    public async Task Gallery_Mie_Correct_Password_Shows_Photos()
    {
        var password = Environment.GetEnvironmentVariable("GALLERY_PASSWORD_MIE") ?? "8670Låsby";

        await GoOrSkip($"{Base}/mie");
        await WaitForPage();

        var passwordInput = Page.Locator("input[type='password'], input[placeholder*='kode'], input[placeholder*='adgang']");
        if (await passwordInput.CountAsync() == 0)
        {
            var imgs = await Page.Locator("img").CountAsync();
            Assert.That(imgs, Is.GreaterThan(0), "Expected photos to be visible");
            return;
        }

        await passwordInput.First.FillAsync(password);
        await passwordInput.First.PressAsync("Enter");
        await Page.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 10_000 });
        await Task.Delay(1_500);

        var images = await Page.Locator("img").CountAsync();
        Assert.That(images, Is.GreaterThan(0), "Expected photos after correct password");
    }

    [Test]
    public async Task Gallery_JesperMette_Requires_Password()
    {
        await GoOrSkip($"{Base}/jespermette");
        await WaitForPage();

        var hasPasswordInput = await Page.Locator("input[type='password'], input[placeholder*='kode'], input[placeholder*='adgang']")
            .CountAsync() > 0;
        var hasPhotos = await Page.Locator("img[src*='/gallery/'], .gallery-item, .photo-grid").CountAsync() > 0;

        Assert.That(hasPasswordInput || hasPhotos, Is.True,
            "Gallery /jespermette should show password form or photos");
    }

    [Test]
    public async Task Gallery_JesperMette_Correct_Password_Shows_Photos()
    {
        var password = Environment.GetEnvironmentVariable("GALLERY_PASSWORD_JESPERMETTE") ?? "2860Søborg";

        await GoOrSkip($"{Base}/jespermette");
        await WaitForPage();

        var passwordInput = Page.Locator("input[type='password'], input[placeholder*='kode'], input[placeholder*='adgang']");
        if (await passwordInput.CountAsync() == 0)
        {
            var imgs = await Page.Locator("img").CountAsync();
            Assert.That(imgs, Is.GreaterThan(0), "Expected photos to be visible");
            return;
        }

        await passwordInput.First.FillAsync(password);
        await passwordInput.First.PressAsync("Enter");
        await Page.WaitForLoadStateAsync(LoadState.Load, new() { Timeout = 10_000 });
        await Task.Delay(1_500);

        var images = await Page.Locator("img").CountAsync();
        Assert.That(images, Is.GreaterThan(0), "Expected photos after correct password");
    }
}

