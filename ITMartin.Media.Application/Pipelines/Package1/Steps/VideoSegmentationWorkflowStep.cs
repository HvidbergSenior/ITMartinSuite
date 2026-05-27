using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class VideoSegmentationWorkflowStep
    : Package1WorkflowStepBase
{
    private readonly IVideoSegmentationService
        _videoSegmentationService;

    private readonly ILogger<
            VideoSegmentationWorkflowStep>
        _logger;

    public VideoSegmentationWorkflowStep(
        IVideoSegmentationService
            videoSegmentationService,
        ILogger<
                VideoSegmentationWorkflowStep>
            logger)
    {
        _videoSegmentationService =
            videoSegmentationService;

        _logger =
            logger;
    }

    public override string Name =>
        "VideoSegmentation";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        var files =
            state.MediaFiles
                .Where(x => x.IsVideo)
                .ToList();

        var total =
            files.Count;

        var current = 0;

        foreach (var file in files)
        {
            current++;

            LogStepProgress(
                _logger,
                Name,
                current,
                total,
                file.FileName);

            await ExecuteOperationAsync(
                "SegmentVideo",
                file.FileName,
                async () =>
                {
                    file.Segments =
                        await _videoSegmentationService
                            .DetectSegmentsAsync(
                                file.FullPath,
                                cancellationToken);

                    _logger.LogInformation(
                        "Detected {Count} segments",
                        file.Segments.Count);
                },
                _logger);
        }
    }
}