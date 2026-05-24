using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoDenoiseWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IVideoEnhancementService
        _videoEnhancementService;

    private readonly ILogger<
            VideoDenoiseWorkflowStep>
        _logger;

    public override string Name =>
        nameof(VideoDenoiseWorkflowStep);

    public VideoDenoiseWorkflowStep(
        IVideoEnhancementService videoEnhancementService,
        ILogger<VideoDenoiseWorkflowStep> logger)
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
                    => "hqdn3d=8:8:6:6",

                RestorationProfile.FamilyArchive
                    => "hqdn3d=1.5:1.5:1:1",

                _ => "hqdn3d=3:3:2:2"
            };

        _logger.LogInformation(
            "Video denoise step running with filter {Filter}",
            filter);

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
                        "Starting video denoise for {File}",
                        fileName);

                    var denoisedPath =
                        await _videoEnhancementService
                            .DenoiseAsync(
                                item.CurrentWorkingPath!,
                                filter,
                                progressValue =>
                                {
                                    _logger.LogInformation(
                                        "Video denoise progress {File}: {Progress:P0}",
                                        fileName,
                                        progressValue);
                                },
                                cancellationToken);

                    if (string.IsNullOrWhiteSpace(
                            denoisedPath))
                    {
                        throw new InvalidOperationException(
                            "Video denoise returned no output path.");
                    }

                    item.CurrentWorkingPath =
                        denoisedPath;

                    _logger.LogInformation(
                        "Completed video denoise for {File}",
                        fileName);
                });
        }
    }
}