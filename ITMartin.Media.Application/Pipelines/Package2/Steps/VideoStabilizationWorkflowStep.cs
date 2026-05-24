using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoStabilizationWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IVideoEnhancementService
        _videoEnhancementService;

    private readonly ILogger<
            VideoStabilizationWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoStabilizationWorkflowStep);

    public VideoStabilizationWorkflowStep(
        IVideoEnhancementService videoEnhancementService,
        ILogger<VideoStabilizationWorkflowStep> logger)
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

        // VHS usually does not need stabilization
        if (state.RestorationProfile !=
            RestorationProfile.HandheldCamera)
        {
            _logger.LogInformation(
                "Skipping video stabilization because restoration profile is {Profile}",
                state.RestorationProfile);

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
                        "Starting video stabilization for {File}",
                        fileName);

                    var stabilizedPath =
                        await _videoEnhancementService
                            .StabilizeAsync(
                                item.CurrentWorkingPath!,
                                progressValue =>
                                {
                                    _logger.LogInformation(
                                        "Video stabilization progress {File}: {Progress:P0}",
                                        fileName,
                                        progressValue);
                                },
                                cancellationToken);

                    if (string.IsNullOrWhiteSpace(
                            stabilizedPath))
                    {
                        throw new InvalidOperationException(
                            "Video stabilization returned no output path.");
                    }

                    item.CurrentWorkingPath =
                        stabilizedPath;

                    _logger.LogInformation(
                        "Completed video stabilization for {File}",
                        fileName);
                });
        }
    }
}