using ITMartinFamily.Application.Interfaces;
using ITMartinFamily.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinFamily.Infrastructure.Repositories;

public sealed class PushSubscriptionRepository(FamilyDbContext db) : IPushSubscriptionRepository
{
    public Task<List<PushSubscription>> GetByFamilyAsync(Guid familyId)
        => db.PushSubscriptions.Where(s => s.FamilyId == familyId).ToListAsync();

    public Task<List<PushSubscription>> GetByMemberAsync(Guid familyId, string memberName)
        => db.PushSubscriptions.Where(s => s.FamilyId == familyId && s.MemberName == memberName).ToListAsync();

    public async Task UpsertAsync(PushSubscription sub)
    {
        var existing = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == sub.Endpoint);
        if (existing is not null)
        {
            existing.FamilyId = sub.FamilyId;
            existing.MemberName = sub.MemberName;
            existing.P256DH = sub.P256DH;
            existing.Auth = sub.Auth;
        }
        else
        {
            db.PushSubscriptions.Add(sub);
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await db.PushSubscriptions.Where(s => s.Id == id).ExecuteDeleteAsync();
    }

    public async Task DeleteByEndpointAsync(string endpoint)
    {
        await db.PushSubscriptions.Where(s => s.Endpoint == endpoint).ExecuteDeleteAsync();
    }
}
