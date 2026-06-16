using ITMartinAdhd.Application.Interfaces;
using ITMartinAdhd.Infrastructure.Persistence;
using ITMartinAdhd.Infrastructure.Services;
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
        services.AddDbContext<AdhdDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IStoredItemService, StoredItemService>();
        services.AddScoped<IAdhdClaudeService, AdhdClaudeService>();

        return services;
    }
}
