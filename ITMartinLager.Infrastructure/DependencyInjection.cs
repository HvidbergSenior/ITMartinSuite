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
        services.AddScoped<IPriceRepository, PriceRepository>();
        services.AddScoped<IBookCheckRepository, BookCheckRepository>();
        services.AddScoped<IGoldRepository, GoldRepository>();
        services.AddScoped<IIntakeRepository, IntakeRepository>();
        services.AddScoped<ISaleBookingRepository, SaleBookingRepository>();
        services.AddScoped<IKioskRepository, KioskRepository>();

        services.AddSingleton<IPhotoStore>(new FilePhotoStore(dataDir));
        services.AddSingleton<IBookValuer, ClaudeBookValuer>();
        services.AddSingleton<ICoverReader, ClaudeCoverReader>();
        services.AddHttpClient<IBarcodeLookup, BarcodeLookup>(c => c.Timeout = TimeSpan.FromSeconds(12));
        services.AddHttpClient<IComicCatalog, GcdCatalog>(c => c.Timeout = TimeSpan.FromSeconds(15));
        services.AddHttpClient<IVareinfoGateway, VareinfoLink>(c => c.Timeout = TimeSpan.FromSeconds(30));
        // Helpers log in with their kundenummer (Kunder__Url / Kunder__ApiKey, user 2026-10-24).
        ITMartinKunder.Client.KunderClientExtensions.AddKunderClient(services, cfg, dataDir);
        services.AddScoped<IHelperNumbers, External.KunderHelperNumbers>();
        services.AddHttpClient<IAuctionGateway, AuctionLink>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient<IMagicGateway, MagicLink>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddPriceSources();

        services.AddSingleton(new LagerOptions
        {
            KolofonFromKr = int.TryParse(cfg["Lager:KolofonFromKr"], out var kr) ? kr : Kolofon.DefaultFromKr,
            SalgPublicUrl = cfg["Lager:SalgPublicUrl"] is { Length: > 0 } salg ? salg : "https://salg.bogshoppen.dk",
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
