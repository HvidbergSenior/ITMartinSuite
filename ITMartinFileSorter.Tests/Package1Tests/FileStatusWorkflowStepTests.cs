using FluentAssertions;
using ITMartin.Media.Application.Pipelines.Package1.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinFileSorter.Tests.Package1Tests;

// Covers the two bugs found and fixed 2026-08-24: RotationIsCorrect used to
// be hardcoded false for every image regardless of what Package1 actually
// knew, and QualityChecked used to just mirror EnableAiClassification
// instead of reflecting a real per-file result. See feedback in the
// conversation this was built from - Package1 should get files to a
// finished (IsDone) state as fast as possible on the first run, not defer
// everything to a later Package3 catch-up pass.
[TestFixture]
public class FileStatusWorkflowStepTests
{
    private string _root = "";
    private Mock<IFileStatusRegistryService> _registry = null!;
    private Dictionary<string, FileStatusRecord> _savedRegistry = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "Package1StatusTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        _registry = new Mock<IFileStatusRegistryService>();
        _registry
            .Setup(r => r.LoadAsync(_root, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, FileStatusRecord>());
        _registry
            .Setup(r => r.SaveAsync(_root, It.IsAny<Dictionary<string, FileStatusRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Dictionary<string, FileStatusRecord>, CancellationToken>((_, dict, _) => _savedRegistry = dict)
            .Returns(Task.CompletedTask);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private static MediaFile MakeImageFile(string path, string hash)
    {
        File.WriteAllBytes(path, [0x01, 0x02, 0x03]);
        var file = new MediaFile(path, DateTime.UtcNow, MediaType.Image, 3, isDateReliable: true);
        file.SetHash(hash);
        return file;
    }

    private async Task<FileStatusRecord> RunAndGetRecordAsync(MediaFile file, bool enableAiClassification = false)
    {
        var state = new Package1WorkflowState
        {
            OutputPath = _root,
            MediaFiles = [file],
            EnableAiClassification = enableAiClassification,
        };

        var step = new FileStatusWorkflowStep(_registry.Object, Mock.Of<ILibraryPathProvider>(), NullLogger<FileStatusWorkflowStep>.Instance);

        var context = new WorkflowExecutionContext<Package1WorkflowState>
        {
            WorkflowName = "Package1Workflow",
            State = state,
        };

        await step.ExecuteAsync(context);

        _savedRegistry.Should().ContainKey(file.Hash!);
        return _savedRegistry[file.Hash!];
    }

    [Test]
    public async Task RotationIsCorrect_is_true_when_source_had_a_usable_exif_orientation_tag()
    {
        var file = MakeImageFile(Path.Combine(_root, "a.jpg"), "hash-known-orientation");
        file.OrientationKnownFromExif = true;

        var record = await RunAndGetRecordAsync(file);

        record.Flags[StepFlags.RotationIsCorrect].Value.Should().BeTrue();
    }

    [Test]
    public async Task RotationIsCorrect_is_false_with_a_suggestion_when_no_exif_orientation_tag_existed()
    {
        var file = MakeImageFile(Path.Combine(_root, "b.jpg"), "hash-unknown-orientation");
        file.OrientationKnownFromExif = false;

        var record = await RunAndGetRecordAsync(file);

        record.Flags[StepFlags.RotationIsCorrect].Value.Should().BeFalse();
        record.Flags[StepFlags.RotationIsCorrect].Suggestion.Should().Contain("No EXIF orientation tag found");
    }

    [Test]
    public async Task QualityChecked_is_true_when_the_free_quality_check_found_no_problem()
    {
        var file = MakeImageFile(Path.Combine(_root, "c.jpg"), "hash-good-quality");
        file.OrientationKnownFromExif = true;
        file.IsBlurry = false;
        file.IsSolidColor = false;

        var record = await RunAndGetRecordAsync(file);

        record.Flags[StepFlags.QualityChecked].Value.Should().BeTrue();
    }

    [Test]
    public async Task QualityChecked_is_false_with_a_blurry_suggestion_when_the_free_check_flagged_blur()
    {
        var file = MakeImageFile(Path.Combine(_root, "d.jpg"), "hash-blurry");
        file.OrientationKnownFromExif = true;
        file.IsBlurry = true;
        file.IsSolidColor = false;

        var record = await RunAndGetRecordAsync(file);

        record.Flags[StepFlags.QualityChecked].Value.Should().BeFalse();
        record.Flags[StepFlags.QualityChecked].Suggestion.Should().Contain("blurry");
    }

    [Test]
    public async Task QualityChecked_is_unresolved_when_the_image_was_never_analyzed()
    {
        // Neither the free ImageQualityWorkflowStep nor the paid
        // AiClassificationWorkflowStep ran (e.g. the file couldn't be
        // decoded) - IsBlurry/IsSolidColor stay null. Must NOT be silently
        // treated as "confirmed good".
        var file = MakeImageFile(Path.Combine(_root, "e.jpg"), "hash-unanalyzed");
        file.OrientationKnownFromExif = true;

        var record = await RunAndGetRecordAsync(file);

        record.Flags[StepFlags.QualityChecked].Value.Should().BeFalse();
        record.Flags[StepFlags.QualityChecked].Suggestion.Should().Contain("Could not be analyzed");
    }

    [Test]
    public async Task A_real_photo_with_known_orientation_and_confirmed_quality_reaches_IsDone_from_Package1_alone()
    {
        // The actual point of both fixes: a normal, unremarkable photo
        // (has EXIF orientation, isn't blurry/solid) should never need a
        // later Package3 catch-up pass at all.
        var file = MakeImageFile(Path.Combine(_root, "f.jpg"), "hash-fully-done");
        file.OrientationKnownFromExif = true;
        file.IsBlurry = false;
        file.IsSolidColor = false;
        file.IsNormalized = true;
        file.SubCategory = MediaSubCategory.PhonePhoto; // away from the default UnknownImage

        var record = await RunAndGetRecordAsync(file);

        record.IsDone.Should().BeTrue();
    }
}
