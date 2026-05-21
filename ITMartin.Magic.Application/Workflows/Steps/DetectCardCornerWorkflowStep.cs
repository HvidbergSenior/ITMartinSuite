using ITMartin.Magic.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class DetectCardCornersWorkflowStep
    : IWorkflowStep
{
    private readonly
        ICardCornerDetectionService
        _cardCornerDetectionService;

    public string Name =>
        nameof(DetectCardCornersWorkflowStep);

    public DetectCardCornersWorkflowStep(
        ICardCornerDetectionService
            cardCornerDetectionService)
    {
        _cardCornerDetectionService =
            cardCornerDetectionService;
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

        var result =
            await _cardCornerDetectionService
                .DetectAsync(
                    state.ImagePath);

        if (result is null)
        {
            throw new InvalidOperationException(
                "Card corner detection failed.");
        }

        state.CornerResult =
            result;
    }
}