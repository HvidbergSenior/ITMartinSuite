// File: ITMartin.Media.Infrastructure.SignalR/Runtime/SignalRRuntimeEventPublisher.cs

using ITMartin.Media.Application.Abstractions.Runtime;
using ITMartin.Media.Infrastructure.SignalR.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ITMartin.Media.Infrastructure.SignalR.Runtime;

public sealed class SignalRRuntimeEventPublisher(
    IHubContext<RuntimeHub> hubContext)
    : IRuntimeEventPublisher
{
    public async Task PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default)
        where T : class
    {
        await hubContext
            .Clients
            .All
            .SendAsync(
                typeof(T).Name,
                message,
                cancellationToken);
    }
}