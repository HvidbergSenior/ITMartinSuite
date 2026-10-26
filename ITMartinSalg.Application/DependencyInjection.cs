using Microsoft.Extensions.DependencyInjection;

namespace ITMartinSalg.Application;

public static class DependencyInjection
{
    /// <summary>The use cases. The ports come from AddSalgInfrastructure.</summary>
    public static IServiceCollection AddSalgApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ShopUseCases>();
        services.AddScoped<CampaignUseCases>();
        services.AddScoped<InterestUseCases>();
        return services;
    }
}
