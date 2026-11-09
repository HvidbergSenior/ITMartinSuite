using Microsoft.Extensions.DependencyInjection;

namespace ITMartinHjemmeside.Application;

public static class DependencyInjection
{
    /// <summary>The use cases. Ports come from AddHjemmesideInfrastructure.</summary>
    public static IServiceCollection AddHjemmesideApplication(this IServiceCollection services, string adminCode)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new Sites(sp.GetRequiredService<ISiteStore>(), sp.GetRequiredService<IPictureStore>(), sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(sp => new Editor(sp.GetRequiredService<ISiteStore>(), sp.GetRequiredService<IPictureStore>(), sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(sp => new Access(adminCode, sp.GetRequiredService<TimeProvider>()));
        return services;
    }
}
