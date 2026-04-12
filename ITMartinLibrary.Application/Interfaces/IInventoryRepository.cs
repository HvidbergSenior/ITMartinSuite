using ITMartinLibrary.Domain.Entities;

namespace ITMartinLibrary.Application.Interfaces;

public interface IInventoryRepository
{
    Task<InventoryItem?> GetByBarcodeAsync(string barcode);
    Task<InventoryItem?> GetByIdAsync(int id);
    Task<List<InventoryItem>> GetAllAsync();
    Task AddAsync(InventoryItem item);
    Task UpdateAsync(InventoryItem item);
}