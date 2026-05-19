namespace ITMartin.Media.Runtime.Interfaces;

public interface IWorkflowRecoveryService
{
    Task RecoverAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
}