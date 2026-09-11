using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Infrastructure.Pipelines.FaceIndex;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinFileSorter.Tests.Services;

// The index around the detector: every image looked at exactly once, the
// "nothing here" photos remembered too, and a label turned into a folder of
// links. The detector itself is faked - its real behaviour is pinned in
// YoloObjectDetectionServiceTests.
[TestFixture]
public class ObjectIndexServiceTests
{
    private string _root = "";
    private int _detectCalls;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "ObjectIndex_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "Billeder", "2015"));
        Directory.CreateDirectory(Path.Combine(_root, "Billeder", "2015", "thumbnails"));
        Directory.CreateDirectory(Path.Combine(_root, "Dubletter", "Billeder"));
        _detectCalls = 0;
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    // File name decides what the fake detector "sees".
    private sealed class FakeDetector(Action onCall) : IObjectDetectionService
    {
        public Task<IReadOnlyList<DetectedObject>> DetectAsync(string filePath, double minConfidence = 0.35, CancellationToken cancellationToken = default)
        {
            onCall();
            var name = Path.GetFileName(filePath);
            IReadOnlyList<DetectedObject> r = name.Contains("svag") ? [new("dog", 0.32)]
                : name.Contains("hund") ? [new("dog", 0.9), new("person", 0.8)]
                : [];
            return Task.FromResult(r);
        }
    }

    private ObjectIndexService Create()
    {
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ObjectIndexService(new TestDbContextFactory(options), () => new FakeDetector(() => Interlocked.Increment(ref _detectCalls)), NullLogger<ObjectIndexService>.Instance);
    }

    private void Photo(params string[] parts)
    {
        var p = Path.Combine([_root, .. parts]);
        File.WriteAllBytes(p, [1, 2, 3]);
    }

    [Test]
    public async Task Scans_each_image_once_and_skips_generated_and_duplicate_folders()
    {
        Photo("Billeder", "2015", "hund-1.jpg");
        Photo("Billeder", "2015", "strand.jpg");
        Photo("Billeder", "2015", "thumbnails", "hund-1.jpg");
        Photo("Dubletter", "Billeder", "hund-2.jpg");
        var svc = Create();

        (await svc.IndexAsync(_root)).Should().Be(2, "thumbnails and Dubletter are never scanned");
        _detectCalls.Should().Be(2);

        (await svc.IndexAsync(_root)).Should().Be(0, "a second run must not look at anything again");
        _detectCalls.Should().Be(2);

        var (scanned, withDog) = await svc.StatusAsync(_root, "dog");
        scanned.Should().Be(2, "the empty photo counts as scanned");
        withDog.Should().Be(1);
    }

    [Test]
    public async Task Folder_holds_the_confident_matches_as_links()
    {
        Photo("Billeder", "2015", "hund-1.jpg");
        Photo("Billeder", "2015", "svag-hund.jpg");
        Photo("Billeder", "2015", "strand.jpg");
        var svc = Create();
        await svc.IndexAsync(_root);

        var linked = await svc.GenerateFolderAsync(_root, "dog", "Fie", minConfidence: 0.4);

        linked.Should().Be(1, "0.32 is below the folder threshold");
        var folder = Path.Combine(_root, "SmartFolders", "People", "Fie");
        Directory.GetFiles(folder).Select(Path.GetFileName).Should().BeEquivalentTo(["hund-1.jpg"]);
    }

    private sealed class TestDbContextFactory(DbContextOptions<MediaDbContext> options)
        : IDbContextFactory<MediaDbContext>
    {
        public MediaDbContext CreateDbContext() => new(options);
    }
}
