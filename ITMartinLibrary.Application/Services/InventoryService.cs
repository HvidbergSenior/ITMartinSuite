using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Domain.Entities;

namespace ITMartinLibrary.Application.Services
{
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

        public async Task ScanOrIncrementAsync(string barcode)
        {
            var existing = await _repository.GetByBarcodeAsync(barcode);

            if (existing is not null)
            {
                existing.Quantity++;
                existing.LastScannedAt = DateTime.UtcNow;
                existing.LookupStatus = "Pending";

                await _repository.UpdateAsync(existing);

                _queue.Enqueue(barcode);

                return;
            }

            var item = new InventoryItem
            {
                Barcode = barcode,
                Quantity = 1,
                LookupStatus = "Pending",
                FirstScannedAt = DateTime.UtcNow,
                LastScannedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(item);

            _queue.Enqueue(barcode);
        }

        public async Task AddAsync(InventoryItem item)
        {
            item.LastScannedAt = DateTime.UtcNow;
            item.LookupStatus = "Pending";
            
            if (item.Quantity <= 0)
                item.Quantity = 1;

            var existing = await _repository.GetByBarcodeAsync(item.Barcode);

            if (existing is not null)
            {
                existing.Quantity += item.Quantity;

                if (!string.IsNullOrWhiteSpace(item.Title))
                    existing.Title = item.Title;

                if (!string.IsNullOrWhiteSpace(item.Type))
                    existing.Type = item.Type;

                if (!string.IsNullOrWhiteSpace(item.AuthorOrDirector))
                    existing.AuthorOrDirector = item.AuthorOrDirector;

                if (!string.IsNullOrWhiteSpace(item.ShelfLocation))
                    existing.ShelfLocation = item.ShelfLocation;

                if (item.Price > 0)
                    existing.Price = item.Price;

                existing.LastScannedAt = DateTime.UtcNow;

                await _repository.UpdateAsync(existing);
                return;
            }

            item.FirstScannedAt = DateTime.UtcNow;

            await _repository.AddAsync(item);

            if (!string.IsNullOrWhiteSpace(item.Barcode))
            {
                _queue.Enqueue(item.Barcode);
            }
        }

        public Task<List<InventoryItem>> GetAllAsync()
        {
            return _repository.GetAllAsync();
        }
        public Task<InventoryItem?> GetByIdAsync(int id)
        {
            return _repository.GetByIdAsync(id);
        }

        public Task UpdateAsync(InventoryItem item)
        {
            item.LastScannedAt = DateTime.UtcNow;
            return _repository.UpdateAsync(item);
        }
    }
}