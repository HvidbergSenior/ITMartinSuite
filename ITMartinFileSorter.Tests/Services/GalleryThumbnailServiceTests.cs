using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Infrastructure.Pipelines.FaceIndex;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinFileSorter.Tests.Services;

// Thumbnails used to be skipped whenever one existed. A photo rotated in
// place after the gallery export - by ApplyManualRotations or the cached
// rotation decisions - then kept its sideways thumbnail forever, because
// nothing else ever deletes one. The rule is now "current only if at least
// as new as the source".
[TestFixture]
public class GalleryThumbnailServiceTests
{
    private string _root = "";
    private Mock<IThumbnailService> _thumbs = new();

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "GalleryThumbs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "Billeder", "2019"));
        _thumbs = new Mock<IThumbnailService>();
        _thumbs.Setup(t => t.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((_, output, _) => { File.WriteAllBytes(output, [1]); return Task.FromResult(output); });
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private (string Photo, string Thumb) WritePhotoWithThumb(DateTime photoWrite, DateTime thumbWrite)
    {
        var photo = Path.Combine(_root, "Billeder", "2019", "foto.jpg");
        var thumbDir = Path.Combine(_root, "Billeder", "2019", "thumbnails");
        Directory.CreateDirectory(thumbDir);
        var thumb = Path.Combine(thumbDir, "foto.jpg");
        File.WriteAllBytes(photo, [1, 2, 3]);
        File.WriteAllBytes(thumb, [9]);
        File.SetLastWriteTimeUtc(photo, photoWrite);
        File.SetLastWriteTimeUtc(thumb, thumbWrite);
        return (photo, thumb);
    }

    [Test]
    public async Task An_up_to_date_thumbnail_is_left_alone()
    {
        var t = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
        var (photo, _) = WritePhotoWithThumb(photoWrite: t, thumbWrite: t.AddMinutes(5));

        var generated = await new GalleryThumbnailService(_thumbs.Object, NullLogger<GalleryThumbnailService>.Instance).GenerateAsync(_root);

        generated.Should().Be(0);
        _thumbs.Verify(x => x.GenerateAsync(photo, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task A_photo_changed_after_its_thumbnail_gets_a_new_one()
    {
        var t = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
        var (photo, thumb) = WritePhotoWithThumb(photoWrite: t.AddMinutes(5), thumbWrite: t);

        var generated = await new GalleryThumbnailService(_thumbs.Object, NullLogger<GalleryThumbnailService>.Instance).GenerateAsync(_root);

        generated.Should().Be(1, "a rotated photo must not keep its sideways thumbnail");
        _thumbs.Verify(x => x.GenerateAsync(photo, thumb, It.IsAny<CancellationToken>()), Times.Once);
    }
}
