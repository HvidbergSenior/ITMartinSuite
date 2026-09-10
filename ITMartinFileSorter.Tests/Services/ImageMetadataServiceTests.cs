using FluentAssertions;
using ITMartin.Media.Infrastructure.Metadata;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace ITMartinFileSorter.Tests.Services;

// GetCameraModel shipped as `throw new NotImplementedException()` while
// MetadataWorkflowStep already called it for every image inside one shared
// try/catch. Every photo was therefore recorded as "metadata extraction
// failed" - 3,816 of 4,351 on the 2026-09-10 test run - and the extractions
// that came after it in that lambda (GPS, dimensions) were skipped too. The
// library still looked broadly right, because the date is read earlier, which
// is exactly why it went unnoticed until the failure list was counted.
//
// These tests are deliberately blunt: they assert the methods RETURN rather
// than throw. A more elaborate fixture would not have caught the actual bug.
[TestFixture]
public class ImageMetadataServiceTests
{
    private string _dir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "imgmeta-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string WriteJpeg(string name, Action<ExifProfile>? exif = null)
    {
        var path = Path.Combine(_dir, name);
        using var image = new Image<Rgb24>(8, 6);

        if (exif is not null)
        {
            image.Metadata.ExifProfile = new ExifProfile();
            exif(image.Metadata.ExifProfile);
        }

        image.SaveAsJpeg(path);
        return path;
    }

    [Test]
    public void GetCameraModel_returns_the_model_from_exif()
    {
        var path = WriteJpeg("with-model.jpg", exif =>
        {
            exif.SetValue(ExifTag.Make, "OLYMPUS IMAGING CORP.");
            exif.SetValue(ExifTag.Model, "C8080WZ");
        });

        var model = new ImageMetadataService().GetCameraModel(path);

        // The substring the rotation filter actually looks for must survive -
        // OrientationUnreliableModelSubstrings matches on "C8080WZ".
        model.Should().NotBeNull();
        model!.Should().Contain("C8080WZ");
    }

    [Test]
    public void GetCameraModel_does_not_repeat_the_make_already_in_the_model()
    {
        var path = WriteJpeg("canon.jpg", exif =>
        {
            exif.SetValue(ExifTag.Make, "Canon");
            exif.SetValue(ExifTag.Model, "Canon EOS 5D");
        });

        var model = new ImageMetadataService().GetCameraModel(path);

        model.Should().Be("Canon EOS 5D");
    }

    [Test]
    public void GetCameraModel_returns_null_for_a_photo_without_exif()
    {
        var path = WriteJpeg("no-exif.jpg");

        // No EXIF is ordinary - it is the very case the manual-rotation review
        // list exists to collect - so it must be null, never an exception.
        new ImageMetadataService().GetCameraModel(path).Should().BeNull();
    }

    [Test]
    public void Every_extractor_returns_rather_than_throws_for_an_unreadable_file()
    {
        var path = Path.Combine(_dir, "not-an-image.jpg");
        File.WriteAllText(path, "this is not a JPEG");

        var service = new ImageMetadataService();

        // The regression that started all this: one extractor throwing took
        // every later extraction for that file down with it.
        service.Invoking(s => s.GetCameraModel(path)).Should().NotThrow();
        service.Invoking(s => s.GetDimensions(path)).Should().NotThrow();
        service.Invoking(s => s.GetCreationTime(path)).Should().NotThrow();
    }

    [Test]
    public void Every_extractor_returns_rather_than_throws_for_a_missing_file()
    {
        var path = Path.Combine(_dir, "does-not-exist.jpg");
        var service = new ImageMetadataService();

        service.Invoking(s => s.GetCameraModel(path)).Should().NotThrow();
        service.Invoking(s => s.GetDimensions(path)).Should().NotThrow();
        service.Invoking(s => s.GetCreationTime(path)).Should().NotThrow();
    }
}
