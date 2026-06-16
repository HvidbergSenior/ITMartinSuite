using Microsoft.Extensions.DependencyInjection;

namespace ITMartinAdhd.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAdhdApplication(
        this IServiceCollection services)
    {
        return services;
    }
}
