using Microsoft.EntityFrameworkCore;

namespace ITMartinLaes.Infrastructure.Persistence;

public sealed class AiDay
{
    public DateOnly Day { get; set; }
    public int Calls { get; set; }
}

public sealed class LaesDb(DbContextOptions<LaesDb> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<AiDay> AiDays => Set<AiDay>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Book>().HasKey(x => x.Id);
        b.Entity<Book>().Property(x => x.State).HasConversion<string>();
        b.Entity<Book>().Ignore(x => x.Points);
        b.Entity<Book>().Ignore(x => x.Chapters);
        b.Entity<Book>().Ignore(x => x.Notes);
        b.Entity<AiDay>().HasKey(x => x.Day);
    }
}

public sealed class BookRepository(IDbContextFactory<LaesDb> dbf) : IBookRepository
{
    public async Task AddAsync(Book book, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Books.Add(book);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Book?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<List<Book>> AllAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var all = await db.Books.AsNoTracking().ToListAsync(ct);
        return all.OrderByDescending(b => b.ReadAt ?? b.AddedAt).ToList();
    }

    public async Task UpdateAsync(Book book, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Books.Update(book);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Books.Where(b => b.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task<Book?> NextToSummariseAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var waiting = await db.Books.AsNoTracking()
            .Where(b => b.State == SummaryState.Venter || b.State == SummaryState.Læser).ToListAsync(ct);
        return waiting.OrderBy(b => b.AddedAt).FirstOrDefault();
    }
}
