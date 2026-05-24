using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoColorCorrectionWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IVideoEnhancementService
        _videoEnhancementService;

    private readonly ILogger<
            VideoColorCorrectionWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoColorCorrectionWorkflowStep);

    public VideoColorCorrectionWorkflowStep(
        IVideoEnhancementService videoEnhancementService,
        ILogger<VideoColorCorrectionWorkflowStep> logger)
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

        string filter =
            state.RestorationProfile switch
            {
                RestorationProfile.VHSAggressive
                    => "eq=contrast=1.3:saturation=1.4:brightness=0.03",

                _ => "eq=contrast=1.1:saturation=1.1"
            };

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
                        "Starting video color correction for {File}",
                        fileName);

                    var correctedPath =
                        await _videoEnhancementService
                            .ColorCorrectAsync(
                                item.CurrentWorkingPath!,
                                filter,
                                progressValue =>
                                {
                                    _logger.LogInformation(
                                        "Video color correction progress {File}: {Progress:P0}",
                                        fileName,
                                        progressValue);
                                },
                                cancellationToken);

                    if (string.IsNullOrWhiteSpace(
                            correctedPath))
                    {
                        throw new InvalidOperationException(
                            "Video color correction returned no output path.");
                    }

                    item.CurrentWorkingPath =
                        correctedPath;

                    _logger.LogInformation(
                        "Completed video color correction for {File}",
                        fileName);
                });
        }
    }
}