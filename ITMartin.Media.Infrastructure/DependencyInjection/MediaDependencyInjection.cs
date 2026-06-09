using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Media.Infrastructure.DependencyInjection;

public static class MediaDependencyInjection
{
    public static IServiceCollection AddMedia(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMediaInfrastructureCore(
            configuration);

        services.AddMediaRuntime(
            configuration);

        return services;
    }
}