using ITMartin.Magic.Application.Workflows;

public interface ICardScanOrchestrator
{
    Task<CardScanContext> ExecuteAsync(
        string imagePath,
        string? setCode,
        CancellationToken cancellationToken);
}