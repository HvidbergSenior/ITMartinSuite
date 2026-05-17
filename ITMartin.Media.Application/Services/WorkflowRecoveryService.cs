using ITMartin.Media.Application.Abstractions.Workflows;

namespace ITMartin.Media.Application.Services.Workflows;

public sealed class WorkflowRecoveryService
{
    private readonly IWorkflowCheckpointStore _checkpointStore;

    public WorkflowRecoveryService(
        IWorkflowCheckpointStore checkpointStore)
    {
        _checkpointStore = checkpointStore;
    }

    public async Task<T?> RecoverStepAsync<T>(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        return await _checkpointStore.LoadCheckpointAsync<T>(
            workflowId,
            stepName,
            cancellationToken);
    }
}