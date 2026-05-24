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
            return;
        }
        if (!state.EnableUpscaling)
        {
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
                    _logger.LogInformation(
                        "START VideoUpscale {File}",
                        item.CurrentWorkingPath);

                    using var cts =
                        CancellationTokenSource
                            .CreateLinkedTokenSource(
                                cancellationToken);

                    cts.CancelAfter(
                        TimeSpan.FromMinutes(30));

                    item.CurrentWorkingPath =
                        await _videoEnhancementService
                            .UpscaleAsync(
                                item.CurrentWorkingPath!,
                                cts.Token);

                    _logger.LogInformation(
                        "END VideoUpscale {File}",
                        item.CurrentWorkingPath);
                });
        }
    }
}