using System.Collections.Concurrent;
using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Application.Models.Workflows;

namespace ITMartin.Media.Infrastructure.Workflows;

public sealed class InMemoryWorkflowResumeStore
    : IWorkflowResumeStore
{
    private static readonly ConcurrentDictionary<
        Guid,
        WorkflowResumeState> States = new();

    public Task SaveAsync(
        WorkflowResumeState state,
        CancellationToken cancellationToken = default)
    {
        States[state.WorkflowId] = state;

        return Task.CompletedTask;
    }

    public Task<WorkflowResumeState?> GetAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        States.TryGetValue(
            workflowId,
            out var state);

        return Task.FromResult(state);
    }

    public Task MarkCompletedAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        States.TryRemove(
            workflowId,
            out _);

        return Task.CompletedTask;
    }
}