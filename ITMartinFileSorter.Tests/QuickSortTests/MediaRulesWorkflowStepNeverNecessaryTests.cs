using FluentAssertions;
using ITMartin.Media.Application.Pipelines.QuickSort.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinFileSorter.Tests.QuickSortTests;

// "Only the necessary files" (2026-09-08): Skærmbilleder/Memes/Gifs/Chat/
// Film/Ikke_identificeret are never exported, so removing them from
// state.MediaFiles right here - the earliest point their category is known -
// means every later step (hashing, dedup, normalization, quality, AI
// classification, export) never sees them at all, instead of each step
// needing its own filter.
[TestFixture]
public class MediaRulesWorkflowStepNeverNecessaryTests
{
    private string _root = "";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "QuickSortNeverNecessaryTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private async Task<QuickSortWorkflowState> ClassifyAsync(params string[] fileNames)
    {
        var files = fileNames.Select(name =>
        {
            var path = Path.Combine(_root, name);
            File.WriteAllBytes(path, [0x01, 0x02, 0x03]);
            return new MediaFile(path, DateTime.UtcNow, MediaType.Image, 3);
        }).ToList();

        var state = new QuickSortWorkflowState { MediaFiles = files };
        var step = new MediaRulesWorkflowStep(
            Mock.Of<IVideoMetadataService>(),
            Mock.Of<IConcurrentVideoDispatcher>(),
            NullLogger<MediaRulesWorkflowStep>.Instance);
        var context = new WorkflowExecutionContext<QuickSortWorkflowState> { WorkflowName = "QuickSortWorkflow", State = state };

        await step.ExecuteAsync(context);
        return state;
    }

    [Test]
    public async Task A_screenshot_is_removed_from_MediaFiles_entirely()
    {
        var state = await ClassifyAsync("Screenshot_2024-01-01.png");

        state.MediaFiles.Should().BeEmpty();
    }

    [Test]
    public async Task An_ordinary_photo_stays_in_MediaFiles()
    {
        var state = await ClassifyAsync("IMG_1234.jpg");

        state.MediaFiles.Should().ContainSingle();
    }

    [Test]
    public async Task Only_the_screenshot_is_removed_from_a_mixed_batch()
    {
        var state = await ClassifyAsync("Screenshot_2024-01-01.png", "IMG_1234.jpg");

        state.MediaFiles.Should().ContainSingle(f => f.FullPath.EndsWith("IMG_1234.jpg"));
    }
}
