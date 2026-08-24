using FluentAssertions;
using ITMartin.Media.Infrastructure.Images;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ITMartinFileSorter.Tests.Services.ImageQualityServiceTests;

// Free local blur/solid-color check added 2026-08-24 as the counterpart to
// the paid Claude vision check - see IImageQualityService. Verifies the two
// heuristics against synthetic images with known, deliberate properties
// rather than real photos, so the test doesn't depend on any fixture files.
[TestFixture]
public class ImageQualityServiceTests
{
    private string _dir = "";
    private ImageQualityService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ImageQualityServiceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _service = new ImageQualityService(NullLogger<ImageQualityService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string SaveImage(Image<Rgba32> image, string name)
    {
        var path = Path.Combine(_dir, name);
        image.SaveAsJpeg(path);
        return path;
    }

    private static Image<Rgba32> MakeSolidColorImage(int size = 128) =>
        new(size, size, new Rgba32(120, 120, 120, 255));

    private static Image<Rgba32> MakeCheckerboardImage(int size = 128, int blockSize = 8)
    {
        var image = new Image<Rgba32>(size, size);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var isWhite = ((x / blockSize) + (y / blockSize)) % 2 == 0;
                image[x, y] = isWhite ? new Rgba32(255, 255, 255, 255) : new Rgba32(0, 0, 0, 255);
            }
        }
        return image;
    }

    [Test]
    public async Task A_flat_single_color_image_is_detected_as_solid_color()
    {
        using var image = MakeSolidColorImage();
        var path = SaveImage(image, "solid.jpg");

        var (isBlurry, isSolidColor) = await _service.AnalyzeAsync(path);

        isSolidColor.Should().BeTrue();
    }

    [Test]
    public async Task A_sharp_high_contrast_pattern_is_not_flagged_as_blurry_or_solid_color()
    {
        using var image = MakeCheckerboardImage();
        var path = SaveImage(image, "sharp.jpg");

        var (isBlurry, isSolidColor) = await _service.AnalyzeAsync(path);

        isSolidColor.Should().BeFalse();
        isBlurry.Should().BeFalse();
    }

    [Test]
    public async Task The_same_pattern_heavily_blurred_is_flagged_as_blurry()
    {
        using var sharp = MakeCheckerboardImage();
        using var blurred = sharp.Clone(x => x.GaussianBlur(15));
        var path = SaveImage(blurred, "blurred.jpg");

        var (isBlurry, isSolidColor) = await _service.AnalyzeAsync(path);

        isBlurry.Should().BeTrue();
        isSolidColor.Should().BeFalse();
    }

    [Test]
    public async Task An_undecodable_file_returns_not_blurry_and_not_solid_color_rather_than_throwing()
    {
        var path = Path.Combine(_dir, "not-an-image.jpg");
        File.WriteAllText(path, "this is not image data");

        var act = async () => await _service.AnalyzeAsync(path);

        await act.Should().NotThrowAsync();

        var (isBlurry, isSolidColor) = await _service.AnalyzeAsync(path);
        isBlurry.Should().BeFalse();
        isSolidColor.Should().BeFalse();
    }
}
