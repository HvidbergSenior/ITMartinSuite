using ITMartinVareinfo.Infrastructure.External;
using ITMartinVareinfo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinVareinfo.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The data folder: vareinfo.db, photos/ (Vareinfo__DataDir, default /app/data in the container).</summary>
    public static string DataDir(IConfiguration cfg) => cfg["Vareinfo:DataDir"] ?? "/app/data";

    public static IServiceCollection AddVareinfoInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var dataDir = DataDir(cfg);
        Directory.CreateDirectory(Path.Combine(dataDir, "photos"));
        services.AddDbContextFactory<VareinfoDb>(o => o.UseSqlite($"Data Source={Path.Combine(dataDir, "vareinfo.db")}"));
        services.AddScoped<IPhotoRepository, PhotoRepository>();
        services.AddScoped<IKolofonShotRepository, KolofonShotRepository>();

        services.AddSingleton<IPhotoStore>(new FilePhotoStore(dataDir));
        services.AddSingleton<ICoverReader, ClaudeCoverReader>();
        services.AddHttpClient<IBarcodeLookup, BarcodeLookup>(c => c.Timeout = TimeSpan.FromSeconds(12));
        services.AddHttpClient<ILagerGateway, LagerLink>(c => c.Timeout = TimeSpan.FromSeconds(30));

        services.AddSingleton(new VareinfoOptions { AutoInterest = cfg.GetValue<bool>("Vareinfo:AutoInterest") });
        return services;
    }

    /// <summary>Creates vareinfo.db, and at the first start takes over Lager's photos (Vareinfo__ImportLagerDb).</summary>
    public static int UpgradeVareinfoDatabase(this IServiceProvider services, IConfiguration cfg)
    {
        using var db = services.GetRequiredService<IDbContextFactory<VareinfoDb>>().CreateDbContext();
        VareinfoDbInitializer.Upgrade(db);
        return cfg["Vareinfo:ImportLagerDb"] is { Length: > 0 } file ? VareinfoDbInitializer.ImportFromLager(db, file) : 0;
    }
}
