using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Infrastructure.Pipelines.FaceIndex;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinFileSorter.Tests.Services;

// Covers the two pieces of real logic in LibraryFinishingService - which
// folders are "a real category" (matters for Dedup, which must never touch
// SmartFolders/Review/the quarantine folders) and which years get a
// Yearbook call. Both are static/pure, tested directly against real temp
// folders rather than the whole service.
[TestFixture]
public class LibraryFinishingServiceTests
{
    private string _root = "";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "LibraryFinishingServiceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [Test]
    public void DiscoverRealCategoryFolders_finds_real_categories_only()
    {
        foreach (var name in new[] { "Billeder", "Videoer", "Musik" })
            Directory.CreateDirectory(Path.Combine(_root, name));

        var found = LibraryFinishingService.DiscoverRealCategoryFolders(_root);

        found.Select(Path.GetFileName).Should().BeEquivalentTo("Billeder", "Videoer", "Musik");
    }

    [Test]
    public void DiscoverRealCategoryFolders_excludes_addon_and_quarantine_folders()
    {
        foreach (var name in new[]
                 {
                     "Billeder", "SmartFolders", "Review", "_Galleri",
                     LibraryPolishService.DuplicatesRemovedFolderName,
                     LibraryPolishService.SmallAlbumsRemovedFolderName,
                     LibraryPolishService.UnplayableFolderName,
                     LibraryPolishService.RotationUnknownFolderName,
                     ".zip-source", ".dvd-source", ".package1",
                 })
        {
            Directory.CreateDirectory(Path.Combine(_root, name));
        }

        var found = LibraryFinishingService.DiscoverRealCategoryFolders(_root)
            .Select(Path.GetFileName)
            .ToList();

        found.Should().BeEquivalentTo("Billeder");
    }

    [Test]
    public void DiscoverYears_finds_year_folders_under_year_bucketed_categories_only()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Billeder", "2020"));
        Directory.CreateDirectory(Path.Combine(_root, "Billeder", "2023"));
        Directory.CreateDirectory(Path.Combine(_root, "Videoer", "2023")); // same year, different category - should not duplicate
        Directory.CreateDirectory(Path.Combine(_root, "Billeder", "Ukendt måned")); // not a year folder
        Directory.CreateDirectory(Path.Combine(_root, "Musik", "2019")); // not year-bucketed - should be ignored

        var years = LibraryFinishingService.DiscoverYears(_root);

        years.Should().Equal(2020, 2023);
    }

    [Test]
    public void DiscoverYears_returns_empty_for_a_library_with_no_year_folders()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Billeder"));

        LibraryFinishingService.DiscoverYears(_root).Should().BeEmpty();
    }

    // The rotation passes infer orientation by running local face-detection
    // on every image. That is ~24 seconds per photo on real hardware: on the
    // ToshibaTest library it managed ~600 of 38,367 images in over four
    // hours, and because it ran as the first phase of this chain it sat in
    // front of push-to-nas and wire-gallery, leaving a fully sorted library
    // undeliverable. The user's instruction is that no rotation scan runs
    // programmatically at all.
    //
    // This has now regressed twice - removed from the QuickSort pipeline
    // 2026-09-03, then reintroduced here - so it is pinned with a test
    // rather than a comment. If this fails, do not "fix" it by updating the
    // assertion.
    [Test]
    public async Task Never_runs_a_rotation_scan()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Billeder"));

        var polish = new Mock<ILibraryPolishService>();
        var smartFolders = new Mock<ISmartFoldersService>();
        var galleryExport = new Mock<IStaticGalleryExportService>();
        var verify = new Mock<ILibraryVerifyService>();

        var service = new LibraryFinishingService(
            polish.Object,
            smartFolders.Object,
            galleryExport.Object,
            verify.Object,
            NullLogger<LibraryFinishingService>.Instance);

        await service.RunAsync(_root);

        polish.Verify(
            p => p.FixOrientationFreeOnlyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        polish.Verify(
            p => p.FixOrientationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        polish.Verify(
            p => p.DetectRotatedImagesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
