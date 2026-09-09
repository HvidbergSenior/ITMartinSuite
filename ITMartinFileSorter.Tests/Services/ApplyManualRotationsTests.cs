using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Infrastructure.Pipelines.FaceIndex;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace ITMartinFileSorter.Tests.Services;

// The other half of the manual-rotation loop: FileStatusWorkflowStep stages
// unresolvable photos into SmartFolders/RoterManuelt, a person rotates what
// is wrong, and this applies those rotations back onto the library.
//
// The trap being pinned here cost a full round-trip to find on ToshibaTest
// 2026-09-10: Windows Photos' rotate button usually only flips the EXIF
// Orientation tag and leaves the pixel data untouched. The photo then looks
// correct in Explorer and Photos while every raw-pixel reader - the gallery
// thumbnailer included - still sees it sideways. Copying such a file back
// unchanged looks like it worked and silently does not, which is why the tag
// must be baked into the pixels before anything is written back.
[TestFixture]
public class ApplyManualRotationsTests
{
    private string _root = "";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "ApplyManualRotations_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private static LibraryPolishService CreateService() =>
        new(
            NullLogger<LibraryPolishService>.Instance,
            Mock.Of<IDbContextFactory<MediaDbContext>>(),
            Mock.Of<IVideoMetadataService>(),
            Mock.Of<IMediaDateService>(),
            Mock.Of<IExifService>(),
            Mock.Of<IPerceptualHashService>(),
            () => Mock.Of<IFaceRecognitionService>(),
            Mock.Of<IImageAnalysisService>(),
            Mock.Of<IDuplicateService>(),
            Mock.Of<IFileStatusRegistryService>(),
            Mock.Of<IAudioConverterService>(),
            Mock.Of<IImageConverterService>(),
            Mock.Of<IConfiguration>());

    // 40 wide, 20 high - deliberately non-square so a real rotation is
    // visible as swapped dimensions.
    private static void WriteLandscape(string path, ushort? orientation = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = new Image<Rgb24>(40, 20);
        if (orientation is not null)
        {
            image.Metadata.ExifProfile ??= new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, orientation.Value);
        }
        image.Save(path);
    }

    private string StageOne(string originalPath, Action<string> mutateCopy)
    {
        var folder = Path.Combine(_root, "SmartFolders", "RoterManuelt");
        Directory.CreateDirectory(folder);

        var copyName = "00001__photo.jpg";
        var copy = Path.Combine(folder, copyName);
        File.Copy(originalPath, copy, overwrite: true);

        mutateCopy(copy);

        File.WriteAllLines(Path.Combine(folder, "RoterManuelt.csv"),
        [
            "CopyName,Original",
            $"\"{copyName}\",\"{originalPath}\"",
        ]);

        return copy;
    }

    [Test]
    public async Task An_untouched_copy_is_not_written_back()
    {
        var original = Path.Combine(_root, "Billeder", "2007", "photo.jpg");
        WriteLandscape(original);
        StageOne(original, _ => { });

        var result = await CreateService().ApplyManualRotationsAsync(_root);

        result.Staged.Should().Be(1);
        result.Rotated.Should().Be(0);
        result.AppliedToLibrary.Should().Be(0);

        using var untouched = Image.Load(original);
        untouched.Width.Should().Be(40, "an unrotated photo must be left exactly as it was");
    }

    // The important one: the copy carries only an EXIF tag saying "rotate
    // me", with landscape pixels still on disk. What lands in the library
    // must be genuinely portrait, not merely tagged.
    [Test]
    public async Task An_exif_only_rotation_is_baked_into_the_pixels_before_being_applied()
    {
        var original = Path.Combine(_root, "Billeder", "2007", "photo.jpg");
        WriteLandscape(original);

        StageOne(original, copy =>
        {
            using var image = Image.Load(copy);
            image.Metadata.ExifProfile ??= new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6); // rotate 90
            image.Save(copy);
        });

        var result = await CreateService().ApplyManualRotationsAsync(_root);

        result.Rotated.Should().Be(1);
        result.AppliedToLibrary.Should().Be(1);

        using var applied = Image.Load(original);
        applied.Width.Should().Be(20, "the pixels themselves must be rotated, not just the EXIF tag");
        applied.Height.Should().Be(40);
        applied.Metadata.ExifProfile?.TryGetValue(ExifTag.Orientation, out _)
            .Should().NotBe(true, "the tag must be cleared so nothing rotates it a second time");
    }

    [Test]
    public async Task Missing_manifest_is_a_no_op()
    {
        var result = await CreateService().ApplyManualRotationsAsync(_root);
        result.Staged.Should().Be(0);
        result.AppliedToLibrary.Should().Be(0);
    }
}
