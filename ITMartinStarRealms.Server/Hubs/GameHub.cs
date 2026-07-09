using Microsoft.AspNetCore.SignalR;

namespace ITMartinStarRealms.Server.Hubs;

public sealed class GameHub : Hub
{
    public async Task JoinSession(string code) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, code.ToUpper());

    public async Task LeaveSession(string code) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, code.ToUpper());
}
