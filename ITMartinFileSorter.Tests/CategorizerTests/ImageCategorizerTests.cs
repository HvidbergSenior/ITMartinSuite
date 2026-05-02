using ITMartinFileSorter.Application.Services;
using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Tests.CategorizerTests;

[TestFixture]
public class ImageCategorizerTests
{
    private ImageCategorizer _sut;

    [SetUp]
    public void Setup()
    {
        _sut = new ImageCategorizer();
    }

    private MediaFile CreateImage(string name, int? w = null, int? h = null)
    {
        var file = new MediaFile(
            fullPath: name,
            createdAt: DateTime.Now,
            type: MediaType.Image,
            sizeBytes: 1000);

        file.Width = w;
        file.Height = h;

        return file;
    }

    [Test]
    public void Categorize_ScreenshotName_ShouldBeScreenshot()
    {
        var file = CreateImage("screenshot_123.png");

        _sut.Categorize(file);

        Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.Screenshot));
    }

    [Test]
    public void Categorize_MemeName_ShouldBeMeme()
    {
        var file = CreateImage("funny_meme.jpg");

        _sut.Categorize(file);

        Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.Meme));
    }

    [Test]
    public void Categorize_SocialName_ShouldBeSocial()
    {
        var file = CreateImage("whatsapp_image.jpg");

        _sut.Categorize(file);

        Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.Social));
    }

    [Test]
    public void Categorize_Gif_ShouldBeMeme()
    {
        var file = CreateImage("file.gif");

        _sut.Categorize(file);

        Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.Meme));
    }

    [Test]
    public void Categorize_LowResolution_ShouldBeMeme()
    {
        var file = CreateImage("img.jpg", 500, 500);

        _sut.Categorize(file);

        Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.Meme));
    }

    [Test]
    public void Categorize_HighResolution_ShouldBePhoto()
    {
        var file = CreateImage("img.jpg", 4000, 3000);

        _sut.Categorize(file);

        Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.OtherImage));
        Assert.That(file.IsProbablyRealPhoto, Is.True);
    }

    [Test]
    public void Categorize_Default_ShouldBeOtherImage()
    {
        var file = CreateImage("random.png");

        _sut.Categorize(file);

        Assert.That(file.SubCategory, Is.EqualTo(MediaSubCategory.OtherImage));
    }

    [Test]
    public void Categorize_NonImage_ShouldThrow()
    {
        var file = new MediaFile(
            "file.mp3",
            DateTime.Now,
            MediaType.Audio,
            1000);

        Assert.Throws<InvalidOperationException>(() =>
            _sut.Categorize(file));
    }
}