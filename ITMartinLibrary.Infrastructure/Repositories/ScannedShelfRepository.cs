using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLibrary.Infrastructure.Repositories;

public sealed class ScannedShelfRepository : IScannedShelfRepository
{
    private readonly LibraryDbContext _db;

    public ScannedShelfRepository(LibraryDbContext db) => _db = db;

    public async Task SaveShelvesAsync(IList<ScannedShelf> shelves, CancellationToken ct)
    {
        var books = await _db.ShelfBooks.ToListAsync(ct);
        _db.ShelfBooks.RemoveRange(books);

        var existing = await _db.ScannedShelves.ToListAsync(ct);
        _db.ScannedShelves.RemoveRange(existing);

        await _db.SaveChangesAsync(ct);

        _db.ScannedShelves.AddRange(shelves);
        await _db.SaveChangesAsync(ct);
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

    public Task<bool> HasDataAsync(CancellationToken ct) =>
        _db.ScannedShelves.AnyAsync(ct);
}
