using ITMartin.Magic.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class DetectCardWorkflowStep
    : IWorkflowStep
{
    private readonly
        ICardLayoutDetectionService
        _cardLayoutDetectionService;

    public string Name =>
        nameof(DetectCardWorkflowStep);

    public DetectCardWorkflowStep(
        ICardLayoutDetectionService
            cardLayoutDetectionService)
    {
        _cardLayoutDetectionService =
            cardLayoutDetectionService;
    }

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        var state =
            context.State as CardScanWorkflowState;

        if (state is null)
        {
            throw new InvalidOperationException(
                "Invalid workflow state.");
        }

        var result =
            await _cardLayoutDetectionService
                .DetectAsync(
                    state.ImagePath);

        if (result is null)
        {
            throw new InvalidOperationException(
                "Card detection failed.");
        }

        state.DetectionResult =
            result;
    }
}