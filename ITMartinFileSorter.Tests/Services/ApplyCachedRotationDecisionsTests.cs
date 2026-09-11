using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Infrastructure.Pipelines.FaceIndex;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ITMartinFileSorter.Tests.Services;

// rotation-decisions.json is keyed by the pre-rotation content hash, so a
// decision made on one run carries over to any later run over the same
// photos - but only if something actually replays it. On the ToshibaTest
// delivery 2026-09-11 all 264 photos reviewed on the test run were still
// sideways in the full library because no phase did. This pins the replay:
// it joins through the file status registry (no hashing), turns what it
// matches, and marks what it resolved so the free pass never re-checks it.
[TestFixture]
public class ApplyCachedRotationDecisionsTests
{
    private string _root = "";
    private Dictionary<string, FileStatusRecord> _registry = new();

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "ApplyCachedRotations_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _registry = new Dictionary<string, FileStatusRecord>(StringComparer.OrdinalIgnoreCase);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private LibraryPolishService CreateService()
    {
        var registry = new Mock<IFileStatusRegistryService>();
        registry.Setup(r => r.LoadAsync(_root, It.IsAny<CancellationToken>())).ReturnsAsync(() => _registry);

        return new LibraryPolishService(
            NullLogger<LibraryPolishService>.Instance,
            Mock.Of<IDbContextFactory<MediaDbContext>>(),
            Mock.Of<IVideoMetadataService>(),
            Mock.Of<IMediaDateService>(),
            Mock.Of<IExifService>(),
            Mock.Of<IPerceptualHashService>(),
            () => Mock.Of<IFaceRecognitionService>(),
            Mock.Of<IImageAnalysisService>(),
            Mock.Of<IDuplicateService>(),
            registry.Object,
            Mock.Of<IAudioConverterService>(),
            Mock.Of<IImageConverterService>(),
            Mock.Of<IConfiguration>());
    }

    // 40 wide, 20 high so a real rotation shows as swapped dimensions.
    private string WriteLandscape(string relativePath, byte shade = 0)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // shade keeps two fixtures from hashing identically
        using var image = new Image<Rgb24>(40, 20, new Rgb24(shade, shade, shade));
        image.Save(path);

        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        _registry[hash] = new FileStatusRecord { ContentHash = hash, RelativePath = relativePath };
        return hash;
    }

    private void WriteDecisions(Dictionary<string, int> decisions) =>
        File.WriteAllText(Path.Combine(_root, "rotation-decisions.json"), JsonSerializer.Serialize(decisions));

    private HashSet<string> CheckedPaths()
    {
        var file = Path.Combine(_root, "rotation-checked.json");
        if (!File.Exists(file)) return [];
        return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(file)) ?? [];
    }

    [Test]
    public async Task A_cached_decision_rotates_the_matching_file_and_marks_it_checked()
    {
        var sideways = WriteLandscape(Path.Combine("Billeder", "2019", "sideways.jpg"));
        var fine = WriteLandscape(Path.Combine("Billeder", "2019", "fine.jpg"), shade: 200);
        WriteDecisions(new() { [sideways] = 90, [fine] = 0 });

        var result = await CreateService().ApplyCachedRotationDecisionsAsync(_root);

        result.Matched.Should().Be(1);
        result.Rotated.Should().Be(1);
        result.Failed.Should().BeEmpty();

        using var rotated = Image.Load(Path.Combine(_root, "Billeder", "2019", "sideways.jpg"));
        rotated.Width.Should().Be(20, "a 90-degree decision must physically turn the pixels");

        using var untouched = Image.Load(Path.Combine(_root, "Billeder", "2019", "fine.jpg"));
        untouched.Width.Should().Be(40);

        CheckedPaths().Should().BeEquivalentTo(
        [
            Path.Combine("Billeder", "2019", "sideways.jpg"),
            Path.Combine("Billeder", "2019", "fine.jpg"),
        ], "both a rotation and an explicit 'this is fine' are resolved and must not be re-checked");
    }

    // The registry still maps the pre-rotation hash to the path until the
    // next IndexConverge, so without a guard a second run turns the photo
    // again. On ToshibaTest 2026-09-11 that happened to 260 photos.
    [Test]
    public async Task Running_twice_before_the_registry_is_rebuilt_rotates_only_once()
    {
        var sideways = WriteLandscape(Path.Combine("Billeder", "2019", "sideways.jpg"));
        WriteDecisions(new() { [sideways] = 90 });
        var service = CreateService();

        await service.ApplyCachedRotationDecisionsAsync(_root);
        var second = await service.ApplyCachedRotationDecisionsAsync(_root);

        second.Rotated.Should().Be(0);
        using var image = Image.Load(Path.Combine(_root, "Billeder", "2019", "sideways.jpg"));
        image.Width.Should().Be(20, "a 90-degree decision applied twice would be 180 and the photo would be upside down instead of upright");
    }

    // A thumbnail made from the sideways pixels must not survive the
    // rotation - the gallery grid is built from thumbnails, so a stale one
    // shows the photo still sideways after it was fixed.
    [Test]
    public async Task Rotating_a_photo_removes_its_stale_thumbnail()
    {
        var sideways = WriteLandscape(Path.Combine("Billeder", "2019", "sideways.jpg"));
        var thumbDir = Path.Combine(_root, "Billeder", "2019", "thumbnails");
        Directory.CreateDirectory(thumbDir);
        var thumb = Path.Combine(thumbDir, "sideways.jpg");
        File.WriteAllBytes(thumb, [9]);
        WriteDecisions(new() { [sideways] = 90 });

        await CreateService().ApplyCachedRotationDecisionsAsync(_root);

        File.Exists(thumb).Should().BeFalse("the thumbnail was made from the unrotated pixels");
    }

    [Test]
    public async Task Decisions_for_hashes_not_in_the_library_are_ignored()
    {
        WriteLandscape(Path.Combine("Billeder", "photo.jpg"));
        WriteDecisions(new() { ["ABCDEF0123456789"] = 180 });

        var result = await CreateService().ApplyCachedRotationDecisionsAsync(_root);

        result.Matched.Should().Be(0);
        result.Rotated.Should().Be(0);
    }

    [Test]
    public async Task Without_a_registry_nothing_is_hashed_and_nothing_changes()
    {
        var path = Path.Combine(_root, "Billeder", "photo.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var image = new Image<Rgb24>(40, 20)) image.Save(path);
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        WriteDecisions(new() { [hash] = 90 });
        // _registry deliberately left empty: first run, IndexConverge has not built it yet

        var result = await CreateService().ApplyCachedRotationDecisionsAsync(_root);

        result.Rotated.Should().Be(0);
        using var untouched = Image.Load(path);
        untouched.Width.Should().Be(40, "with no registry the decision must wait for the next round, not trigger a full hash of the library");
    }

    [Test]
    public async Task No_decisions_file_is_a_quiet_no_op()
    {
        WriteLandscape(Path.Combine("Billeder", "photo.jpg"));

        var result = await CreateService().ApplyCachedRotationDecisionsAsync(_root);

        result.Matched.Should().Be(0);
        File.Exists(Path.Combine(_root, "rotation-checked.json")).Should().BeFalse();
    }
}
