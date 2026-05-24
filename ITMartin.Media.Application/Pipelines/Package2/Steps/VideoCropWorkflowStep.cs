using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoCropWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IVideoEnhancementService
        _videoEnhancementService;

    public override string Name =>
        nameof(VideoCropWorkflowStep);

    public VideoCropWorkflowStep(
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
        Console.WriteLine($"RESTORATION PROFILE: {state.RestorationProfile}");
        if (state.RestorationProfile !=
            RestorationProfile.VHSAggressive)
        {
            return;
        }

        const string filter =
            "crop=in_w:in_h-32:0:0";
        Console.WriteLine("VIDEO CROP STEP RUNNING");
        
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
                            .CropAsync(
                                item.CurrentWorkingPath!,
                                filter,
                                cancellationToken);
                });
        }
    }
}