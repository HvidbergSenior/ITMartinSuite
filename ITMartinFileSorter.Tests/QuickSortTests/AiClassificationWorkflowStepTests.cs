using System.Reflection;
using FluentAssertions;
using ITMartin.Media.Application.Pipelines.QuickSort.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinFileSorter.Tests.QuickSortTests;

// Covers the bug found and fixed 2026-08-24: this step used to be an
// uncapped, unbatched per-file Claude-call loop, which is exactly what
// CLAUDE.md's "AI/Claude API cost discipline" rule exists to prevent (a real
// customer library can be tens of thousands of files). It's now capped and
// concurrent, matching the convention LibraryPolishService already uses for
// its own per-file AI passes (ReclassifyScreenshotsAsync etc.).
[TestFixture]
public class AiClassificationWorkflowStepTests
{
    private string _root = "";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "QuickSortAiClassificationTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private static int GetMaxChecksPerRun()
    {
        var field = typeof(AiClassificationWorkflowStep)
            .GetField("MaxAiClassificationChecksPerRun", BindingFlags.NonPublic | BindingFlags.Static);
        return (int)field!.GetRawConstantValue()!;
    }

    [Test]
    public async Task Never_calls_the_ai_service_more_times_than_the_hard_cap_even_with_more_images_available()
    {
        var cap = GetMaxChecksPerRun();
        var extra = 25;

        var files = new List<MediaFile>();
        for (var i = 0; i < cap + extra; i++)
        {
            var path = Path.Combine(_root, $"img_{i}.jpg");
            File.WriteAllBytes(path, [0x01]);
            files.Add(new MediaFile(path, DateTime.UtcNow, MediaType.Image, 1, isDateReliable: true));
        }

        var callCount = 0;
        var analysis = new Mock<IImageAnalysisService>();
        analysis
            .Setup(a => a.AnalyzeImageAsync(It.IsAny<string>()))
            .ReturnsAsync(new AiAnalysisResult())
            .Callback(() => Interlocked.Increment(ref callCount));

        var state = new QuickSortWorkflowState
        {
            OutputPath = _root,
            MediaFiles = files,
            EnableAiClassification = true,
        };

        var step = new AiClassificationWorkflowStep(analysis.Object, NullLogger<AiClassificationWorkflowStep>.Instance);
        var context = new WorkflowExecutionContext<QuickSortWorkflowState>
        {
            WorkflowName = "QuickSortWorkflow",
            State = state,
        };

        await step.ExecuteAsync(context);

        callCount.Should().Be(cap, "the step must never exceed its hard per-run cap regardless of library size");
    }

    [Test]
    public async Task Does_nothing_when_ai_classification_is_disabled()
    {
        var path = Path.Combine(_root, "img.jpg");
        File.WriteAllBytes(path, [0x01]);
        var file = new MediaFile(path, DateTime.UtcNow, MediaType.Image, 1, isDateReliable: true);

        var analysis = new Mock<IImageAnalysisService>();

        var state = new QuickSortWorkflowState
        {
            OutputPath = _root,
            MediaFiles = [file],
            EnableAiClassification = false,
        };

        var step = new AiClassificationWorkflowStep(analysis.Object, NullLogger<AiClassificationWorkflowStep>.Instance);
        var context = new WorkflowExecutionContext<QuickSortWorkflowState>
        {
            WorkflowName = "QuickSortWorkflow",
            State = state,
        };

        await step.ExecuteAsync(context);

        analysis.Verify(a => a.AnalyzeImageAsync(It.IsAny<string>()), Times.Never);
    }
}
