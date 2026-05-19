namespace ITMartin.Media.Application.Abstractions.Workflows;

public interface IWorkflowRecoveryService
{
    Task RecoverAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
}