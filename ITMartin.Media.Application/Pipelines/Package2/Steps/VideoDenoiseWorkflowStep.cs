using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoDenoiseWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IVideoEnhancementService
        _videoEnhancementService;

    public override string Name =>
        nameof(VideoDenoiseWorkflowStep);

    public VideoDenoiseWorkflowStep(
        IVideoEnhancementService videoEnhancementService)
    {
        _videoEnhancementService =
            videoEnhancementService;
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
                    item.CurrentWorkingPath =
                        await _videoEnhancementService
                            .DenoiseAsync(
                                item.CurrentWorkingPath!,
                                filter,
                                cancellationToken);
                });
        }
    }
}