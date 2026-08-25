using FluentAssertions;
using ITMartin.Media.Application.Services.Steps.NormalizationStep;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace ITMartinFileSorter.Tests.Services.ImageConverterServiceTests;

// Covers the bug found and fixed 2026-08-24: RotationIsCorrect used to be
// hardcoded false for every image because Package1 never checked whether it
// already knew the answer. TryGetSourceOrientation is the cheap, decode-free
// EXIF read that makes "known vs unknown" answerable - these tests verify it
// against synthetic JPEGs with a real EXIF Orientation tag written in.
[TestFixture]
public class ImageConverterServiceTests
{
    private string _dir = "";
    private ImageConverterService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ImageConverterServiceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _service = new ImageConverterService(NullLogger<ImageConverterService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string SaveJpegWithOrientation(ushort? orientation)
    {
        using var image = new Image<Rgba32>(16, 16, new Rgba32(200, 200, 200, 255));

        if (orientation is { } value)
        {
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, value);
        }

        var path = Path.Combine(_dir, $"orientation_{orientation?.ToString() ?? "none"}.jpg");
        image.SaveAsJpeg(path);
        return path;
    }

    [Test]
    public void Returns_true_and_the_real_value_when_a_rotated_orientation_tag_exists()
    {
        var path = SaveJpegWithOrientation(6); // rotate 90 CW

        var known = _service.TryGetSourceOrientation(path, out var orientation);

        known.Should().BeTrue();
        orientation.Should().Be(6);
    }

    [Test]
    public void Returns_true_when_the_tag_explicitly_says_normal()
    {
        var path = SaveJpegWithOrientation(1); // already upright, but still a known answer

        var known = _service.TryGetSourceOrientation(path, out var orientation);

        known.Should().BeTrue();
        orientation.Should().Be(1);
    }

    [Test]
    public void Returns_false_when_no_orientation_tag_exists_at_all()
    {
        var path = SaveJpegWithOrientation(null);

        var known = _service.TryGetSourceOrientation(path, out _);

        known.Should().BeFalse();
    }

    [Test]
    public void Returns_false_for_a_missing_file_instead_of_throwing()
    {
        var path = Path.Combine(_dir, "does-not-exist.jpg");

        var act = () => _service.TryGetSourceOrientation(path, out _);

        act.Should().NotThrow();
        act().Should().BeFalse();
    }
}
