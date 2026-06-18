using ITMartinFamily.Domain.Entities;

namespace ITMartinFamily.Application.Interfaces;

public interface IDailyTaskRepository
{
    Task<List<DailyTask>> GetTodayAsync(CancellationToken ct = default);
    Task<DailyTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(DailyTask task, CancellationToken ct = default);
    Task UpdateAsync(DailyTask task, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
