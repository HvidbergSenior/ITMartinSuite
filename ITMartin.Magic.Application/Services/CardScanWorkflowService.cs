using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Workflows;

namespace ITMartin.Magic.Application.Services;

public sealed class CardScanWorkflowService
    : ICardScanWorkflowService
{
    private readonly
        IWorkflowExecutor
        _workflowExecutor;

    private readonly
        CardScanWorkflowDefinition
        _workflow;

    public CardScanWorkflowService(
        IWorkflowExecutor workflowExecutor,
        CardScanWorkflowDefinition workflow)
    {
        _workflowExecutor =
            workflowExecutor;

        _workflow =
            workflow;
    }

    public async Task ExecuteAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var state =
            new CardScanWorkflowState
            {
                ImagePath = imagePath
            };

        var context =
            new WorkflowExecutionContext<
                CardScanWorkflowState>
            {
                WorkflowId =
                    Guid.NewGuid(),

                WorkflowName =
                    _workflow.Name,

                State =
                    state,

                CancellationToken =
                    cancellationToken
            };

        await _workflowExecutor.ExecuteAsync(
            _workflow,
            context,
            cancellationToken);
    }
}