namespace ITMartinLibrary.Application.Interfaces;

public interface IBarcodeEnrichmentQueue
{
    void Enqueue(Guid groupId, string barcode);
    Task<(Guid GroupId, string Barcode)> DequeueAsync(CancellationToken cancellationToken);
}