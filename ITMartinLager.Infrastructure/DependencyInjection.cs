using ITMartinLager.Infrastructure.External;
using ITMartinLager.Infrastructure.Persistence;
using ITMartinLager.Infrastructure.PriceSites;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinLager.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The data folder: lager.db, photos/ (Lager__DataDir, default /app/data in the container).</summary>
    public static string DataDir(IConfiguration cfg) => cfg["Lager:DataDir"] ?? "/app/data";

    public static IServiceCollection AddLagerInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var dataDir = DataDir(cfg);
        Directory.CreateDirectory(Path.Combine(dataDir, "photos"));
        services.AddDbContextFactory<LagerDb>(o => o.UseSqlite($"Data Source={Path.Combine(dataDir, "lager.db")}"));
        services.AddScoped<IBoxRepository, BoxRepository>();
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IPhotoRepository, PhotoRepository>();
        services.AddScoped<IPriceRepository, PriceRepository>();
        services.AddScoped<IBookCheckRepository, BookCheckRepository>();

        services.AddSingleton<IPhotoStore>(new FilePhotoStore(dataDir));
        services.AddSingleton<ICoverReader, ClaudeCoverReader>();
        services.AddHttpClient<IAuctionGateway, AuctionLink>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient<IMagicGateway, MagicLink>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient<IBarcodeLookup, BarcodeLookup>(c => c.Timeout = TimeSpan.FromSeconds(12));
        services.AddPriceSources();

        services.AddSingleton(new LagerOptions
        {
            AutoInterest = cfg.GetValue<bool>("Lager:AutoInterest"),
            KolofonFromKr = int.TryParse(cfg["Lager:KolofonFromKr"], out var kr) ? kr : Kolofon.DefaultFromKr,
        });
        return services;
    }

    /// <summary>Creates or upgrades lager.db - call once at start.</summary>
    public static void UpgradeLagerDatabase(this IServiceProvider services)
    {
        using var db = services.GetRequiredService<IDbContextFactory<LagerDb>>().CreateDbContext();
        LagerDbInitializer.Upgrade(db);
    }
}
