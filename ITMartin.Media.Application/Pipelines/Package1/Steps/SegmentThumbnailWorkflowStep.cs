using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class SegmentThumbnailWorkflowStep
    : Package1WorkflowStepBase
{
    private readonly IVideoSegmentThumbnailService
        _thumbnailService;

    private readonly ILogger<
            SegmentThumbnailWorkflowStep>
        _logger;

    public SegmentThumbnailWorkflowStep(
        IVideoSegmentThumbnailService
            thumbnailService,
        ILogger<
                SegmentThumbnailWorkflowStep>
            logger)
    {
        _thumbnailService =
            thumbnailService;

        _logger =
            logger;
    }

    public override string Name =>
        "SegmentThumbnails";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        foreach (var file in state.MediaFiles
                     .Where(x =>
                         x.IsVideo &&
                         x.Segments.Count > 0))
        {
            var total =
                file.Segments.Count;

            var current = 0;

            foreach (var segment in file.Segments)
            {
                current++;

                LogStepProgress(
                    _logger,
                    Name,
                    current,
                    total,
                    file.FileName);

                var middle =
                    segment.Start +
                    TimeSpan.FromSeconds(
                        segment.DurationSeconds / 2);

                await ExecuteOperationAsync(
                    "GenerateSegmentThumbnail",
                    file.FileName,
                    async () =>
                    {
                        segment.ThumbnailPath =
                            await _thumbnailService
                                .GenerateThumbnailAsync(
                                    file.FullPath,
                                    middle,
                                    cancellationToken);
                    },
                    _logger);
            }
        }
    }
}