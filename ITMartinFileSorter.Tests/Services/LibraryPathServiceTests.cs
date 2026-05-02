using ITMartinFileSorter.Application.Services;
using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;
using NUnit.Framework;

namespace ITMartinFileSorter.Tests.Services;

[TestFixture]
public class LibraryPathServiceTests
{
private LibraryPathService _sut;

[SetUp]
public void Setup()
{
    _sut = new LibraryPathService(new FakeGpsService());
}

// =========================
// HELPERS
// =========================
private MediaFile CreateImage(
    string name,
    DateTime? date,
    bool reliable = true,
    MediaSubCategory sub = MediaSubCategory.OtherImage)
{
    var file = new MediaFile(
        fullPath: name,
        createdAt: date,
        type: MediaType.Image,
        sizeBytes: 1000);

    file.SubCategory = sub;

    if (date != null)
        file.SetDate(date, reliable);

    return file;
}

// =========================
// TESTS
// =========================

[Test]
public void Screenshot_ShouldGoToScreenshotsRoot()
{
    var file = CreateImage(
        "screen.png",
        DateTime.Now,
        sub: MediaSubCategory.Screenshot);

    var result = _sut.BuildFolderPath(file);

    Assert.That(result, Is.EqualTo("Screenshots"));
}

[Test]
public void Meme_ShouldGoToMemesRoot()
{
    var file = CreateImage(
        "meme.png",
        DateTime.Now,
        sub: MediaSubCategory.Meme);

    var result = _sut.BuildFolderPath(file);

    Assert.That(result, Is.EqualTo("Memes"));
}

[Test]
public void TrustedImage_ShouldUseYearMonthStructure()
{
    var date = new DateTime(2022, 5, 10);

    var file = CreateImage("img.jpg", date, reliable: true);

    var result = _sut.BuildFolderPath(file);

    Assert.That(result, Does.StartWith("Images"));
    Assert.That(result, Does.Contain("2022"));
}

[Test]
public void UntrustedImage_ShouldGoToOtherFolder()
{
    var file = CreateImage(
        "img.jpg",
        null,
        reliable: false);

    var result = _sut.BuildFolderPath(file);

    Assert.That(result, Does.StartWith("Other"));
}

[Test]
public void RecentImage_ShouldBeTrustedEvenIfUnreliable()
{
    var recent = DateTime.Now.AddDays(-5);

    var file = CreateImage(
        "img.jpg",
        recent,
        reliable: false);

    var result = _sut.BuildFolderPath(file);

    Assert.That(result, Does.StartWith("Images"));
}

[Test]
public void BuildFileName_ShouldIncludeDateCategoryAndIndex()
{
    var date = new DateTime(2023, 1, 2, 12, 30, 0);

    var file = CreateImage("photo.jpg", date);

    var result = _sut.BuildFileName(file, 1);

    Assert.That(result, Does.Contain("2023-01-02"));
    Assert.That(result, Does.Contain("Photo"));
    Assert.That(result, Does.Contain("001"));
}

[Test]
public void Document_ShouldGoToDocumentsRoot()
{
    var file = new MediaFile(
        "doc.pdf",
        DateTime.Now,
        MediaType.Document,
        1000);

    file.SubCategory = MediaSubCategory.Pdf;

    var result = _sut.BuildFolderPath(file);

    Assert.That(result, Does.StartWith("Documents"));
}

[Test]
public void Audio_ShouldGoToAudioRoot()
{
    var file = new MediaFile(
        "song.mp3",
        DateTime.Now,
        MediaType.Audio,
        1000);

    var result = _sut.BuildFolderPath(file);

    Assert.That(result, Does.StartWith("Audio"));
}

[Test]
public void Video_ShouldGoToVideoRoot()
{
    var file = new MediaFile(
        "video.mp4",
        DateTime.Now,
        MediaType.Video,
        1000);

    var result = _sut.BuildFolderPath(file);

    Assert.That(result, Does.StartWith("Videos"));
}

}


