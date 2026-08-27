using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package4.Orchestration;

public sealed class Package4WorkflowRunner
{
    private readonly IWorkflowExecutor _workflowExecutor;
    private readonly Package4WorkflowDefinition _workflowDefinition;

    public Package4WorkflowRunner(IWorkflowExecutor workflowExecutor, Package4WorkflowDefinition workflowDefinition)
    {
        _workflowExecutor = workflowExecutor;
        _workflowDefinition = workflowDefinition;
    }

    public async Task ExecuteAsync(Guid workflowId, Package4WorkflowState state, CancellationToken cancellationToken)
    {
        await _workflowExecutor.ExecuteAsync(
            _workflowDefinition,
            new WorkflowExecutionContext<Package4WorkflowState>
            {
                WorkflowId = workflowId,
                WorkflowName = _workflowDefinition.Name,
                State = state,
                CancellationToken = cancellationToken
            },
            cancellationToken);
    }
}
