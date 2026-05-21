namespace ITMartin.Magic.Application.Workflows;

public interface ICardScanWorkflow
{
    Task<CardScanWorkflowResult> ExecuteAsync(
        CardScanContext context,
        CancellationToken cancellationToken);
}