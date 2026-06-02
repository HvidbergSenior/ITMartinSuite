using ITMartin.Magic.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class BlurDetectionWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly
        IBlurDetectionService
        _blurDetectionService;

    public override string Name =>
        nameof(BlurDetectionWorkflowStep);

    public BlurDetectionWorkflowStep(
        IBlurDetectionService blurDetectionService)
    {
        _blurDetectionService =
            blurDetectionService;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        var imagePath =
            context.State.PerspectiveCorrectedImagePath
            ?? context.State.ImagePath;

        var isBlurry =
            await _blurDetectionService
                .IsBlurryAsync(
                    imagePath,
                    cancellationToken:
                    cancellationToken);
        
        context.State.IsBlurry =
            isBlurry;
    }
}