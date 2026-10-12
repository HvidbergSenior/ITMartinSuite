using Microsoft.Extensions.DependencyInjection;

namespace ITMartinAuction.Application;

public static class DependencyInjection
{
    /// <summary>The use cases. The ports come from AddAuctionInfrastructure and the Server (broadcast, countdown).</summary>
    public static IServiceCollection AddAuctionApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuctionUseCases>();
        services.AddScoped<LagerUseCases>();
        services.AddScoped<CleanupUseCases>();
        return services;
    }
}
