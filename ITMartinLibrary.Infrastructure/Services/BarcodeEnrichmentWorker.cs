using ITMartinLibrary.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ITMartinLibrary.Application.Interfaces;
namespace ITMartinLibrary.Infrastructure.Services;

public class BarcodeEnrichmentWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IBarcodeEnrichmentQueue _queue;

    public BarcodeEnrichmentWorker(
        IServiceProvider serviceProvider,
        IBarcodeEnrichmentQueue queue)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var barcode = await _queue.DequeueAsync(stoppingToken);

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();

            var item = await repository.GetByBarcodeAsync(barcode);

            if (item is null)
                continue;

            item.Title = $"Auto-filled {barcode}";
            item.Type = "Book";
            item.LookupStatus = "Completed";
            item.DetailsUpdatedAt = DateTime.UtcNow;

            await repository.UpdateAsync(item);
        }
    }
}