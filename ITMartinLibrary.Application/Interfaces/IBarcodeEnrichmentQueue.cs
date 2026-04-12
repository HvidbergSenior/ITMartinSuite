namespace ITMartinLibrary.Application.Interfaces;

public interface IBarcodeEnrichmentQueue
{
    void Enqueue(string barcode);
    Task<string> DequeueAsync(CancellationToken cancellationToken);
}