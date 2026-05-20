namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public interface IWorkflowRecoveryService
{
    Task RecoverAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
}