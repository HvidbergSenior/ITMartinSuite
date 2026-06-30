using ITMartinFamily.Domain.Entities;

namespace ITMartinFamily.Application.Interfaces;

public interface IPushSubscriptionRepository
{
    Task<List<PushSubscription>> GetByFamilyAsync(Guid familyId);
    Task<List<PushSubscription>> GetByMemberAsync(Guid familyId, string memberName);
    Task UpsertAsync(PushSubscription sub);
    Task DeleteAsync(Guid id);
    Task DeleteByEndpointAsync(string endpoint);
}
