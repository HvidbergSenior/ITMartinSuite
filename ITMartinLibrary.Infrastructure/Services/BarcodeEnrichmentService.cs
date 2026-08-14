using System.Threading.Channels;
using ITMartinLibrary.Application.Interfaces;

namespace ITMartinLibrary.Infrastructure.Services;

public class BarcodeEnrichmentQueue : IBarcodeEnrichmentQueue
{
    private readonly Channel<(Guid GroupId, string Barcode)> _queue = Channel.CreateUnbounded<(Guid, string)>();

    public void Enqueue(Guid groupId, string barcode)
    {
        _queue.Writer.TryWrite((groupId, barcode));
    }

    public async Task<(Guid GroupId, string Barcode)> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}