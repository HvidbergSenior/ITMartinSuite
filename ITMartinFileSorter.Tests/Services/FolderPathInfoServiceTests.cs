using ITMartinFileSorter.Application.Services;
using NUnit.Framework;

namespace ITMartinFileSorter.Tests.Services;

[TestFixture]
public class FolderPathInfoServiceTests
{
    private FolderPathInfoService _sut;

    [SetUp]
    public void Setup()
    {
        _sut = new FolderPathInfoService();
    }

// =========================
// VALID CASES
// =========================

    [Test]
    public void SamePath_ShouldReturnNull()
    {
        var path = @"C:\Data\Files";

        var result = _sut.GetPathInfo(path, path);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void SamePath_WithTrailingSlash_ShouldReturnNull()
    {
        var configured = @"C:\Data\Files\";
        var selected = @"C:\Data\Files";

        var result = _sut.GetPathInfo(configured, selected);

        Assert.That(result, Is.Null);
    }

// =========================
// DIFFERENT PATHS
// =========================

    [Test]
    public void DifferentPaths_ShouldReturnWarningMessage()
    {
        var configured = @"C:\Default";
        var selected = @"D:\Other";

        var result = _sut.GetPathInfo(configured, selected);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("Default"));
        Assert.That(result, Does.Contain("Selected"));
    }

// =========================
// EDGE CASES
// =========================

    [Test]
    public void NullConfiguredPath_ShouldReturnNull()
    {
        var result = _sut.GetPathInfo(null, @"C:\Test");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void NullSelectedPath_ShouldReturnNull()
    {
        var result = _sut.GetPathInfo(@"C:\Test", null);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void EmptyPaths_ShouldReturnNull()
    {
        var result = _sut.GetPathInfo("", "");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void CaseInsensitivePaths_ShouldReturnNull()
    {
        var configured = @"C:\DATA\FILES";
        var selected = @"c:\data\files";

        var result = _sut.GetPathInfo(configured, selected);

        Assert.That(result, Is.Null);
    }

}