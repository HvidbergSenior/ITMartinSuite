using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoCropWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IVideoEnhancementService
        _videoEnhancementService;

    private readonly ILogger<
            VideoCropWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoCropWorkflowStep);

    public VideoCropWorkflowStep(
        IVideoEnhancementService videoEnhancementService,
        ILogger<VideoCropWorkflowStep> logger)
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

        var cropAmount =
            state.RestorationProfile switch
            {
                RestorationProfile.VHSAggressive => 80,
                RestorationProfile.FamilyArchive => 48,
                _ => 32
            };

        var filter =
            $"crop=in_w:in_h-{cropAmount}:0:0";

        _logger.LogInformation(
            "Video crop step running with crop amount {CropAmount}px",
            cropAmount);

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
                        "Starting video crop for {File}",
                        fileName);

                    var croppedPath =
                        await _videoEnhancementService
                            .CropAsync(
                                item.CurrentWorkingPath!,
                                filter,
                                progressValue =>
                                {
                                    _logger.LogInformation(
                                        "Video crop progress {File}: {Progress:P0}",
                                        fileName,
                                        progressValue);
                                },
                                cancellationToken);

                    if (string.IsNullOrWhiteSpace(
                            croppedPath))
                    {
                        throw new InvalidOperationException(
                            "Video crop returned no output path.");
                    }

                    item.CurrentWorkingPath =
                        croppedPath;

                    _logger.LogInformation(
                        "Completed video crop for {File}",
                        fileName);
                });
        }
    }
}