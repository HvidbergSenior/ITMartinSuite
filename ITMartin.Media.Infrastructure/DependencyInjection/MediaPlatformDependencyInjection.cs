using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure.DependencyInjection;

public static class MediaPlatformDependencyInjection
{
    public static IServiceCollection AddMediaPlatform(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMediaCore(configuration);

        services.AddPackage1Pipeline(configuration);

        services.AddPackage2Pipeline(configuration);

        return services;
    }
}