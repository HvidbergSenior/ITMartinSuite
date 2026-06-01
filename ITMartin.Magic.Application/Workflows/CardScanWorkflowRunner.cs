using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows;

public sealed class CardScanWorkflowRunner
{
    private readonly IWorkflowExecutor _workflowExecutor;

    private readonly CardScanWorkflowDefinition _workflowDefinition;

    public CardScanWorkflowRunner(
        IWorkflowExecutor workflowExecutor,
        CardScanWorkflowDefinition workflowDefinition)
    {
        _workflowExecutor =
            workflowExecutor;

        _workflowDefinition =
            workflowDefinition;
    }

    public async Task ExecuteAsync(
        CardScanContext context,
        CancellationToken cancellationToken)
    {
        await _workflowExecutor.ExecuteAsync(
            _workflowDefinition,
            new WorkflowExecutionContext<CardScanContext>
            {
                WorkflowId = Guid.NewGuid(),
                WorkflowName = _workflowDefinition.Name,
                State = context,
                CancellationToken = cancellationToken
            },
            cancellationToken);
    }
}