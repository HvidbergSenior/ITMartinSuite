using ITMartinMarket.Application.Interfaces;
using ITMartinMarket.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinMarket.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMarketInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=/app/data/market.db";

        services.AddDbContext<MarketDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<ISaleItemRepository, SaleItemRepository>();

        return services;
    }
}
