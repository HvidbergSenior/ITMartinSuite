using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoDeinterlaceWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IVideoEnhancementService
        _videoEnhancementService;

    private readonly ILogger<VideoDeinterlaceWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoDeinterlaceWorkflowStep);

    public VideoDeinterlaceWorkflowStep(
        IVideoEnhancementService videoEnhancementService,
        ILogger<VideoDeinterlaceWorkflowStep> logger)
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
                    => "bwdif",

                _ => "yadif=0"
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
            var fileName =
                Path.GetFileName(
                    item.CurrentWorkingPath);

            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    _logger.LogInformation(
                        "Starting deinterlace for {File}",
                        fileName);

                    var deinterlacedPath =
                        await _videoEnhancementService
                            .DeinterlaceAsync(
                                item.CurrentWorkingPath!,
                                filter,
                                progressValue =>
                                {
                                    cancellationToken
                                        .ThrowIfCancellationRequested();

                                    _logger.LogInformation(
                                        "Deinterlace progress {File}: {Progress:P0}",
                                        fileName,
                                        progressValue);
                                },
                                cancellationToken);

                    cancellationToken
                        .ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(
                            deinterlacedPath))
                    {
                        throw new InvalidOperationException(
                            "Video deinterlace returned no output path.");
                    }

                    item.CurrentWorkingPath =
                        deinterlacedPath;

                    _logger.LogInformation(
                        "Completed deinterlace for {File}",
                        fileName);
                });
        }
    }
}