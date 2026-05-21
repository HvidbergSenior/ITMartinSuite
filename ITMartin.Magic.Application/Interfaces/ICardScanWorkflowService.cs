namespace ITMartin.Magic.Application.Interfaces;

public interface ICardScanWorkflowService
{
    Task ExecuteAsync(
        string imagePath,
        CancellationToken cancellationToken = default);
}