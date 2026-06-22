using ITMartinAdhd.Application.Interfaces;
using ITMartinAdhd.Application.Models;
using ITMartinAdhd.Domain.Entities;
using ITMartinAdhd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ITMartinAdhd.Infrastructure.Services;

public sealed class StoredItemService : IStoredItemService
{
    private readonly AdhdDbContext _db;
    private readonly string _photoDir;

    public StoredItemService(AdhdDbContext db, IConfiguration config)
    {
        _db = db;
        _photoDir = config["AdhdSettings:PhotoDir"] ?? "/app/data/photos";
        Directory.CreateDirectory(_photoDir);
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

    public async Task<StoredItemModel> SaveAsync(string name, string location, string? notes = null, byte[]? photo = null)
    {
        var now = DateTime.UtcNow;

        var existing = await _db.StoredItems
            .FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower());

        if (existing is not null)
        {
            existing.Location = location;
            existing.Notes = notes;
            existing.UpdatedAt = now;
            if (photo is not null)
                existing.PhotoPath = await SavePhotoAsync(existing.Id, photo);
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

        if (photo is not null)
        {
            item.PhotoPath = await SavePhotoAsync(item.Id, photo);
            await _db.SaveChangesAsync();
        }

        return ToModel(item);
    }

    private async Task<string> SavePhotoAsync(int id, byte[] bytes)
    {
        var fileName = $"{id}.jpg";
        var path = Path.Combine(_photoDir, fileName);
        await File.WriteAllBytesAsync(path, bytes);
        return $"/photos/{fileName}";
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

        if (item.PhotoPath is not null)
        {
            var file = Path.Combine(_photoDir, Path.GetFileName(item.PhotoPath));
            if (File.Exists(file)) File.Delete(file);
        }

        _db.StoredItems.Remove(item);
        await _db.SaveChangesAsync();
    }

    private static StoredItemModel ToModel(StoredItem x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Location = x.Location,
        Notes = x.Notes,
        PhotoPath = x.PhotoPath,
        StoredAt = x.StoredAt,
        UpdatedAt = x.UpdatedAt,
    };
}
