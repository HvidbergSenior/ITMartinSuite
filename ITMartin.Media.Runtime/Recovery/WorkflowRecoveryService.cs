using System.Text.Json;
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
        var checkpoint =
            await checkpointStore.GetLatestCheckpointAsync(
                workflowId,
                cancellationToken);

        if (checkpoint is null)
        {
            return;
        }

        var workflow =
            workflowRegistry.Resolve(
                checkpoint.WorkflowName);

        var state =
            JsonSerializer.Deserialize<Package1WorkflowState>(
                checkpoint.StateJson)
            ?? throw new InvalidOperationException(
                "Failed to deserialize workflow state");

        var context =
            new WorkflowExecutionContext<Package1WorkflowState>
            {
                WorkflowId = workflowId,
                WorkflowName = checkpoint.WorkflowName,
                State = state,
                CancellationToken = cancellationToken
            };

        await workflowExecutor.ExecuteAsync(
            workflow,
            context,
            cancellationToken);
    }
}