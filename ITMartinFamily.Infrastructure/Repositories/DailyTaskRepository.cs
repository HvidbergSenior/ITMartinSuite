using ITMartinFamily.Application.Interfaces;
using ITMartinFamily.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinFamily.Infrastructure.Repositories;

public sealed class DailyTaskRepository(FamilyDbContext db) : IDailyTaskRepository
{
    public Task<List<DailyTask>> GetTodayAsync(CancellationToken ct = default)
        => db.Tasks
            .Where(t => t.Date == DateOnly.FromDateTime(DateTime.Today))
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

    public Task<DailyTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Tasks.FindAsync([id], ct).AsTask();

    public async Task AddAsync(DailyTask task, CancellationToken ct = default)
    {
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(DailyTask task, CancellationToken ct = default)
    {
        db.Tasks.Update(task);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var task = await db.Tasks.FindAsync([id], ct);
        if (task is not null)
        {
            db.Tasks.Remove(task);
            await db.SaveChangesAsync(ct);
        }
    }
}
