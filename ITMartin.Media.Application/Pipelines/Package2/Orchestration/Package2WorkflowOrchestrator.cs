using ITMartin.Media.Application.Pipelines.Package2.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Orchestration;

public sealed class Package2WorkflowOrchestrator
{
    private readonly IWorkflowExecutor
        _workflowExecutor;

    private readonly Package2WorkflowDefinition
        _workflowDefinition;

    public Package2WorkflowOrchestrator(
        IWorkflowExecutor workflowExecutor,
        Package2WorkflowDefinition workflowDefinition)
    {
        _workflowExecutor = workflowExecutor;
        _workflowDefinition = workflowDefinition;
    }

    public async Task ExecuteAsync(
        Package2WorkflowState workflowState,
        CancellationToken cancellationToken = default)
    {
        var context =
            new WorkflowExecutionContext<Package2WorkflowState>
            {
                WorkflowName =
                    _workflowDefinition.Name,

                State =
                    workflowState
            };

        await _workflowExecutor.ExecuteAsync(
            _workflowDefinition,
            context,
            cancellationToken);
    }
}