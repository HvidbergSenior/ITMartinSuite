using ITMartinFileSorter.Application.Services;
using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using NUnit.Framework;

namespace ITMartinFileSorter.Tests.Services;

[TestFixture]
public class AudioCategorizerTests
{
    private AudioCategorizer _sut;

    [SetUp]
    public void Setup()
    {
        _sut = new AudioCategorizer();
    }

    private MediaFile CreateAudio(string extension)
    {
        return new MediaFile(
            fullPath: $"file{extension}",
            createdAt: DateTime.Now,
            type: MediaType.Audio,
            sizeBytes: 1000);
    }

    [Test]
    public void Categorize_Mp3_SetsMusic()
    {
        var file = CreateAudio(".mp3");

        _sut.Categorize(file);

        Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.Music));
    }

    [Test]
    public void Categorize_Flac_SetsMusic()
    {
        var file = CreateAudio(".flac");

        _sut.Categorize(file);

        Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.Music));
    }

    [Test]
    public void Categorize_Wav_SetsVoiceMemo()
    {
        var file = CreateAudio(".wav");

        _sut.Categorize(file);

        Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.VoiceMemo));
    }

    [Test]
    public void Categorize_Aac_SetsVoiceMemo()
    {
        var file = CreateAudio(".aac");

        _sut.Categorize(file);

        Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.VoiceMemo));
    }

    [Test]
    public void Categorize_UnknownExtension_SetsUnknownAudio()
    {
        var file = CreateAudio(".ogg");

        _sut.Categorize(file);

        Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.UnknownAudio));
    }

    [Test]
    public void Categorize_NonAudio_Throws()
    {
        var file = new MediaFile(
            fullPath: "file.jpg",
            createdAt: DateTime.Now,
            type: MediaType.Image,
            sizeBytes: 1000);

        Assert.Throws<InvalidOperationException>(() =>
            _sut.Categorize(file));
    }

    [Test]
    public void Categorize_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _sut.Categorize(null!));
    }
}