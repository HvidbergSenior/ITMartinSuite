using ITMartinLibrary.Domain;
using ITMartinLibrary.Domain.Entities;

namespace ITMartinLibrary.Application.Interfaces
{
    public interface IInventoryRepository
    {
        Task<List<InventoryItem>> GetAllAsync();
        Task AddAsync(InventoryItem item);
        Task<List<InventoryItem>> SearchAsync(string text);
        Task UpdateAsync(InventoryItem item);
        Task<InventoryItem?> GetByBarcodeAsync(string barcode);
    }
}