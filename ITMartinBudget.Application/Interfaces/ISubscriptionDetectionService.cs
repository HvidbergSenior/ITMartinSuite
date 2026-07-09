using ITMartinBudget.Application.Models;

namespace ITMartinBudget.Application.Interfaces;

public interface ISubscriptionDetectionService
{
    Task<List<DetectedSubscription>> DetectAsync();
}
