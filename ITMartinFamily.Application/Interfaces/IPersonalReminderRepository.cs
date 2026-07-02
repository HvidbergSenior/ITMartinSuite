using ITMartinFamily.Domain.Entities;

namespace ITMartinFamily.Application.Interfaces;

public interface IPersonalReminderRepository
{
    Task<List<PersonalReminder>> GetTodayAsync(Guid familyId, string memberName, CancellationToken ct = default);
    Task AddAsync(PersonalReminder reminder, CancellationToken ct = default);
    Task UpdateAsync(PersonalReminder reminder, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
