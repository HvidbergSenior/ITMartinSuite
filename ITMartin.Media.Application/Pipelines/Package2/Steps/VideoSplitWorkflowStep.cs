using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoSplitWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IVideoSegmentationService
        _videoSegmentationService;

    private readonly ILogger<
            VideoSplitWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoSplitWorkflowStep);

    public VideoSplitWorkflowStep(
        IVideoSegmentationService
            videoSegmentationService,
        ILogger<VideoSplitWorkflowStep> logger)
    {
        _videoSegmentationService =
            videoSegmentationService;

        _logger =
            logger;
    }

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not Package2WorkflowState state)
        {
            return;
        }

        if (state.ManualSegments.Count == 0)
        {
            _logger.LogWarning(
                "No manual segments configured");

            return;
        }

        var items =
            state.Items
                .Where(x =>
                    !x.Failed &&
                    x.MediaKind == MediaKind.Video &&
                    x.CurrentWorkingPath is not null)
                .ToList();

        var newItems =
            new List<EnhancedMediaItem>();

        foreach (var item in items)
        {
            var index = 0;

            foreach (var segment in state.ManualSegments)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                index++;

                var duration =
                    segment.End -
                    segment.Start;

                if (duration.TotalSeconds < 5)
                {
                    continue;
                }

                var extension =
                    Path.GetExtension(
                        item.CurrentWorkingPath);

                var fileName =
                    Path.GetFileNameWithoutExtension(
                        item.CurrentWorkingPath);

                var splitPath =
                    Path.Combine(
                        Path.GetDirectoryName(
                            item.CurrentWorkingPath)!,
                        $"{fileName}.segment_{index}{extension}");

                _logger.LogInformation(
                    "Generating split segment {Segment} for {File}",
                    index,
                    item.CurrentWorkingPath);

                await _videoSegmentationService
                    .GenerateSampleAsync(
                        item.CurrentWorkingPath,
                        splitPath,
                        segment.Start,
                        duration,
                        cancellationToken);

                newItems.Add(
                    new EnhancedMediaItem
                    {
                        OriginalPath =
                            item.OriginalPath,

                        NormalizedPath =
                            splitPath,

                        CurrentWorkingPath =
                            splitPath,

                        EnhancedOutputPath =
                            splitPath.Replace(
                                extension,
                                $".restored{extension}"),

                        ThumbnailOutputPath =
                            splitPath.Replace(
                                extension,
                                ".jpg"),

                        MediaKind =
                            MediaKind.Video,

                        IsSample =
                            false,

                        SkipFurtherProcessing =
                            false
                    });
            }

            item.SkipFurtherProcessing =
                true;
        }

        foreach (var newItem in newItems)
        {
            state.Items.Add(
                newItem);
        }
    }
}