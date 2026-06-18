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

    public async Task<(InventoryItem Item, bool IsNew)> ScanOrIncrementAsync(string barcode)
    {
        var now = DateTime.UtcNow;

        var item = await _repository.GetByBarcodeAsync(barcode);
        bool isNew;

        if (item is null)
        {
            isNew = true;
            var type = (barcode.StartsWith("978") || barcode.StartsWith("979"))
                ? "Book"
                : "DVD";

            item = new InventoryItem
            {
                Barcode = barcode,
                Title = "Untitled",
                Type = type,
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
            isNew = false;
            item.Quantity += 1;
            item.LastScannedAt = now;
            item.DetailsUpdatedAt = now;

            await _repository.UpdateAsync(item);
        }

        _queue.Enqueue(barcode);

        return (item, isNew);
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

    public Task<InventoryItem?> GetByBarcodeAsync(string barcode)
        => _repository.GetByBarcodeAsync(barcode);

    public Task<InventoryItem?> GetByTitleAsync(string title)
        => _repository.GetByTitleAsync(title);
}