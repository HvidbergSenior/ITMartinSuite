using ITMartinAdhd.Application.Interfaces;
using ITMartinAdhd.Application.Models;
using ITMartinAdhd.Domain.Entities;
using ITMartinAdhd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITMartinAdhd.Infrastructure.Services;

public sealed class StoredItemService : IStoredItemService
{
    private readonly AdhdDbContext _db;

    public StoredItemService(AdhdDbContext db)
    {
        _db = db;
    }

    public async Task<List<StoredItemModel>> GetRecentAsync(int count = 20)
    {
        return await _db.StoredItems
            .OrderByDescending(x => x.UpdatedAt)
            .Take(count)
            .Select(x => ToModel(x))
            .ToListAsync();
    }

    public async Task<List<StoredItemModel>> SearchAsync(string query)
    {
        var lower = query.ToLower();
        return await _db.StoredItems
            .Where(x => x.Name.ToLower().Contains(lower)
                     || x.Location.ToLower().Contains(lower)
                     || (x.Notes != null && x.Notes.ToLower().Contains(lower)))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(50)
            .Select(x => ToModel(x))
            .ToListAsync();
    }

    public async Task<StoredItemModel> SaveAsync(string name, string location, string? notes = null)
    {
        var now = DateTime.UtcNow;

        var existing = await _db.StoredItems
            .FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower());

        if (existing is not null)
        {
            existing.Location = location;
            existing.Notes = notes;
            existing.UpdatedAt = now;
            await _db.SaveChangesAsync();
            return ToModel(existing);
        }

        var item = new StoredItem
        {
            Name = name,
            Location = location,
            Notes = notes,
            StoredAt = now,
            UpdatedAt = now,
        };

        _db.StoredItems.Add(item);
        await _db.SaveChangesAsync();
        return ToModel(item);
    }

    public async Task UpdateLocationAsync(int id, string location)
    {
        var item = await _db.StoredItems.FindAsync(id);
        if (item is null) return;

        item.Location = location;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var item = await _db.StoredItems.FindAsync(id);
        if (item is null) return;

        _db.StoredItems.Remove(item);
        await _db.SaveChangesAsync();
    }

    private static StoredItemModel ToModel(StoredItem x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Location = x.Location,
        Notes = x.Notes,
        StoredAt = x.StoredAt,
        UpdatedAt = x.UpdatedAt,
    };
}
