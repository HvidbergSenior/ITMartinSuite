using ITMartinFileSorter.Application.Services;
using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;
using NUnit.Framework;

namespace ITMartinFileSorter.Tests.CategorizerTests;

[TestFixture]
public class MediaCategorizerTests
{
private MediaCategorizer _sut;

[SetUp]
public void Setup()
{
    var categorizers = new IMediaSubCategorizer[]
    {
        new ImageCategorizer(),
        new VideoCategorizer(),
        new AudioCategorizer(),
        new DocumentCategorizer()
    };

    _sut = new MediaCategorizer(categorizers);
}

private MediaFile Create(string name, MediaType type)
{
    return new MediaFile(
        fullPath: name,
        createdAt: DateTime.Now,
        type: type,
        sizeBytes: 1000);
}

[Test]
public void Image_ShouldUseImageCategorizer()
{
    var file = Create("screenshot.png", MediaType.Image);

    _sut.Categorize(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.Screenshot));
}

[Test]
public void Video_ShouldUseVideoCategorizer()
{
    var file = Create("screen_recording.mp4", MediaType.Video);

    _sut.Categorize(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.ScreenRecording));
}

[Test]
public void Audio_ShouldUseAudioCategorizer()
{
    var file = Create("song.mp3", MediaType.Audio);

    _sut.Categorize(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.Music));
}

[Test]
public void Document_ShouldUseDocumentCategorizer()
{
    var file = Create("file.pdf", MediaType.Document);

    _sut.Categorize(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.Pdf));
}

[Test]
public void UnknownExtension_ShouldFallbackCorrectly()
{
    var file = Create("file.unknown", MediaType.Document);

    _sut.Categorize(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.UnknownDocument));
}

[Test]
public void UnsupportedType_ShouldThrow()
{
    var file = Create("file.xyz", (MediaType)999);

    Assert.Throws<InvalidOperationException>(() =>
        _sut.Categorize(file));
}

}
