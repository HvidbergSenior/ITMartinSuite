using ITMartinFileSorter.Application.Services;
using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using NUnit.Framework;

namespace ITMartinFileSorter.Tests.Services;

[TestFixture]
public class DuplicateServiceTests
{
private DuplicateService _sut;

[SetUp]
public void Setup()
{
    _sut = new DuplicateService();
}

// =========================
// HELPERS
// =========================
private MediaFile Create(string name, long size, string? hash)
{
    var file = new MediaFile(
        fullPath: name,
        createdAt: DateTime.Now,
        type: MediaType.Image,
        sizeBytes: size);

    if (hash != null)
        file.SetHash(hash);

    return file;
}

// =========================
// TESTS
// =========================

    [Test]
    public void SameHash_SameSize_SameName_ShouldDeleteDuplicates()
    {
        var date = DateTime.Now;

        var file1 = Create("a.jpg", 1000, "hash");
        var file2 = Create("a.jpg", 1000, "hash");

        file1.SetDate(date, true);
        file2.SetDate(date, true);

        _sut.AllFiles = new() { file1, file2 };

        _sut.BuildDuplicateGroups();

        Assert.That(file1.Status, Is.EqualTo(MediaFileStatus.ToKeep));
        Assert.That(file2.Status, Is.EqualTo(MediaFileStatus.ToDelete));
    }

[Test]
public void SameHash_DifferentSize_ShouldRequireReview()
{
    var file1 = Create("a.jpg", 1000, "hash");
    var file2 = Create("a.jpg", 2000, "hash");

    _sut.AllFiles = new() { file1, file2 };

    _sut.BuildDuplicateGroups();

    Assert.That(file1.Status, Is.EqualTo(MediaFileStatus.ToKeep));
    Assert.That(file2.Status, Is.EqualTo(MediaFileStatus.ToKeep));
    Assert.That(file2.RequiresReview, Is.True);
}

[Test]
public void SameHash_DifferentName_ShouldRequireReview()
{
    var file1 = Create("a.jpg", 1000, "hash");
    var file2 = Create("b.jpg", 1000, "hash");

    _sut.AllFiles = new() { file1, file2 };

    _sut.BuildDuplicateGroups();

    Assert.That(file1.Status, Is.EqualTo(MediaFileStatus.ToKeep));
    Assert.That(file2.Status, Is.EqualTo(MediaFileStatus.ToKeep));
    Assert.That(file2.RequiresReview, Is.True);
}

[Test]
public void SameHash_MultipleFiles_ShouldOnlyKeepOne()
{
    var file1 = Create("a.jpg", 1000, "hash");
    var file2 = Create("a.jpg", 1000, "hash");
    var file3 = Create("a.jpg", 1000, "hash");

    _sut.AllFiles = new() { file1, file2, file3 };

    _sut.BuildDuplicateGroups();

    var keepCount = _sut.AllFiles.Count(f => f.Status == MediaFileStatus.ToKeep);
    var deleteCount = _sut.AllFiles.Count(f => f.Status == MediaFileStatus.ToDelete);

    Assert.That(keepCount, Is.EqualTo(1));
    Assert.That(deleteCount, Is.EqualTo(2));
}

[Test]
public void MissingHash_ShouldThrow()
{
    var file = Create("a.jpg", 1000, null);

    _sut.AllFiles = new() { file };

    Assert.Throws<InvalidOperationException>(() =>
        _sut.BuildDuplicateGroups());
}

[Test]
public void AlreadyProcessedFile_ShouldNotBeOverwritten()
{
    var file1 = Create("a.jpg", 1000, "hash");
    var file2 = Create("a.jpg", 1000, "hash");

    file2.Status = MediaFileStatus.ToKeep; // simulate manual decision

    _sut.AllFiles = new() { file1, file2 };

    _sut.BuildDuplicateGroups();

    Assert.That(file2.Status, Is.EqualTo(MediaFileStatus.ToKeep));
}

[Test]
public void SelectBestFile_ShouldPreferReliableDate()
{
    var older = Create("a.jpg", 1000, "hash");
    older.SetDate(DateTime.Now.AddYears(-5), false);

    var newerReliable = Create("a.jpg", 1000, "hash");
    newerReliable.SetDate(DateTime.Now, true);

    _sut.AllFiles = new() { older, newerReliable };

    _sut.BuildDuplicateGroups();

    Assert.That(newerReliable.Status, Is.EqualTo(MediaFileStatus.ToKeep));
}

}
