using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLibrary.Infrastructure.Repositories;

public sealed class ScannedShelfRepository : IScannedShelfRepository
{
    private readonly LibraryDbContext _db;

    public ScannedShelfRepository(LibraryDbContext db) => _db = db;

    public async Task AddShelvesAsync(IList<ScannedShelf> shelves, CancellationToken ct)
    {
        _db.ScannedShelves.AddRange(shelves);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<HashSet<string>> GetExistingTitlesAsync(CancellationToken ct)
    {
        var titles = await _db.ShelfBooks
            .Select(x => x.Title)
            .ToListAsync(ct);

        return titles
            .Select(t => t.Trim().ToLowerInvariant())
            .ToHashSet();
    }

    public Task<IList<ScannedShelf>> GetAllWithBooksAsync(CancellationToken ct) =>
        _db.ScannedShelves
            .Include(x => x.Books)
            .OrderBy(x => x.ShelfNumber)
            .ToListAsync(ct)
            .ContinueWith(t => (IList<ScannedShelf>)t.Result, ct);

    public async Task ClearAllAsync(CancellationToken ct)
    {
        _db.ShelfBooks.RemoveRange(await _db.ShelfBooks.ToListAsync(ct));
        _db.ScannedShelves.RemoveRange(await _db.ScannedShelves.ToListAsync(ct));
        await _db.SaveChangesAsync(ct);
    }

    public Task<int> GetTotalBookCountAsync(CancellationToken ct) =>
        _db.ShelfBooks.CountAsync(ct);
}
