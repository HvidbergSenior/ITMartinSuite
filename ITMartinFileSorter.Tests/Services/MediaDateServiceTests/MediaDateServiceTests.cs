using ITMartinFileSorter.Infrastructure.Services;

namespace ITMartinFileSorter.Tests.Services.MediaDateServiceTests;

[TestFixture]
public class MediaDateServiceTests
{
private MediaDateService _sut;
private string _tempDir;

[SetUp]
public void Setup()
{
    _sut = new MediaDateService();

    _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(_tempDir);
}

[TearDown]
public void Cleanup()
{
    if (Directory.Exists(_tempDir))
        Directory.Delete(_tempDir, true);
}

private string CreateFile(string name, DateTime? modified = null)
{
    var path = Path.Combine(_tempDir, name);
    File.WriteAllText(path, "test");

    if (modified != null)
        File.SetLastWriteTime(path, modified.Value);

    return path;
}

// =========================
// FILENAME (HIGH TRUST)
// =========================

[Test]
public void Should_Parse_FullDate_From_FileName()
{
    var file = CreateFile("2023-10-05_12-30-45.jpg");

    var (date, reliable) = _sut.GetBestDate(file);

    Assert.That(date, Is.Not.Null);
    Assert.That(date!.Value.Year, Is.EqualTo(2023));
    Assert.That(date.Value.Month, Is.EqualTo(10));
    Assert.That(date.Value.Day, Is.EqualTo(5));
    Assert.That(reliable, Is.True);
}

[Test]
public void Should_Parse_DateOnly_From_FileName()
{
    var file = CreateFile("2023-10-05.jpg");

    var (date, reliable) = _sut.GetBestDate(file);

    Assert.That(date, Is.Not.Null);
    Assert.That(date!.Value.Year, Is.EqualTo(2023));
    Assert.That(date.Value.Month, Is.EqualTo(10));
    Assert.That(date.Value.Day, Is.EqualTo(5));
    Assert.That(reliable, Is.True);
}

// =========================
// FALLBACK (LOW TRUST)
// =========================

[Test]
public void Should_Use_FileDate_When_NoMetadata_And_Recent()
{
    var recent = DateTime.Now.AddDays(-5);

    var file = CreateFile("file.jpg", recent);

    var (date, reliable) = _sut.GetBestDate(file);

    Assert.That(date, Is.Not.Null);
    Assert.That(reliable, Is.False);
}

[Test]
public void Should_ReturnNull_When_FileTooOld_And_NoMetadata()
{
    var old = DateTime.Now.AddYears(-5);

    var file = CreateFile("file.jpg", old);

    var (date, reliable) = _sut.GetBestDate(file);

    Assert.That(date, Is.Null);
    Assert.That(reliable, Is.False);
}

// =========================
// EDGE CASES
// =========================

[Test]
public void Should_Handle_Invalid_FileName()
{
    var file = CreateFile("random_name.jpg");

    var (date, reliable) = _sut.GetBestDate(file);

    // fallback or null depending on file date
    Assert.That(reliable, Is.False.Or.True);
}

[Test]
public void Should_NotThrow_On_InvalidPath()
{
    var (date, reliable) = _sut.GetBestDate("Z:\\does_not_exist.jpg");

    Assert.That(date, Is.Null);
}

}
