using ITMartinAdhd.Application;
using ITMartinAdhd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinAdhd.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAdhdInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        services.AddDbContext<AdhdDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IItemStore, EfItemStore>();
        services.AddSingleton<IPhotoStore>(new FilePhotoStore(configuration["AdhdSettings:PhotoDir"] ?? "/app/data/photos"));
        // The 30-day clean-up deletes things AND photos. It was never running before 2026-10-12, and the one real
        // household item is older than 30 days - so it only runs when switched on: FindIt:ClearAfter30Days=true.
        if (string.Equals(configuration["FindIt:ClearAfter30Days"], "true", StringComparison.OrdinalIgnoreCase))
            services.AddHostedService<StaleCleanup>();
        return services;
    }
}
