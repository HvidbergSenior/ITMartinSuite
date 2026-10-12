using Microsoft.Extensions.DependencyInjection;

namespace ITMartinVareinfo.Application;

public static class DependencyInjection
{
    /// <summary>The use cases. The ports come from AddVareinfoInfrastructure.</summary>
    public static IServiceCollection AddVareinfoApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ReadingUseCases>();
        services.AddScoped<LagerLinks>();
        services.AddScoped<KolofonUseCases>();
        return services;
    }
}
