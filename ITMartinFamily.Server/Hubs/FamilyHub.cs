using Microsoft.AspNetCore.SignalR;

namespace ITMartinFamily.Server.Hubs;

public sealed class FamilyHub : Hub
{
    public async Task TaskAdded(object task) =>
        await Clients.Others.SendAsync("TaskAdded", task);

    public async Task TaskClaimed(Guid taskId, string claimedBy) =>
        await Clients.Others.SendAsync("TaskClaimed", taskId, claimedBy);

    public async Task TaskCompleted(Guid taskId) =>
        await Clients.Others.SendAsync("TaskCompleted", taskId);

    public async Task TaskDeleted(Guid taskId) =>
        await Clients.Others.SendAsync("TaskDeleted", taskId);
}
