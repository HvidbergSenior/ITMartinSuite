// File: ITMartin.Media.Infrastructure.SignalR/SignalRDependencyInjection.cs

using ITMartin.Media.Application.Abstractions.Runtime;
using ITMartin.Media.Infrastructure.SignalR.Hubs;
using ITMartin.Media.Infrastructure.SignalR.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure;

public static class SignalRDependencyInjection
{
    public static IServiceCollection AddMediaSignalR(
        this IServiceCollection services)
    {
        services.AddSignalR();

        services.AddSingleton<
            IRuntimeEventPublisher,
            SignalRRuntimeEventPublisher>();

        return services;
    }

    public static IEndpointRouteBuilder MapMediaSignalRHubs(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<RuntimeHub>(
            RuntimeHub.HubRoute);

        return endpoints;
    }
}