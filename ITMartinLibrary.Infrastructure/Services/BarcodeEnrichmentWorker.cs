using ITMartinLibrary.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ITMartinLibrary.Infrastructure.Services;

public class BarcodeEnrichmentWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IBarcodeEnrichmentQueue _queue;
    private readonly IBarcodeLookupService _lookupService;
    
    public BarcodeEnrichmentWorker(
        IServiceProvider serviceProvider,
        IBarcodeEnrichmentQueue queue,
        IBarcodeLookupService lookupService)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
        _lookupService = lookupService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("BACKGROUND WORKER STARTED");

        while (!stoppingToken.IsCancellationRequested)
        {
            var barcode = await _queue.DequeueAsync(stoppingToken);

            Console.WriteLine($"PROCESSING {barcode}");

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();

            var item = await repository.GetByBarcodeAsync(barcode);

            if (item is null)
                continue;

            if (item.LookupStatus == "Completed" &&
                !string.IsNullOrWhiteSpace(item.Title))
                continue;

            item.LookupStatus = "Processing";
            item.DetailsUpdatedAt = DateTime.UtcNow;

            await repository.UpdateAsync(item);

            var enriched = await _lookupService.LookupAsync(barcode);

            if (enriched is null)
            {
                item.LookupStatus = "Pending Manual Review";
                item.DetailsUpdatedAt = DateTime.UtcNow;

                await repository.UpdateAsync(item);

                Console.WriteLine($"NOT FOUND {barcode}");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(enriched.Title))
                item.Title = enriched.Title;

            if (!string.IsNullOrWhiteSpace(enriched.AuthorOrDirector))
                item.AuthorOrDirector = enriched.AuthorOrDirector;

            if (!string.IsNullOrWhiteSpace(enriched.Type))
                item.Type = enriched.Type;

            item.LookupStatus = "Completed";
            item.DetailsUpdatedAt = DateTime.UtcNow;

            await repository.UpdateAsync(item);

            Console.WriteLine($"UPDATED {barcode}");
        }
    }
}