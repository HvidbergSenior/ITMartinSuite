using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Application.Interfaces;

// Port for the Ugemål of one ledger (a small file next to the database - no table needed).
public interface IWeeklyGoalStore
{
    Task<IReadOnlyList<WeeklyGoal>> GetAsync(string ledgerId, CancellationToken ct = default);
    Task SaveAsync(string ledgerId, IReadOnlyList<WeeklyGoal> goals, CancellationToken ct = default);
}
