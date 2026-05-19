namespace ITMartin.Media.Contracts.Contracts.Runtime.Services;

public interface IWorkflowRecoveryService
{
    Task RecoverAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
}