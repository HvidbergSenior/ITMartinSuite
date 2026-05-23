using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class CropDetectionWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly IImageEnhancementService
        _imageEnhancementService;

    public override string Name =>
        nameof(CropDetectionWorkflowStep);

    public CropDetectionWorkflowStep(
        IImageEnhancementService imageEnhancementService)
    {
        _imageEnhancementService =
            imageEnhancementService;
    }

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not Package2WorkflowState state)
        {
            return;
        }

        foreach (var item in state.Items
                     .Where(x =>
                         !x.Failed &&
                         x.MediaKind == MediaKind.Image &&
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
                        await _imageEnhancementService
                            .AutoCropAsync(
                                item.CurrentWorkingPath!,
                                cancellationToken);
                });
        }
    }
}