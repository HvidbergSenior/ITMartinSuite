using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Infrastructure.Services;
using NUnit.Framework;

namespace ITMartinFileSorter.Tests.Services;

[TestFixture]
public class MediaClassificationServiceTests
{
private MediaClassificationService _sut;

[SetUp]
public void Setup()
{
    _sut = new MediaClassificationService();
}

private MediaFile Create(string name, MediaType type)
{
    return new MediaFile(
        fullPath: name,
        createdAt: DateTime.Now,
        type: type,
        sizeBytes: 1000);
}

// =========================
// IMAGE
// =========================

[Test]
public void Image_ScreenshotByName_ShouldBeScreenshot()
{
    var file = Create("screenshot_001.png", MediaType.Image);

    _sut.Classify(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.Screenshot));
}

[Test]
public void Image_ScreenshotBySize_ShouldBeScreenshot()
{
    var file = Create("photo.png", MediaType.Image);
    file.Width = 1000;
    file.Height = 2000; // portrait ratio

    _sut.Classify(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.Screenshot));
}

[Test]
public void Image_WithExif_ShouldBePhonePhoto()
{
    var file = Create("random.jpg", MediaType.Image);
    file.HasExif = true;

    _sut.Classify(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.PhonePhoto));
}

[Test]
public void Image_WithImgPrefix_ShouldBePhonePhoto()
{
    var file = Create("img_1234.jpg", MediaType.Image);

    _sut.Classify(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.PhonePhoto));
}

[Test]
public void Image_Meme_ShouldAddTag()
{
    var file = Create("funny_meme.jpg", MediaType.Image);

    _sut.Classify(file);

    Assert.That(file.Tags.Contains("meme"), Is.True);
    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.OtherImage));
}

[Test]
public void Image_Default_ShouldBeOtherImage()
{
    var file = Create("random.jpg", MediaType.Image);

    _sut.Classify(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.OtherImage));
}

// =========================
// VIDEO
// =========================

[Test]
public void Video_ScreenRecording_ShouldBeScreenRecording()
{
    var file = Create("screenrecord.mp4", MediaType.Video);

    _sut.Classify(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.ScreenRecording));
}

[Test]
public void Video_PhoneVideo_ShouldBePhoneVideo()
{
    var file = Create("vid_123.mp4", MediaType.Video);

    _sut.Classify(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.PhoneVideo));
}

[Test]
public void Video_Default_ShouldBeOtherVideo()
{
    var file = Create("movie.mp4", MediaType.Video);

    _sut.Classify(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.OtherVideo));
}

// =========================
// AUDIO
// =========================

[Test]
public void Audio_VoiceMemo_ShouldBeVoiceMemo()
{
    var file = Create("voice_recording.wav", MediaType.Audio);

    _sut.Classify(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.VoiceMemo));
}

[Test]
public void Audio_Default_ShouldBeMusic()
{
    var file = Create("song.mp3", MediaType.Audio);

    _sut.Classify(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.Music));
}

// =========================
// DOCUMENT
// =========================

[Test]
public void Document_Pdf_ShouldBePdf()
{
    var file = Create("file.pdf", MediaType.Document);

    _sut.Classify(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.Pdf));
}

[Test]
public void Document_Unknown_ShouldFallback()
{
    var file = Create("file.unknown", MediaType.Document);

    _sut.Classify(file);

    Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.UnknownDocument));
}

// =========================
// SOURCE
// =========================

[Test]
public void Source_WhatsApp_ShouldBeDetected()
{
    var file = Create(@"C:\whatsapp\image.jpg", MediaType.Image);

    _sut.Classify(file);

    Assert.That(file.Source, Is.EqualTo(MediaSource.WhatsApp));
}

[Test]
public void Source_Telegram_ShouldBeDetected()
{
    var file = Create(@"C:\telegram\file.jpg", MediaType.Image);

    _sut.Classify(file);

    Assert.That(file.Source, Is.EqualTo(MediaSource.Telegram));
}

[Test]
public void Source_Download_ShouldBeDetected()
{
    var file = Create(@"C:\download\file.jpg", MediaType.Image);

    _sut.Classify(file);

    Assert.That(file.Source, Is.EqualTo(MediaSource.Download));
}

[Test]
public void Source_Default_ShouldBeUnknown()
{
    var file = Create(@"C:\random\file.jpg", MediaType.Image);

    _sut.Classify(file);

    Assert.That(file.Source, Is.EqualTo(MediaSource.Unknown));
}

}
