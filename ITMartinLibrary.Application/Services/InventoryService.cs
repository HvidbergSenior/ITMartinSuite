using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Domain.Entities;

namespace ITMartinLibrary.Application.Services;

public class InventoryService
{
    private readonly IInventoryRepository _repository;
    private readonly IBarcodeEnrichmentQueue _queue;

    public InventoryService(
        IInventoryRepository repository,
        IBarcodeEnrichmentQueue queue)
    {
        _repository = repository;
        _queue = queue;
    }

    public async Task AddAsync(InventoryItem item)
    {
        var now = DateTime.UtcNow;

        item.FirstScannedAt = now;
        item.LastScannedAt = now;
        item.DetailsUpdatedAt = now;

        if (string.IsNullOrWhiteSpace(item.LookupStatus))
            item.LookupStatus = "Queued";

        await _repository.AddAsync(item);

        if (!string.IsNullOrWhiteSpace(item.Barcode))
            _queue.Enqueue(item.Barcode);
    }

    public async Task ScanOrIncrementAsync(string barcode)
    {
        var now = DateTime.UtcNow;

        var item = await _repository.GetByBarcodeAsync(barcode);

        if (item is null)
        {
            item = new InventoryItem
            {
                Barcode = barcode,
                Title = "Untitled",
                Quantity = 1,
                LookupStatus = "Queued",
                FirstScannedAt = now,
                LastScannedAt = now,
                DetailsUpdatedAt = now
            };

            await _repository.AddAsync(item);
        }
        else
        {
            item.Quantity += 1;
            item.LastScannedAt = now;
            item.DetailsUpdatedAt = now;

            await _repository.UpdateAsync(item);
        }

        _queue.Enqueue(barcode);
    }

    public async Task UpdateAsync(InventoryItem item)
    {
        item.DetailsUpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(item);
    }

    public async Task<InventoryItem?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<List<InventoryItem>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }
}