using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Orchestration;

public sealed class Package2WorkflowRunner
{
    private readonly IWorkflowExecutor
        _workflowExecutor;

    private readonly Package2WorkflowDefinition
        _workflowDefinition;

    public Package2WorkflowRunner(
        IWorkflowExecutor workflowExecutor,
        Package2WorkflowDefinition workflowDefinition)
    {
        _workflowExecutor =
            workflowExecutor;

        _workflowDefinition =
            workflowDefinition;
    }

    public async Task ExecuteAsync(
        Guid workflowId,
        Package2WorkflowState state,
        CancellationToken cancellationToken)
    {
        await _workflowExecutor.ExecuteAsync(
            _workflowDefinition,
            new WorkflowExecutionContext<
                Package2WorkflowState>
            {
                WorkflowId =
                    workflowId,

                WorkflowName =
                    _workflowDefinition.Name,

                State =
                    state,

                CancellationToken =
                    cancellationToken
            },
            cancellationToken);
    }
}