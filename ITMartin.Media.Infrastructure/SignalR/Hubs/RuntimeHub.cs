// File: ITMartin.Media.Infrastructure.SignalR/Hubs/RuntimeHub.cs

using Microsoft.AspNetCore.SignalR;

namespace ITMartin.Media.Infrastructure.SignalR.Hubs;

public sealed class RuntimeHub : Hub
{
    public const string HubRoute = "/media/runtime";
}