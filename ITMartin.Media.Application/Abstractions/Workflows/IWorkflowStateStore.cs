// File: ITMartin.Media.Application/Abstractions/Workflows/IWorkflowStateStore.cs

namespace ITMartin.Media.Application.Abstractions.Workflows;

public interface IWorkflowStateStore
{
    Task SaveCheckpointAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default);

    Task<string?> GetLastCompletedStepAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
}