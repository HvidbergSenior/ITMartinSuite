using ITMartinBibliotek.Application;
using ITMartinBibliotek.Domain;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBibliotek.Infrastructure;

public sealed class EfMediaRepository(IDbContextFactory<BibliotekDbContext> dbFactory) : IMediaRepository
{
    public async Task<List<MediaItem>> ForOwnerAsync(string owner)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Items.AsNoTracking().Where(i => i.Owner == owner).ToListAsync();
    }

    public async Task<List<MediaItem>> AllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Items.AsNoTracking().ToListAsync();
    }

    public async Task<MediaItem?> GetAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<MediaItem?> ByBarcodeAsync(string owner, string barcode)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Barcode == barcode && i.Owner == owner);
    }

    public async Task<bool> OwnsAsync(string owner, string jellyfinId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Items.AnyAsync(i => i.JellyfinId == jellyfinId && i.Owner == owner);
    }

    public async Task AddAsync(MediaItem item)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Items.Add(item);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(MediaItem item)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Items.Update(item);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Items.Where(i => i.Id == id).ExecuteDeleteAsync();
    }

    public async Task ApplySyncAsync(IReadOnlyCollection<MediaItem> added, IReadOnlyCollection<MediaItem> changed, IReadOnlyCollection<MediaItem> removed)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Items.AddRange(added);
        db.Items.UpdateRange(changed);
        db.Items.RemoveRange(removed);
        await db.SaveChangesAsync();
    }
}
