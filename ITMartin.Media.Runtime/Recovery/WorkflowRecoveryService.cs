using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
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
                    Package1WorkflowState>(
                    workflowId,
                    cancellationToken);

        if (state is null)
        {
            return;
        }

        var workflow =
            workflowRegistry.Resolve(
                "Package1Workflow");

        var context =
            new WorkflowExecutionContext<
                Package1WorkflowState>
            {
                WorkflowId = workflowId,

                WorkflowName =
                    "Package1Workflow",

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