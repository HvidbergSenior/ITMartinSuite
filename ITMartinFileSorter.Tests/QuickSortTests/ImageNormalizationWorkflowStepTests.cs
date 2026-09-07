using FluentAssertions;
using ITMartin.Media.Application.Pipelines.QuickSort.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinFileSorter.Tests.QuickSortTests;

// Covers the pipeline reorder from 2026-09-07 ("get all not relevant files
// off as fast as possible... so that this does not happen" - live on a real
// 84,535-file/77,061-image run, ImageNormalization was normalizing exact
// duplicates that get thrown away two steps later anyway).
// CleanupEvaluationWorkflowStep now runs before this step, so a file it's
// already marked Duplicates/DeleteCandidates must never reach
// IImageConverterService at all.
[TestFixture]
public class ImageNormalizationWorkflowStepTests
{
    private static MediaFile Image(string name, string? exportSubFolder = null) =>
        new($"C:\\lib\\{name}", DateTime.UtcNow, MediaType.Image, 1, isDateReliable: true)
        {
            ExportSubFolder = exportSubFolder,
        };

    private static async Task<(QuickSortWorkflowState State, Mock<IImageConverterService> Converter)> RunAsync(params MediaFile[] files)
    {
        var converter = new Mock<IImageConverterService>();
        converter.Setup(c => c.ConvertToJpgAsync(It.IsAny<string>())).ReturnsAsync((string p) => p);

        var step = new ImageNormalizationWorkflowStep(converter.Object, NullLogger<ImageNormalizationWorkflowStep>.Instance);

        var state = new QuickSortWorkflowState { RootPath = "C:\\lib", MediaFiles = files.ToList() };
        var context = new WorkflowExecutionContext<QuickSortWorkflowState>
        {
            WorkflowName = "QuickSortWorkflow",
            State = state,
        };

        await step.ExecuteAsync(context);
        return (state, converter);
    }

    [Test]
    public async Task Skips_a_file_already_marked_Duplicates_by_CleanupEvaluation()
    {
        var duplicate = Image("dup.jpg", exportSubFolder: "Duplicates");

        var (_, converter) = await RunAsync(duplicate);

        converter.Verify(c => c.ConvertToJpgAsync(It.IsAny<string>()), Times.Never);
        converter.Verify(c => c.TryGetSourceOrientation(It.IsAny<string>(), out It.Ref<ushort>.IsAny), Times.Never);
    }

    [Test]
    public async Task Skips_a_file_already_marked_DeleteCandidates_by_CleanupEvaluation()
    {
        var tiny = Image("tiny.jpg", exportSubFolder: "DeleteCandidates");

        var (_, converter) = await RunAsync(tiny);

        converter.Verify(c => c.ConvertToJpgAsync(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Still_normalizes_a_real_kept_image()
    {
        var real = Image("real.jpg");

        var (_, converter) = await RunAsync(real);

        converter.Verify(c => c.ConvertToJpgAsync(real.FullPath), Times.Once);
        real.IsNormalized.Should().BeTrue();
    }

    [Test]
    public async Task A_mix_only_processes_the_ones_not_marked_for_removal()
    {
        var keep1 = Image("keep1.jpg");
        var dup = Image("dup.jpg", exportSubFolder: "Duplicates");
        var keep2 = Image("keep2.jpg");
        var tiny = Image("tiny.jpg", exportSubFolder: "DeleteCandidates");

        var (_, converter) = await RunAsync(keep1, dup, keep2, tiny);

        converter.Verify(c => c.ConvertToJpgAsync(It.IsAny<string>()), Times.Exactly(2));
        converter.Verify(c => c.ConvertToJpgAsync(keep1.FullPath), Times.Once);
        converter.Verify(c => c.ConvertToJpgAsync(keep2.FullPath), Times.Once);
    }
}
