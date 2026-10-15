using Microsoft.Extensions.DependencyInjection;

namespace ITMartinBibliotek.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddBibliotekApplication(this IServiceCollection services)
    {
        services.AddSingleton<Playback>();
        services.AddSingleton<Library>();
        services.AddSingleton<Catalog>();
        services.AddSingleton<JellyfinSync>();
        services.AddSingleton<Admin>();
        return services;
    }
}
