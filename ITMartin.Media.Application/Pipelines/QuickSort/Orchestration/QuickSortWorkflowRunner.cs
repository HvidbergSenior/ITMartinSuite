using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;

public sealed class QuickSortWorkflowRunner
{
    private readonly IWorkflowExecutor
        _workflowExecutor;

    private readonly QuickSortWorkflowDefinition
        _workflowDefinition;

    public QuickSortWorkflowRunner(
        IWorkflowExecutor workflowExecutor,
        QuickSortWorkflowDefinition workflowDefinition)
    {
        _workflowExecutor =
            workflowExecutor;

        _workflowDefinition =
            workflowDefinition;
    }

    public async Task ExecuteAsync(
        Guid workflowId,
        QuickSortWorkflowState state,
        CancellationToken cancellationToken)
    {
        await _workflowExecutor.ExecuteAsync(
            _workflowDefinition,
            new WorkflowExecutionContext<
                QuickSortWorkflowState>
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