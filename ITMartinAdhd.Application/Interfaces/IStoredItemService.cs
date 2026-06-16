using ITMartinAdhd.Application.Models;

namespace ITMartinAdhd.Application.Interfaces;

public interface IStoredItemService
{
    Task<List<StoredItemModel>> GetRecentAsync(int count = 20);
    Task<List<StoredItemModel>> SearchAsync(string query);
    Task<StoredItemModel> SaveAsync(string name, string location, string? notes = null);
    Task UpdateLocationAsync(int id, string location);
    Task DeleteAsync(int id);
}
