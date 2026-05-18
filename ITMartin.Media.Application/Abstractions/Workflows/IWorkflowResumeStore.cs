using ITMartin.Media.Application.Models.Workflows;

namespace ITMartin.Media.Application.Abstractions.Workflows;

public interface IWorkflowResumeStore
{
    Task SaveAsync(
        WorkflowResumeState state,
        CancellationToken cancellationToken = default);

    Task<WorkflowResumeState?> GetAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
}