using ITMartinSalg.Infrastructure.External;
using ITMartinSalg.Infrastructure.Persistence;
using ITMartinKunder.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinSalg.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The data folder: salg.db, keys/ (Salg__DataDir, default /app/data in the container).</summary>
    public static string DataDir(IConfiguration cfg) => cfg["Salg:DataDir"] ?? "/app/data";

    public static IServiceCollection AddSalgInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var dataDir = DataDir(cfg);
        Directory.CreateDirectory(dataDir);
        services.AddDbContextFactory<SalgDb>(o => o.UseSqlite($"Data Source={Path.Combine(dataDir, "salg.db")};Default Timeout=30"));
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<IInterestRepository, InterestRepository>();
        services.AddHttpClient<IPaymentGateway, StripeCheckout>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient<IStockGateway, LagerStockLink>(c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddKunderClient(cfg, dataDir);
        services.AddScoped<ICustomerRecord, KunderBuyers>();

        services.AddSingleton(new SalgOptions
        {
            ShopKey = cfg["Salg:ShopKey"] ?? "",
            ShopAutoMaxKr = int.TryParse(cfg["Salg:ShopAutoMaxKr"], out var auto) ? auto : Shop.DefaultAutoMaxKr,
            ShopShippingKr = int.TryParse(cfg["Salg:ShopShippingKr"], out var ship) ? ship : 0,
            ShopPickup = cfg["Salg:ShopPickup"] ?? "",
        });
        return services;
    }

    /// <summary>Creates or upgrades salg.db - call once at start. The first time, Lager's old orders and offers are
    /// copied from data/import/lager.db when that copy is there (put there once at the move, 2026-10-21).</summary>
    public static void UpgradeSalgDatabase(this IServiceProvider services, IConfiguration cfg)
    {
        using var db = services.GetRequiredService<IDbContextFactory<SalgDb>>().CreateDbContext();
        SalgDbInitializer.Upgrade(db, Path.Combine(DataDir(cfg), "import", "lager.db"));
    }
}
