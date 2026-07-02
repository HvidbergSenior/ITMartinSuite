using System.Net;
using System.Text.Json;
using ITMartinFamily.Application.Interfaces;
using Microsoft.Extensions.Logging;
using WebPush;

namespace ITMartinFamily.Infrastructure.Services;

public sealed class PushNotificationService(
    IPushSubscriptionRepository repo,
    VapidKeyProvider vapid,
    ILogger<PushNotificationService> logger) : IPushNotificationService
{
    public string GetPublicKey() => vapid.PublicKey;

    public async Task SendToFamilyAsync(Guid familyId, string excludeMember, string title, string body)
    {
        var subs = await repo.GetByFamilyAsync(familyId);
        await SendAsync(subs.Where(s => s.MemberName != excludeMember), title, body);
    }

    public async Task SendToMemberAsync(Guid familyId, string memberName, string title, string body)
    {
        var subs = await repo.GetByMemberAsync(familyId, memberName);
        await SendAsync(subs, title, body);
    }

    private async Task SendAsync(IEnumerable<Domain.Entities.PushSubscription> subs, string title, string body)
    {
        var (publicKey, privateKey) = vapid.Load();
        var vapidDetails = new VapidDetails("https://idag.itmartin.dk", publicKey, privateKey);
        var payload = JsonSerializer.Serialize(new { title, body });
        var client = new WebPushClient();

        foreach (var sub in subs)
        {
            try
            {
                await client.SendNotificationAsync(
                    new WebPush.PushSubscription(sub.Endpoint, sub.P256DH, sub.Auth),
                    payload,
                    vapidDetails);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                await repo.DeleteAsync(sub.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Push failed for endpoint {Endpoint}", sub.Endpoint[..Math.Min(40, sub.Endpoint.Length)]);
            }
        }
    }
}
