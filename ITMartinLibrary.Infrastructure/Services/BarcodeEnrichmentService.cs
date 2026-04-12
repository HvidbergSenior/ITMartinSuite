using System.Threading.Channels;
using ITMartinLibrary.Application.Interfaces;

namespace ITMartinLibrary.Infrastructure.Services;

public class BarcodeEnrichmentQueue : IBarcodeEnrichmentQueue
{
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>();

    public void Enqueue(string barcode)
    {
        _queue.Writer.TryWrite(barcode);
    }

    public async Task<string> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}