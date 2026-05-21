using ITMartin.Magic.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class BlurDetectionWorkflowStep
    : IWorkflowStep
{
    private readonly
        IBlurDetectionService
        _blurDetectionService;

    public string Name =>
        nameof(BlurDetectionWorkflowStep);

    public BlurDetectionWorkflowStep(
        IBlurDetectionService blurDetectionService)
    {
        _blurDetectionService =
            blurDetectionService;
    }

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        var state =
            context.State as CardScanWorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state.");

        var imagePath =
            state.CorrectedImagePath
            ?? state.ImagePath;

        var isBlurry =
            await _blurDetectionService
                .IsBlurryAsync(imagePath);

        if (isBlurry)
        {
            throw new InvalidOperationException(
                "Image is too blurry.");
        }
    }
}