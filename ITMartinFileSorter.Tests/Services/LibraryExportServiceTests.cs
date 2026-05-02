using ITMartinFileSorter.Application.Services;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Tests.Services;

[TestFixture]
public class LibraryExportServiceTests
{
private string _tempRoot;
private LibraryExportService _sut;

private FakeVideoService _video;
private FakeImageService _image;

[SetUp]
public void Setup()
{
    _tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(_tempRoot);

    var pathService = new LibraryPathService(new FakeGpsService());

    _video = new FakeVideoService();
    _image = new FakeImageService();

    _sut = new LibraryExportService(
        pathService,
        _video,
        _image);
}

[TearDown]
public void Cleanup()
{
    if (Directory.Exists(_tempRoot))
        Directory.Delete(_tempRoot, true);
}

// =========================
// HELPERS
// =========================

private MediaFile CreateFile(string name, MediaType type)
{
    var fullPath = Path.Combine(_tempRoot, name);
    File.WriteAllText(fullPath, "test");

    return new MediaFile(
        fullPath,
        DateTime.Now,
        type,
        100);
}

// =========================
// TESTS
// =========================

[Test]
public async Task Export_ShouldCopyFilesToCorrectFolder()
{
    var file = CreateFile("image.jpg", MediaType.Image);
    file.SubCategory = MediaSubCategory.OtherImage;

    var exportRoot = Path.Combine(_tempRoot, "export");

    await _sut.ExportAsync(new[] { file }, exportRoot);

    var files = Directory.GetFiles(exportRoot, "*.*", SearchOption.AllDirectories);

    Assert.That(files.Length, Is.EqualTo(1));
    Assert.That(files[0], Does.Contain("Images"));
}

[Test]
public async Task Export_ShouldRenameFilesCorrectly()
{
    var file = CreateFile("photo.jpg", MediaType.Image);
    file.SubCategory = MediaSubCategory.OtherImage;

    var exportRoot = Path.Combine(_tempRoot, "export");

    await _sut.ExportAsync(new[] { file }, exportRoot);

    var files = Directory.GetFiles(exportRoot, "*.*", SearchOption.AllDirectories);
    var name = Path.GetFileName(files[0]);

    Assert.That(name, Does.Match(@".*_001\..+"));
}

[Test]
public async Task Export_ShouldCreateBaseFolders_WhenFilesExist()
{
    var file = CreateFile("image.jpg", MediaType.Image);
    file.SubCategory = MediaSubCategory.OtherImage;

    var exportRoot = Path.Combine(_tempRoot, "export");

    await _sut.ExportAsync(new[] { file }, exportRoot);

    Assert.That(Directory.Exists(Path.Combine(exportRoot, "Images")), Is.True);
    Assert.That(Directory.Exists(Path.Combine(exportRoot, "Videos")), Is.True);
    Assert.That(Directory.Exists(Path.Combine(exportRoot, "Documents")), Is.True);
}

[Test]
public async Task Export_ShouldCallProgress()
{
    var file = CreateFile("image.jpg", MediaType.Image);
    file.SubCategory = MediaSubCategory.OtherImage;

    var exportRoot = Path.Combine(_tempRoot, "export");

    int calls = 0;

    await _sut.ExportAsync(
        new[] { file },
        exportRoot,
        async (done, total, name, stage) =>
        {
            calls++;
            await Task.CompletedTask;
        });

    Assert.That(calls, Is.GreaterThan(0));
}

[Test]
public async Task Export_ShouldCallConverters()
{
    var file = CreateFile("video.mp4", MediaType.Video);
    file.SubCategory = MediaSubCategory.OtherVideo;

    var exportRoot = Path.Combine(_tempRoot, "export");

    await _sut.ExportAsync(new[] { file }, exportRoot);

    Assert.That(_video.Called, Is.True);
    Assert.That(_image.Called, Is.True);
}

}

// =========================
// FAKES (INTERFACE-BASED)
// =========================


