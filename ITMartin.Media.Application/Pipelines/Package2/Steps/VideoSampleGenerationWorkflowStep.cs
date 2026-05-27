using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoSampleGenerationWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IVideoSampleService
        _videoSampleService;

    private readonly ILogger<
            VideoSampleGenerationWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoSampleGenerationWorkflowStep);

    public VideoSampleGenerationWorkflowStep(
        IVideoSampleService videoSampleService,
        ILogger<VideoSampleGenerationWorkflowStep> logger)
    {
        _videoSampleService =
            videoSampleService;

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

        if (!state.EnableSampleGeneration)
        {
            _logger.LogInformation(
                "Skipping sample generation");

            return;
        }

        var originalItems =
            state.Items
                .Where(x =>
                    !x.Failed &&
                    x.MediaKind == MediaKind.Video &&
                    x.CurrentWorkingPath is not null)
                .ToList();

        var generatedItems =
            new List<EnhancedMediaItem>();

        var total =
            originalItems.Count;

        var current = 0;

        foreach (var item in originalItems)
        {
            current++;

            _logger.LogInformation(
                "[{Step}] {Current}/{Total} {File}",
                Name,
                current,
                total,
                item.CurrentWorkingPath);

            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    var segments =
                        item.Segments
                        ?? [];

                    if (segments.Count == 0)
                    {
                        _logger.LogInformation(
                            "No segments found for sample generation");

                        return;
                    }

                    var selectedSegments =
                        segments
                            .OrderByDescending(x =>
                                x.End - x.Start)
                            .Take(
                                state.SampleCount)
                            .ToList();

                    var sampleIndex = 0;

                    foreach (var segment in selectedSegments)
                    {
                        cancellationToken
                            .ThrowIfCancellationRequested();

                        sampleIndex++;

                        var samplePath =
                            await _videoSampleService
                                .CreateSampleAsync(
                                    item.CurrentWorkingPath!,
                                    segment.Start,
                                    state.SampleDuration,
                                    cancellationToken);

                        var sampleItem =
                            new EnhancedMediaItem
                            {
                                OriginalPath =
                                    item.OriginalPath,

                                NormalizedPath =
                                    samplePath,

                                CurrentWorkingPath =
                                    samplePath,

                                MediaKind =
                                    MediaKind.Video,

                                IsSample =
                                    true,

                                SampleStart =
                                    segment.Start,

                                SampleDuration =
                                    state.SampleDuration,

                                VideoFilters =
                                    [],

                                AudioFilters =
                                    [],

                                Operations =
                                [
                                    new EnhancementOperation
                                    {
                                        Name =
                                            "SampleGenerated",

                                        Success =
                                            true,

                                        StartedAt =
                                            DateTimeOffset.UtcNow,

                                        CompletedAt =
                                            DateTimeOffset.UtcNow
                                    }
                                ]
                            };

                        generatedItems.Add(
                            sampleItem);

                        _logger.LogInformation(
                            """
                            Generated sample
                            Source: {Source}
                            Sample: {Sample}
                            Start: {Start}
                            Duration: {Duration}
                            """,
                            item.CurrentWorkingPath,
                            samplePath,
                            segment.Start,
                            state.SampleDuration);
                    }

                    item.SkipFurtherProcessing =
                        true;
                },
                _logger);
        }

        foreach (var item in generatedItems)
        {
            state.Items.Add(item);
        }
    }
}