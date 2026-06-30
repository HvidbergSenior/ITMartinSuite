using Microsoft.AspNetCore.SignalR;

namespace ITMartinFamily.Server.Hubs;

public sealed class FamilyHub : Hub
{
    public async Task JoinFamily(string slug)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"f-{slug}");

    public async Task TaskAdded(string slug, object task)
        => await Clients.OthersInGroup($"f-{slug}").SendAsync("TaskAdded", task);

    public async Task TaskClaimed(string slug, Guid taskId, string claimedBy)
        => await Clients.OthersInGroup($"f-{slug}").SendAsync("TaskClaimed", taskId, claimedBy);

    public async Task TaskCompleted(string slug, Guid taskId)
        => await Clients.OthersInGroup($"f-{slug}").SendAsync("TaskCompleted", taskId);

    public async Task TaskDeleted(string slug, Guid taskId)
        => await Clients.OthersInGroup($"f-{slug}").SendAsync("TaskDeleted", taskId);

    public async Task SendChat(string slug, string sender, string text)
        => await Clients.OthersInGroup($"f-{slug}").SendAsync("ChatReceived", sender, text, DateTime.UtcNow);
}
