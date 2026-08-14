using ITMartinLibrary.Domain.Entities;

namespace ITMartinLibrary.Application.Interfaces;

public interface IInventoryRepository
{
    Task<InventoryItem?> GetByBarcodeAsync(Guid groupId, string barcode);
    Task<InventoryItem?> GetByTitleAsync(Guid groupId, string title);
    Task<InventoryItem?> GetByIdAsync(Guid groupId, int id);
    Task<List<InventoryItem>> GetAllAsync(Guid groupId);
    Task<List<InventoryItem>> SearchAsync(Guid groupId, string text);
    Task AddAsync(InventoryItem item);
    Task UpdateAsync(InventoryItem item);
}