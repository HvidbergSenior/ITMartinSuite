using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartinLibrary.Application.Workflows;

public sealed class ShelfScanWorkflowRunner
{
    private readonly IWorkflowExecutor _workflowExecutor;

    private readonly ShelfScanWorkflowDefinition _workflowDefinition;

    public ShelfScanWorkflowRunner(
        IWorkflowExecutor workflowExecutor,
        ShelfScanWorkflowDefinition workflowDefinition)
    {
        _workflowExecutor = workflowExecutor;
        _workflowDefinition = workflowDefinition;
    }

    public async Task ExecuteAsync(
        ShelfScanContext context,
        CancellationToken cancellationToken)
    {
        await _workflowExecutor.ExecuteAsync(
            _workflowDefinition,
            new WorkflowExecutionContext<ShelfScanContext>
            {
                WorkflowId = Guid.NewGuid(),
                WorkflowName = _workflowDefinition.Name,
                State = context,
                CancellationToken = cancellationToken
            },
            cancellationToken);
    }
}
