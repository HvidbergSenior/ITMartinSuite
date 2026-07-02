using ITMartinFamily.Application.Interfaces;
using ITMartinFamily.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinFamily.Infrastructure.Repositories;

public sealed class PersonalReminderRepository(FamilyDbContext db) : IPersonalReminderRepository
{
    public Task<List<PersonalReminder>> GetTodayAsync(Guid familyId, string memberName, CancellationToken ct = default)
        => db.Reminders
            .Where(r => r.FamilyId == familyId && r.MemberName == memberName && r.Date == DateOnly.FromDateTime(DateTime.Today))
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(PersonalReminder reminder, CancellationToken ct = default)
    {
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PersonalReminder reminder, CancellationToken ct = default)
    {
        db.Reminders.Update(reminder);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var r = await db.Reminders.FindAsync([id], ct);
        if (r is not null) { db.Reminders.Remove(r); await db.SaveChangesAsync(ct); }
    }
}
