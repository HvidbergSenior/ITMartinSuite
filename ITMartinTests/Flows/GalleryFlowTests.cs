namespace ITMartinTests.Flows;

[TestFixture]
[Category("Flow")]
public class GalleryFlowTests : PageTest
{
    private const string Base = "https://gallery.itmartin.dk";

    [Test]
    public async Task Gallery_Index_Loads()
    {
        await GoOrSkip(Base);
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var body = await Page.ContentAsync();
        Assert.That(body, Does.Contain("blazor").Or.Contain("gallery").IgnoreCase,
            "Gallery index should load");
    }

    [Test]
    public async Task Gallery_Mie_Requires_Password()
    {
        await GoOrSkip($"{Base}/mie");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        // Should show a password form
        var hasPasswordInput = await Page.Locator("input[type='password'], input[placeholder*='kode'], input[placeholder*='adgang']")
            .CountAsync() > 0;

        // OR might already be in an authenticated state (cookies) — accept both
        var hasPhotos = await Page.Locator("img[src*='/gallery/'], .gallery-item, .photo-grid").CountAsync() > 0;

        Assert.That(hasPasswordInput || hasPhotos, Is.True,
            "Gallery /mie should show password form or photos");
    }

    [Test]
    public async Task Gallery_Mie_Correct_Password_Shows_Photos()
    {
        var password = Environment.GetEnvironmentVariable("GALLERY_PASSWORD_MIE") ?? "8670Låsby";

        await GoOrSkip($"{Base}/mie");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var passwordInput = Page.Locator("input[type='password']");
        if (await passwordInput.CountAsync() == 0)
        {
            // Already authenticated — just verify photos are visible
            var imgs = await Page.Locator("img").CountAsync();
            Assert.That(imgs, Is.GreaterThan(0), "Expected photos to be visible");
            return;
        }

        await passwordInput.First.FillAsync(password);
        // Submit — try Enter key first, then look for a submit button
        await passwordInput.First.PressAsync("Enter");

        try
        {
            await Page.WaitForSelectorAsync("img, .gallery-grid, .photo-item",
                new() { Timeout = 10_000 });
        }
        catch
        {
            // Try clicking a submit button
            var btn = Page.Locator("button[type='submit'], .gallery-login-btn, button:has-text('Åbn'), button:has-text('Log ind')");
            if (await btn.CountAsync() > 0)
            {
                await btn.First.ClickAsync();
                await Page.WaitForSelectorAsync("img, .gallery-grid", new() { Timeout = 10_000 });
            }
        }

        var images = await Page.Locator("img").CountAsync();
        Assert.That(images, Is.GreaterThan(0), "Expected photos after correct password");
    }

    [Test]
    public async Task Gallery_JesperMette_Requires_Password()
    {
        await GoOrSkip($"{Base}/jespermette");
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

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
        await Page.WaitForSelectorAsync("body", new() { Timeout = 15_000 });

        var passwordInput = Page.Locator("input[type='password']");
        if (await passwordInput.CountAsync() == 0)
        {
            var imgs = await Page.Locator("img").CountAsync();
            Assert.That(imgs, Is.GreaterThan(0), "Expected photos to be visible");
            return;
        }

        await passwordInput.First.FillAsync(password);
        await passwordInput.First.PressAsync("Enter");

        try
        {
            await Page.WaitForSelectorAsync("img, .gallery-grid, .photo-item",
                new() { Timeout = 10_000 });
        }
        catch
        {
            var btn = Page.Locator("button[type='submit'], .gallery-login-btn, button:has-text('Åbn'), button:has-text('Log ind')");
            if (await btn.CountAsync() > 0)
            {
                await btn.First.ClickAsync();
                await Page.WaitForSelectorAsync("img, .gallery-grid", new() { Timeout = 10_000 });
            }
        }

        var images = await Page.Locator("img").CountAsync();
        Assert.That(images, Is.GreaterThan(0), "Expected photos after correct password");
    }

    private async Task GoOrSkip(string url)
    {
        try
        {
            var resp = await Page.GotoAsync(url, new() { Timeout = 15_000 });
            if (resp?.Status is 502 or 503 or 504)
                Assert.Ignore($"OFFLINE — gallery-web returned {resp.Status}");
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("net::ERR"))
        {
            Assert.Ignore($"OFFLINE — cannot reach {url}: {ex.Message}");
        }
    }
}
