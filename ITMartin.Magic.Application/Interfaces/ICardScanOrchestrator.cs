using ITMartin.Magic.Application.Workflows;

namespace ITMartin.Magic.Application.Interfaces;

public interface ICardScanOrchestrator
{
    Task<CardScanContext> ExecuteAsync(
        string imagePath,
        CancellationToken cancellationToken);
}