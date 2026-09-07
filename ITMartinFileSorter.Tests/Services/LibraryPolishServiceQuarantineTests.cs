using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Infrastructure.Pipelines.FaceIndex;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinFileSorter.Tests.Services;

// Requested 2026-09-07: DeduplicateFolderAsync and PruneSmallAlbumsAsync used
// to permanently File.Delete/Directory.Delete real files - on a real
// customer photo library there's no acceptable margin for a wrong guess.
// Both now MOVE into a named quarantine folder at the library root
// ("Dubletter" / "SmåAlbummer") instead. These are confirmation tests on
// that folder structure - the thing actually being verified is "did the file
// really end up recoverable, not gone."
[TestFixture]
public class LibraryPolishServiceQuarantineTests
{
    private string _root = "";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "LibraryPolishQuarantineTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private static LibraryPolishService CreateService()
    {
        // DeduplicateFolderAsync/PruneSmallAlbumsAsync only ever touch the
        // filesystem directly + the logger + IPerceptualHashService (near-dup
        // tier) - every other dependency is exercised by other methods on
        // this service, not these two, so a loose mock (no .Setup calls,
        // returns default/null for anything unused) is enough here.
        var perceptualHash = new Mock<IPerceptualHashService>();

        return new LibraryPolishService(
            NullLogger<LibraryPolishService>.Instance,
            Mock.Of<IDbContextFactory<MediaDbContext>>(),
            Mock.Of<IVideoMetadataService>(),
            Mock.Of<IMediaDateService>(),
            Mock.Of<IExifService>(),
            perceptualHash.Object,
            () => Mock.Of<IFaceRecognitionService>(),
            Mock.Of<IImageAnalysisService>(),
            Mock.Of<IDuplicateService>(),
            Mock.Of<IFileStatusRegistryService>(),
            Mock.Of<IAudioConverterService>(),
            Mock.Of<IImageConverterService>(),
            Mock.Of<IConfiguration>());
    }

    [Test]
    public async Task Exact_duplicate_is_moved_to_Dubletter_not_deleted()
    {
        var billeder = Path.Combine(_root, "Billeder");
        Directory.CreateDirectory(billeder);
        var bytes = new byte[] { 1, 2, 3, 4 };
        File.WriteAllBytes(Path.Combine(billeder, "a_original.jpg"), bytes);
        File.WriteAllBytes(Path.Combine(billeder, "b_duplicate.jpg"), bytes);

        var service = CreateService();
        var result = await service.DeduplicateFolderAsync(billeder);

        result.Deleted.Should().Be(1);

        // Winner (first by ordinal filename) stays put.
        File.Exists(Path.Combine(billeder, "a_original.jpg")).Should().BeTrue();

        // Loser is gone from Billeder...
        File.Exists(Path.Combine(billeder, "b_duplicate.jpg")).Should().BeFalse();

        // ...but recoverable in Dubletter/, at the library root (sibling of
        // Billeder, not buried inside it), preserving its original relative
        // path under the category it came from.
        var quarantined = Path.Combine(_root, LibraryPolishService.DuplicatesRemovedFolderName, "Billeder", "b_duplicate.jpg");
        File.Exists(quarantined).Should().BeTrue();
        File.ReadAllBytes(quarantined).Should().Equal(bytes);
    }

    [Test]
    public async Task Non_duplicate_files_are_left_alone()
    {
        var billeder = Path.Combine(_root, "Billeder");
        Directory.CreateDirectory(billeder);
        File.WriteAllBytes(Path.Combine(billeder, "one.jpg"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(billeder, "two.jpg"), [4, 5, 6]);

        var service = CreateService();
        var result = await service.DeduplicateFolderAsync(billeder);

        result.Deleted.Should().Be(0);
        File.Exists(Path.Combine(billeder, "one.jpg")).Should().BeTrue();
        File.Exists(Path.Combine(billeder, "two.jpg")).Should().BeTrue();
        Directory.Exists(Path.Combine(_root, LibraryPolishService.DuplicatesRemovedFolderName)).Should().BeFalse();
    }

    [Test]
    public async Task Small_album_is_moved_to_SmaAlbummer_not_deleted()
    {
        var albumDir = Path.Combine(_root, "Musik", "Some Artist", "One Track Wonder");
        Directory.CreateDirectory(albumDir);
        File.WriteAllBytes(Path.Combine(albumDir, "track1.mp3"), [1, 2, 3]);

        var service = CreateService();
        var result = await service.PruneSmallAlbumsAsync(_root, minTracks: 6);

        result.AlbumsRemoved.Should().Be(1);
        result.FilesRemoved.Should().Be(1);

        Directory.Exists(albumDir).Should().BeFalse();

        var quarantined = Path.Combine(_root, LibraryPolishService.SmallAlbumsRemovedFolderName, "Musik", "Some Artist", "One Track Wonder", "track1.mp3");
        File.Exists(quarantined).Should().BeTrue();
    }

    [Test]
    public async Task Album_meeting_the_track_threshold_is_kept_in_place()
    {
        var albumDir = Path.Combine(_root, "Musik", "Real Artist", "Full Album");
        Directory.CreateDirectory(albumDir);
        for (var i = 1; i <= 8; i++)
            File.WriteAllBytes(Path.Combine(albumDir, $"track{i}.mp3"), [1, 2, 3]);

        var service = CreateService();
        var result = await service.PruneSmallAlbumsAsync(_root, minTracks: 6);

        result.AlbumsRemoved.Should().Be(0);
        Directory.Exists(albumDir).Should().BeTrue();
        Directory.EnumerateFiles(albumDir).Should().HaveCount(8);
    }
}
