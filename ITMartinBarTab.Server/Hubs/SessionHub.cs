using Microsoft.AspNetCore.SignalR;

namespace ITMartinBarTab.Server.Hubs;

public sealed class SessionHub : Hub
{
    public async Task JoinSession(string sessionCode)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionCode);
    }

    public async Task LeaveSession(string sessionCode)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionCode);
    }
}
