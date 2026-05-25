using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
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

        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not Package2WorkflowState state)
        {
            return;
        }

        if (!state.VideoPipeline.HasFilters)
        {
            _logger.LogInformation(
                "Skipping video render because no filters were registered.");

            return;
        }
        
        var videoFilterChain =
            state.VideoPipeline.Build();

        var audioFilterChain =
            state.AudioPipeline.Build();
        
        _logger.LogInformation(
            "Built video filter chain: {FilterChain}",
            videoFilterChain);

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Video &&
                         x.CurrentWorkingPath is not null &&
                         !x.Operations.Any(o =>
                             o.Name == Name &&
                             o.Success)))
        {
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

                    _logger.LogInformation(
                        "Starting video render for {File}",
                        fileName);

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

                    _logger.LogInformation(
                        "Completed video render for {File}",
                        fileName);
                });
        }
    }
}