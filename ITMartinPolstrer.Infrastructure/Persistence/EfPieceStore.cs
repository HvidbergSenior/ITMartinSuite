using ITMartinPolstrer.Application;
using ITMartinPolstrer.Domain;
using Microsoft.EntityFrameworkCore;

namespace ITMartinPolstrer.Infrastructure.Persistence;

// A short-lived DbContext per call: Blazor Server keeps scoped services for the whole visit.
public sealed class EfPieceStore(IDbContextFactory<PolstrerDbContext> dbf) : IPieceStore
{
    private static IQueryable<Piece> Full(PolstrerDbContext db) =>
        db.Pieces.Include(p => p.Steps).ThenInclude(s => s.Media).Include(p => p.Comments).AsSplitQuery();

    public async Task<Piece?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await Full(db).AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug, ct);
    }

    public async Task<List<Piece>> AllAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await Full(db).AsNoTracking().ToListAsync(ct);
    }

    public async Task<List<string>> WorkersAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Pieces.Select(p => p.Worker).Where(w => w != "").Distinct().OrderBy(w => w).ToListAsync(ct);
    }

    public async Task<Piece> SaveGraphAsync(Guid? id, Func<Piece> create, Func<Piece, Task> change, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var piece = id is { } pid
            ? await db.Pieces.Include(p => p.Steps).ThenInclude(s => s.Media).FirstOrDefaultAsync(p => p.Id == pid, ct)
            : null;
        if (piece is null)
        {
            piece = create();
            await change(piece);
            db.Pieces.Add(piece);                   // the whole new graph is Added
        }
        else
        {
            var steps = piece.Steps.Select(s => s.Id).ToHashSet();
            var media = piece.Steps.SelectMany(s => s.Media).Select(m => m.Id).ToHashSet();
            await change(piece);
            // Keys are client-made Guids, so EF would take a new photo on an existing step for an existing row (UPDATE).
            // New steps and photos are added explicitly instead; changed paths on existing ones are picked up as usual.
            foreach (var s in piece.Steps.Where(s => !steps.Contains(s.Id)).ToList()) db.Steps.Add(s);
            foreach (var m in piece.Steps.Where(s => steps.Contains(s.Id)).SelectMany(s => s.Media).Where(m => !media.Contains(m.Id)).ToList()) db.Media.Add(m);
        }
        await db.SaveChangesAsync(ct);
        return piece;
    }

    public async Task UpdatePieceAsync(Guid id, Action<Piece> change, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var piece = await db.Pieces.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (piece is null) return;
        change(piece);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeletePieceAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Pieces.Where(p => p.Id == id).ExecuteDeleteAsync(ct);   // steps, media rows and comments cascade
    }

    public async Task AddStepAsync(Step step, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Steps.Add(step);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateStepNoteAsync(Guid stepId, string note, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Steps.Where(s => s.Id == stepId).ExecuteUpdateAsync(u => u.SetProperty(s => s.Note, note), ct);
    }

    public async Task DeleteStepAsync(Guid stepId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Steps.Where(s => s.Id == stepId).ExecuteDeleteAsync(ct);
    }

    public async Task DeleteMediaAsync(Guid mediaId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Media.Where(m => m.Id == mediaId).ExecuteDeleteAsync(ct);
    }

    public async Task AddCommentAsync(Comment comment, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteCommentAsync(Guid commentId, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.Comments.Where(c => c.Id == commentId).ExecuteDeleteAsync(ct);
    }
}
