using ITMartin.Receipt.Application.Workflows;

namespace ITMartin.Receipt.Application.Interfaces;

public interface IReceiptWorkflowOrchestrator
{
    Task<ReceiptContext> ExecuteAsync(
        string imagePath,
        CancellationToken cancellationToken);
}