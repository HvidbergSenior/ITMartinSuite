using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        Console.WriteLine("BACKGROUND WORKER STARTED");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Console.WriteLine("WAITING FOR BARCODE...");
                var barcode = await _queue.DequeueAsync(stoppingToken);

                Console.WriteLine($"PROCESSING {barcode}");

                using var scope = _serviceProvider.CreateScope();

                var repository = scope.ServiceProvider
                    .GetRequiredService<IInventoryRepository>();

                var lookupService = scope.ServiceProvider
                    .GetRequiredService<IBarcodeLookupService>();

                var item = await repository.GetByBarcodeAsync(barcode);

                if (item is null)
                    continue;

                if (item.LookupStatus == "Completed" &&
                    !string.IsNullOrWhiteSpace(item.Title) &&
                    item.Title != "Untitled")
                    continue;

                item.LookupStatus = "Processing";
                item.DetailsUpdatedAt = DateTime.UtcNow;

                Console.WriteLine($"FINAL SAVE TYPE: {item.Type}");
                Console.WriteLine($"FINAL SAVE TITLE: {item.Title}");
                await repository.UpdateAsync(item);

                InventoryItem? enriched = null;

                // Retry up to 3 times
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    Console.WriteLine($"LOOKUP ATTEMPT {attempt} FOR {barcode}");

                    enriched = await lookupService.LookupAsync(barcode);

                    if (enriched is not null)
                        break;

                    await Task.Delay(1000, stoppingToken);
                }

                if (enriched is null)
                {
                    item.LookupStatus = "Pending Manual Review";
                    item.DetailsUpdatedAt = DateTime.UtcNow;

                    await repository.UpdateAsync(item);

                    Console.WriteLine($"NOT FOUND AFTER RETRY {barcode}");

                    await Task.Delay(300, stoppingToken);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(enriched.Title))
                    item.Title = enriched.Title;

                if (!string.IsNullOrWhiteSpace(enriched.AuthorOrDirector))
                    item.AuthorOrDirector = enriched.AuthorOrDirector;

                if (!string.IsNullOrWhiteSpace(enriched.Type))
                    item.Type = enriched.Type;

                if (!string.IsNullOrWhiteSpace(enriched.Genre))
                    item.Genre = enriched.Genre;

                if (!string.IsNullOrWhiteSpace(enriched.Runtime))
                    item.Runtime = enriched.Runtime;

                if (!string.IsNullOrWhiteSpace(enriched.ReleaseYear))
                    item.ReleaseYear = enriched.ReleaseYear;

                if (!string.IsNullOrWhiteSpace(enriched.Plot))
                    item.Plot = enriched.Plot;

                if (!string.IsNullOrWhiteSpace(enriched.CoverUrl))
                    item.CoverUrl = enriched.CoverUrl;

                item.LookupStatus = "Completed";
                item.DetailsUpdatedAt = DateTime.UtcNow;

                await repository.UpdateAsync(item);

                Console.WriteLine($"UPDATED {barcode}");

                // Small pause between items
                await Task.Delay(300, stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WORKER ERROR: {ex}");
            }
        }
    }
}