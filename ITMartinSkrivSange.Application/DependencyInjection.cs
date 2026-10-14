using Microsoft.Extensions.DependencyInjection;

namespace ITMartinSkrivSange.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSkrivSangeApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<SongWriting>();
        services.AddScoped<Practice>();
        services.AddScoped<Drafts>();
        services.AddScoped<Vault>();
        return services;
    }
}
