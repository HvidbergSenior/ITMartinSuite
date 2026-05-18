namespace ITMartin.Media.Application.Abstractions.Workflows;

public interface IWorkflowRecoveryStore
{
    Task<IReadOnlyCollection<Guid>> GetUnfinishedWorkflowIdsAsync(
        CancellationToken cancellationToken = default);
}