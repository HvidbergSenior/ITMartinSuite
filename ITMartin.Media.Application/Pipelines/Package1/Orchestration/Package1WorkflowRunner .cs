using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package1.Orchestration;

public sealed class Package1WorkflowRunner
{
    private readonly IWorkflowExecutor
        _workflowExecutor;

    private readonly Package1WorkflowDefinition
        _workflowDefinition;

    public Package1WorkflowRunner(
        IWorkflowExecutor workflowExecutor,
        Package1WorkflowDefinition workflowDefinition)
    {
        _workflowExecutor =
            workflowExecutor;

        _workflowDefinition =
            workflowDefinition;
    }

    public async Task ExecuteAsync(
        Guid workflowId,
        Package1WorkflowState state,
        CancellationToken cancellationToken)
    {
        await _workflowExecutor.ExecuteAsync(
            _workflowDefinition,
            new WorkflowExecutionContext<
                Package1WorkflowState>
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