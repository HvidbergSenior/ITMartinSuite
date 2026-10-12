using ITMartinSnak.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinSnak.Infrastructure;

public static class DependencyInjection
{
    /// <summary>JSON files, live feed and WebPush - all in the data folder (Snak:DataDir, default /data).</summary>
    public static IServiceCollection AddSnakInfrastructure(this IServiceCollection services, string dataDir)
    {
        services.AddSingleton<IGroupStore>(new JsonGroupStore(dataDir));
        services.AddSingleton<ILiveFeed, SseLiveFeed>();
        services.AddSingleton<IPushSender>(sp => new WebPushSender(dataDir, sp.GetRequiredService<ILogger<WebPushSender>>()));
        return services;
    }
}
