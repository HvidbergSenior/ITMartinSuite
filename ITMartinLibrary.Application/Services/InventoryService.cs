using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Domain.Entities;

namespace ITMartinLibrary.Application.Services
{
    public class InventoryService
    {
        private readonly IInventoryRepository _repository;

        public InventoryService(IInventoryRepository repository)
        {
            _repository = repository;
        }

        public async Task ScanOrIncrementAsync(string barcode)
        {
            var existing = await _repository.GetByBarcodeAsync(barcode);

            if (existing is not null)
            {
                existing.Quantity++;
                existing.LastScannedAt = DateTime.UtcNow;

                await _repository.UpdateAsync(existing);
                return;
            }

            var item = new InventoryItem
            {
                Barcode = barcode,
                Quantity = 1,
                FirstScannedAt = DateTime.UtcNow,
                LastScannedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(item);
        }

        public async Task AddAsync(InventoryItem item)
        {
            item.FirstScannedAt = DateTime.UtcNow;
            item.LastScannedAt = DateTime.UtcNow;

            if (item.Quantity <= 0)
                item.Quantity = 1;

            await _repository.AddAsync(item);
        }

        public Task<List<InventoryItem>> GetAllAsync()
        {
            return _repository.GetAllAsync();
        }
    }
}