using Microsoft.AspNetCore.SignalR;

namespace ITMartinFamily.Server.Hubs;

public sealed class FamilyHub : Hub
{
    public async Task JoinFamily(string slug)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"f-{slug}");

    public async Task TaskAdded(string slug, object task)
        => await Clients.OthersInGroup($"f-{slug}").SendAsync("TaskAdded", task);

    public async Task TaskAssigned(string slug, Guid taskId, string assignedTo)
        => await Clients.OthersInGroup($"f-{slug}").SendAsync("TaskAssigned", taskId, assignedTo);

    public async Task TaskCompleted(string slug, Guid taskId, string completedBy)
        => await Clients.OthersInGroup($"f-{slug}").SendAsync("TaskCompleted", taskId, completedBy);

    public async Task TaskDeleted(string slug, Guid taskId)
        => await Clients.OthersInGroup($"f-{slug}").SendAsync("TaskDeleted", taskId);

    public async Task SendChat(string slug, string sender, string text)
        => await Clients.OthersInGroup($"f-{slug}").SendAsync("ChatReceived", sender, text, DateTime.UtcNow);
}
