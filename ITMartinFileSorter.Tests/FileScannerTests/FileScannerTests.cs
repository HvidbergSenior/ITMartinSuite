using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;
using ITMartinFileSorter.Infrastructure.FileSystem;
using NUnit.Framework;

namespace ITMartinFileSorter.Tests.Infrastructure;

[TestFixture]
public class FileScannerTests
{
private string _tempDir;

[SetUp]
public void Setup()
{
    _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(_tempDir);
}

[TearDown]
public void Cleanup()
{
    if (Directory.Exists(_tempDir))
        Directory.Delete(_tempDir, true);
}

// =========================
// HELPERS
// =========================

private string CreateFile(string name)
{
    var path = Path.Combine(_tempDir, name);
    File.WriteAllText(path, "test");
    return path;
}

private FileScanner CreateScanner()
{
    return new FileScanner(
        new RealFileSystem(),          // ✅ NEW
        new FakeHashService(),
        new FakeDateService(),
        new FakeClassificationService(),
        new FakeExifService());       // ✅ NEW
}

// =========================
// TESTS
// =========================

[Test]
public void ScanFolder_ShouldReturnSupportedFilesOnly()
{
    CreateFile("a.jpg");
    CreateFile("b.mp4");
    CreateFile("c.txt");
    CreateFile("d.xyz");

    var scanner = CreateScanner();

    var result = scanner.ScanFolder(_tempDir).ToList();

    Assert.That(result.Count, Is.EqualTo(3));
}

[Test]
public void ScanFolder_ShouldSetCorrectMediaType()
{
    CreateFile("image.jpg");
    CreateFile("video.mp4");
    CreateFile("audio.mp3");
    CreateFile("doc.pdf");

    var scanner = CreateScanner();

    var result = scanner.ScanFolder(_tempDir).ToList();

    Assert.That(result.Any(f => f.Type == MediaType.Image), Is.True);
    Assert.That(result.Any(f => f.Type == MediaType.Video), Is.True);
    Assert.That(result.Any(f => f.Type == MediaType.Audio), Is.True);
    Assert.That(result.Any(f => f.Type == MediaType.Document), Is.True);
}

[Test]
public void ScanFolder_ShouldAssignHash()
{
    CreateFile("a.jpg");

    var scanner = CreateScanner();

    var file = scanner.ScanFolder(_tempDir).First();

    Assert.That(file.Hash, Is.Not.Null);
}

[Test]
public void ScanFolder_ShouldNotCrashOnHashFailure()
{
    CreateFile("a.jpg");

    var scanner = new FileScanner(
        new RealFileSystem(),
        new FailingHashService(),
        new FakeDateService(),
        new FakeClassificationService(),
        new FakeExifService());

    var result = scanner.ScanFolder(_tempDir).ToList();

    Assert.That(result.Count, Is.EqualTo(1));
}

[Test]
public void ScanFolder_ShouldCallClassification()
{
    CreateFile("a.jpg");

    var classifier = new TrackingClassificationService();

    var scanner = new FileScanner(
        new RealFileSystem(),
        new FakeHashService(),
        new FakeDateService(),
        classifier,
        new FakeExifService());

    scanner.ScanFolder(_tempDir).ToList();

    Assert.That(classifier.Called, Is.True);
}

[Test]
public void ScanFolder_ShouldReportProgress()
{
    CreateFile("a.jpg");
    CreateFile("b.jpg");

    var scanner = CreateScanner();

    int calls = 0;

    scanner.ScanFolder(_tempDir, (i, f) => calls++).ToList();

    Assert.That(calls, Is.EqualTo(2));
}

}

// =========================
// FAKES
// =========================

public class RealFileSystem : IFileSystem
{
public IEnumerable<string> EnumerateFiles(string root, string pattern, EnumerationOptions options)
=> Directory.EnumerateFiles(root, pattern, options);

public bool DirectoryExists(string path)
    => Directory.Exists(path);

public long GetFileSize(string path)
    => new FileInfo(path).Length;

public DateTime GetLastWriteTime(string path)
    => File.GetLastWriteTime(path);

}

public class FakeExifService : IExifService
{
public (int? Width, int? Height) GetDimensions(string path)
=> (1920, 1080);

public (string? Make, string? Model, string? Software)? ReadMetadata(string path)
    => null;

public bool IsIphone(string path) => false;
public bool IsAndroid(string path) => false;

}

public class FakeHashService : IHashService
{
public string ComputeHash(string path) => "hash";
}

public class FailingHashService : IHashService
{
public string ComputeHash(string path) => throw new Exception("fail");
}

public class FakeDateService : IMediaDateService
{
public (DateTime? date, bool isReliable) GetBestDate(string path)
=> (DateTime.Now, true);
}

public class FakeClassificationService : IMediaClassificationService
{
public void Classify(MediaFile file)
{
file.SubCategory = MediaSubCategory.OtherImage;
}
}

public class TrackingClassificationService : IMediaClassificationService
{
public bool Called { get; private set; }

public void Classify(MediaFile file)
{
    Called = true;
}

}
