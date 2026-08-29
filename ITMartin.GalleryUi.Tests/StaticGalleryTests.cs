using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace ITMartin.GalleryUi.Tests;

// Exercises the *actual generated* static gallery (_Galleri/*.html) on a real
// library, not the export code in isolation - built 2026-08-28 after several
// silent failures (missing video thumbnails, "1. January" placeholder dates)
// went unnoticed for a while because nothing rendered the gallery and looked
// at what a viewer would actually see. Point GALLERY_TEST_LIBRARY_PATH at any
// tenant's library root; defaults to D:\MieFiler.
[TestFixture]
public class StaticGalleryTests : PageTest
{
    private static string LibraryPath =>
        Environment.GetEnvironmentVariable("GALLERY_TEST_LIBRARY_PATH") ?? @"D:\MieFiler";

    private static string GalleryRoot => Path.Combine(LibraryPath, "_Galleri");

    private sealed record GalleryItemDto(string T, string F, bool V, bool W, string D);

    private static IEnumerable<string> YearMediaPages() =>
        Directory.Exists(GalleryRoot)
            ? Directory.EnumerateFiles(GalleryRoot, "*-billeder.html")
                .Concat(Directory.EnumerateFiles(GalleryRoot, "*-videoer.html"))
                .OrderBy(f => f)
            : Enumerable.Empty<string>();

    private async Task<List<GalleryItemDto>> LoadItemsAsync(string htmlPath)
    {
        await Page.GotoAsync(new Uri(htmlPath).AbsoluteUri);
        var json = await Page.EvaluateAsync<JsonElement>("items");
        var items = new List<GalleryItemDto>();
        foreach (var el in json.EnumerateArray())
        {
            items.Add(new GalleryItemDto(
                el.GetProperty("t").GetString()!,
                el.GetProperty("f").GetString()!,
                el.GetProperty("v").GetBoolean(),
                el.GetProperty("w").GetBoolean(),
                el.GetProperty("d").GetString()!));
        }
        return items;
    }

    [Test]
    [TestCaseSource(nameof(YearMediaPages))]
    public async Task AllReferencedFiles_ExistOnDisk(string htmlPath)
    {
        var items = await LoadItemsAsync(htmlPath);
        items.Should().NotBeEmpty($"{Path.GetFileName(htmlPath)} should have rendered at least one item");

        var missing = items
            .SelectMany(i => new[] { ("thumb", i.T), ("full", i.F) })
            .Where(pair => !File.Exists(Path.Combine(GalleryRoot, pair.Item2.Replace('/', Path.DirectorySeparatorChar))))
            .ToList();

        missing.Should().BeEmpty(
            $"every thumb/full path referenced by {Path.GetFileName(htmlPath)} must point at a real file - " +
            "a missing one means the gallery is showing (or claims to show) something that isn't actually there");
    }

    // The exact regression found 2026-08-28: a missing ffprobe.exe made every
    // single video in every year fall back to a year-only placeholder date.
    // One or two undated items in a small year is normal; 100% of a
    // non-trivial year is the signature of the whole date pipeline being
    // broken, not genuinely-undated photos.
    [Test]
    [TestCaseSource(nameof(YearMediaPages))]
    public async Task NotEveryItemInAYear_HasAnUnknownDate(string htmlPath)
    {
        var items = await LoadItemsAsync(htmlPath);
        if (items.Count < 20) return; // too small a sample to say anything meaningful

        var unknownCount = items.Count(i => i.D.Contains("dato ukendt"));
        var unknownRatio = (double)unknownCount / items.Count;

        unknownRatio.Should().BeLessThan(0.95,
            $"{Path.GetFileName(htmlPath)}: {unknownCount}/{items.Count} items have an unknown date - " +
            "if that's ~100%, date detection for this media type is silently broken, not just missing metadata on a few files");
    }

    // Proves the thumbnail files aren't just present but are actually
    // decodable images a browser can render - catches a 0-byte or corrupt
    // thumbnail that would pass the file-exists check above but still show
    // as a broken image to a real viewer.
    [Test]
    [TestCaseSource(nameof(YearMediaPages))]
    public async Task SampledThumbnails_RenderAsRealImages(string htmlPath)
    {
        await Page.GotoAsync(new Uri(htmlPath).AbsoluteUri);
        var images = Page.Locator(".grid img, #grid img, #gridPhotos img, #gridVideos img");
        var count = await images.CountAsync();
        if (count == 0) return;

        var sampleSize = Math.Min(count, 25);
        for (var i = 0; i < sampleSize; i++)
        {
            var img = images.Nth(i * count / sampleSize);
            // Grid thumbnails use loading="lazy" - a browser only fetches
            // the image once it's near the viewport, so naturalWidth is 0
            // until scrolled into view regardless of whether the file is fine.
            await img.ScrollIntoViewIfNeededAsync();
            await Expect(img).ToBeVisibleAsync();
            int naturalWidth;
            try
            {
                await Expect(img).ToHaveJSPropertyAsync("complete", true, new() { Timeout = 5000 });
                naturalWidth = await img.EvaluateAsync<int>("el => el.naturalWidth");
            }
            catch (TimeoutException)
            {
                naturalWidth = 0;
            }
            naturalWidth.Should().BeGreaterThan(0,
                $"thumbnail #{i} on {Path.GetFileName(htmlPath)} loaded as a broken image (naturalWidth=0)");
        }
    }

    [Test]
    public async Task IndexPage_YearCounts_MatchActualBilllederPageItemCounts()
    {
        var indexPath = Path.Combine(LibraryPath, "index.html");
        File.Exists(indexPath).Should().BeTrue("the library root must have an index.html landing page");

        await Page.GotoAsync(new Uri(indexPath).AbsoluteUri);
        var cardCounts = await Page.EvaluateAsync<Dictionary<string, int>>(
            @"() => Object.fromEntries(
                [...document.querySelectorAll('a.card')].map(a => {
                    const year = a.querySelector('.label')?.firstChild?.textContent?.trim();
                    const count = a.querySelector('.count')?.textContent?.match(/\d+/)?.[0];
                    return [year, count ? parseInt(count) : -1];
                }).filter(([year]) => /^\d{4}$/.test(year))
            )");

        foreach (var (year, claimedCount) in cardCounts)
        {
            var billederPath = Path.Combine(GalleryRoot, $"{year}-billeder.html");
            if (!File.Exists(billederPath)) continue;

            var items = await LoadItemsAsync(billederPath);
            items.Count.Should().Be(claimedCount,
                $"index.html claims {claimedCount} files for {year} but {year}-billeder.html actually has {items.Count}");
        }
    }
}
