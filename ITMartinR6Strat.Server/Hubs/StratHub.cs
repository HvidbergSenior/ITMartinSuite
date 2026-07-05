using Microsoft.AspNetCore.SignalR;

namespace ITMartinR6Strat.Server.Hubs;

public sealed class StratHub : Hub
{
    public async Task JoinSession(string code) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, code.ToUpper());
}
