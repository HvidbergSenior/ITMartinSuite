using ITMartinKunder.Client;
using ITMartinVareinfo.Infrastructure.External;
using ITMartinVareinfo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinVareinfo.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The data folder: vareinfo.db, inbox/ (uploads), photos/ (Vareinfo__DataDir, default /app/data in the container).</summary>
    public static string DataDir(IConfiguration cfg) => cfg["Vareinfo:DataDir"] ?? "/app/data";

    public static IServiceCollection AddVareinfoInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var dataDir = DataDir(cfg);
        Directory.CreateDirectory(Path.Combine(dataDir, "photos"));
        Directory.CreateDirectory(Path.Combine(dataDir, "inbox"));
        // Default Timeout: a write waits up to 30 s for another phone's write instead of failing at once.
        services.AddDbContextFactory<VareinfoDb>(o => o.UseSqlite($"Data Source={Path.Combine(dataDir, "vareinfo.db")};Default Timeout=30"));
        services.AddScoped<IPhotoRepository, PhotoRepository>();
        services.AddScoped<IScanRepository, ScanRepository>();
        services.AddScoped<IHelperRepository, HelperRepository>();
        services.AddScoped<IBoxClaimRepository, BoxClaimRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddSingleton<IPhotoStore>(new FilePhotoStore(dataDir));
        // Helpers log in with their kundenummer (Kunder__Url / Kunder__ApiKey, user 2026-10-24).
        services.AddKunderClient(cfg, dataDir);
        services.AddScoped<IHelperNumbers, KunderHelperNumbers>();

        services.AddSingleton(new VareinfoOptions
        {
            AdminPin = cfg["Vareinfo:Pin"] ?? "",
            AdminName = cfg["Vareinfo:AdminName"] is { Length: > 0 } n ? n : "Martin",
            LagerPublicUrl = cfg["Vareinfo:LagerPublicUrl"] ?? "https://lager.bogshoppen.dk",
        });
        return services;
    }

    /// <summary>Creates or upgrades vareinfo.db - call once at start.</summary>
    public static void UpgradeVareinfoDatabase(this IServiceProvider services)
    {
        using var db = services.GetRequiredService<IDbContextFactory<VareinfoDb>>().CreateDbContext();
        VareinfoDbInitializer.Upgrade(db);
    }
}
