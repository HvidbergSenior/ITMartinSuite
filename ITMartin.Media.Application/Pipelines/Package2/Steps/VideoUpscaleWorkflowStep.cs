using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoUpscaleWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IVideoEnhancementService
        _videoEnhancementService;

    private readonly ILogger<
            VideoUpscaleWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoUpscaleWorkflowStep);

    public VideoUpscaleWorkflowStep(
        IVideoEnhancementService videoEnhancementService,
        ILogger<VideoUpscaleWorkflowStep> logger)
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

        if (state.RestorationProfile ==
            RestorationProfile.VHSAggressive)
        {
            _logger.LogInformation(
                "Skipping video upscale because restoration profile is {Profile}",
                state.RestorationProfile);

            return;
        }

        if (!state.EnableUpscaling)
        {
            _logger.LogInformation(
                "Skipping video upscale because upscaling is disabled");

            return;
        }

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
                    var fileName =
                        Path.GetFileName(
                            item.CurrentWorkingPath);

                    _logger.LogInformation(
                        "Starting video upscale for {File}",
                        fileName);

                    using var cts =
                        CancellationTokenSource
                            .CreateLinkedTokenSource(
                                cancellationToken);

                    cts.CancelAfter(
                        TimeSpan.FromMinutes(30));

                    var upscaledPath =
                        await _videoEnhancementService
                            .UpscaleAsync(
                                item.CurrentWorkingPath!,
                                progressValue =>
                                {
                                    _logger.LogInformation(
                                        "Video upscale progress {File}: {Progress:P0}",
                                        fileName,
                                        progressValue);
                                },
                                cts.Token);

                    if (string.IsNullOrWhiteSpace(
                            upscaledPath))
                    {
                        throw new InvalidOperationException(
                            "Video upscale returned no output path.");
                    }

                    item.CurrentWorkingPath =
                        upscaledPath;

                    _logger.LogInformation(
                        "Completed video upscale for {File}",
                        fileName);
                });
        }
    }
}