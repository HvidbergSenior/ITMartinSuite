using ITMartinFamily.Application.Interfaces;
using ITMartinFamily.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinFamily.Infrastructure.Repositories;

public sealed class FamilyStoredItemRepository(FamilyDbContext db) : IFamilyStoredItemRepository
{
    public Task<List<FamilyStoredItem>> GetRecentAsync(Guid familyId, int count = 20, CancellationToken ct = default)
        => db.StoredItems
            .Where(i => i.FamilyId == familyId)
            .OrderByDescending(i => i.UpdatedAt)
            .Take(count)
            .ToListAsync(ct);

    public Task<List<FamilyStoredItem>> SearchAsync(Guid familyId, string query, CancellationToken ct = default)
        => db.StoredItems
            .Where(i => i.FamilyId == familyId
                && (EF.Functions.Like(i.Name, $"%{query}%")
                    || EF.Functions.Like(i.Location, $"%{query}%")
                    || (i.Notes != null && EF.Functions.Like(i.Notes, $"%{query}%"))))
            .OrderByDescending(i => i.UpdatedAt)
            .Take(20)
            .ToListAsync(ct);

    public Task<FamilyStoredItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.StoredItems.FindAsync([id], ct).AsTask();

    public async Task<FamilyStoredItem> SaveAsync(Guid familyId, string name, string location, string? notes, string? photoPath, CancellationToken ct = default)
    {
        var existing = await db.StoredItems
            .FirstOrDefaultAsync(i => i.FamilyId == familyId && i.Name.ToLower() == name.ToLower(), ct);

        if (existing is not null)
        {
            existing.Location  = location;
            existing.Notes     = notes;
            existing.UpdatedAt = DateTime.UtcNow;
            if (photoPath is not null)
            {
                DeleteFile(existing.PhotoPath);
                existing.PhotoPath = photoPath;
            }
            await db.SaveChangesAsync(ct);
            return existing;
        }

        var item = new FamilyStoredItem
        {
            FamilyId  = familyId,
            Name      = name,
            Location  = location,
            Notes     = notes,
            PhotoPath = photoPath
        };
        db.StoredItems.Add(item);
        await db.SaveChangesAsync(ct);
        return item;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = await db.StoredItems.FindAsync([id], ct);
        if (item is not null)
        {
            DeleteFile(item.PhotoPath);
            db.StoredItems.Remove(item);
            await db.SaveChangesAsync(ct);
        }
    }

    private static void DeleteFile(string? path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            try { File.Delete(path); } catch { }
    }
}
