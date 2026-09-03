using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Runtime.Recovery;

public sealed class WorkflowRecoveryService(
    IWorkflowCheckpointStore checkpointStore,
    IWorkflowRegistry workflowRegistry,
    IWorkflowExecutor workflowExecutor)
    : IWorkflowRecoveryService
{
    public async Task RecoverAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var state =
            await checkpointStore
                .LoadLatestCheckpointAsync<
                    QuickSortWorkflowState>(
                    workflowId,
                    cancellationToken);

        if (state is null)
        {
            return;
        }

        var workflow =
            workflowRegistry.Resolve(
                WorkflowType.QuickSort);

        var context =
            new WorkflowExecutionContext<
                QuickSortWorkflowState>
            {
                WorkflowId = workflowId,

                WorkflowName =
                    "QuickSortWorkflow",

                State = state,

                CancellationToken =
                    cancellationToken
            };

        await workflowExecutor.ExecuteAsync(
            workflow,
            context,
            cancellationToken);
    }
}