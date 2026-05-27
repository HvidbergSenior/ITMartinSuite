using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoRenderWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IVideoEnhancementService
        _videoEnhancementService;

    private readonly ILogger<
            VideoRenderWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoRenderWorkflowStep);

    public VideoRenderWorkflowStep(
        IVideoEnhancementService videoEnhancementService,
        ILogger<VideoRenderWorkflowStep> logger)
    {
        _videoEnhancementService =
            videoEnhancementService;

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

        var items =
            state.Items
                .Where(x =>
                    !x.Failed &&
                    x.MediaKind == MediaKind.Video &&
                    x.CurrentWorkingPath is not null &&
                    x.VideoFilters.Count > 0 &&
                    !AlreadyExecuted(x, Name))
                .ToList();

        if (items.Count == 0)
        {
            _logger.LogInformation(
                "Skipping video render because no items contain video filters.");

            return;
        }

        var total =
            items.Count;

        var current = 0;

        foreach (var item in items)
        {
            current++;

            var videoFilterChain =
                string.Join(
                    ",",
                    item.VideoFilters);

            var audioFilterChain =
                string.Join(
                    ",",
                    item.AudioFilters);

            _logger.LogInformation(
                """
                Built filter chains
                Video: {Video}
                Audio: {Audio}
                """,
                videoFilterChain,
                audioFilterChain);

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
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    var fileName =
                        Path.GetFileName(
                            item.CurrentWorkingPath);

                    var outputPath =
                        await _videoEnhancementService
                            .ApplyFiltersAsync(
                                item.CurrentWorkingPath!,
                                videoFilterChain,
                                audioFilterChain,
                                progressValue =>
                                {
                                    _logger.LogInformation(
                                        "Video render progress {File}: {Progress:P0}",
                                        fileName,
                                        progressValue);
                                },
                                cancellationToken);

                    cancellationToken
                        .ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(
                            outputPath))
                    {
                        throw new InvalidOperationException(
                            "Video render returned no output path.");
                    }

                    item.CurrentWorkingPath =
                        outputPath;
                },
                _logger);
        }
    }
}