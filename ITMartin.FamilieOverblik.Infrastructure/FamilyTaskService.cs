using ITMartin.FamilieOverblik.Domain;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.FamilieOverblik.Infrastructure;

public class FamilyTaskService
{
    private readonly FamilieOverblikDbContext _db;

    public FamilyTaskService(FamilieOverblikDbContext db)
    {
        _db = db;
    }

    public async Task<List<FamilyTask>> GetTodayAsync(CancellationToken ct = default)
    {
        var today = DateTime.Today;
        return await _db.Tasks
            .Where(t => t.CreatedAt >= today)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<FamilyTask> CreateAsync(
        string type,
        string? note,
        string? photoPath,
        string createdBy,
        CancellationToken ct = default)
    {
        var task = new FamilyTask
        {
            Id = Guid.NewGuid(),
            Type = type,
            Note = note,
            PhotoPath = photoPath,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);
        return task;
    }

    public async Task<bool> ClaimAsync(
        Guid id,
        string claimedBy,
        CancellationToken ct = default)
    {
        var task = await _db.Tasks.FindAsync([id], ct);
        if (task is null) return false;

        task.ClaimedBy = claimedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CompleteAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var task = await _db.Tasks.FindAsync([id], ct);
        if (task is null) return false;

        task.IsCompleted = true;
        task.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
