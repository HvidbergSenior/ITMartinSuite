using ITMartinPolstrer.Application;
using ITMartinPolstrer.Infrastructure.External;
using ITMartinPolstrer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinPolstrer.Infrastructure;

public static class DependencyInjection
{
    /// <summary>SQLite (ConnectionStrings:PolstrerDb, default /app/data/polstrer.db), photo files (Polstrer:MediaRoot) and Claude (Claude:ApiKey).
    /// Used by both the phone app and the workshop board - they share one database and one media folder.</summary>
    public static IServiceCollection AddPolstrerInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var db = cfg.GetConnectionString("PolstrerDb") ?? "Data Source=/app/data/polstrer.db";
        services.AddDbContextFactory<PolstrerDbContext>(o => o.UseSqlite(db));
        services.AddSingleton<IPieceStore, EfPieceStore>();
        services.AddSingleton<FileMediaStore>();
        services.AddSingleton<IMediaStore>(sp => sp.GetRequiredService<FileMediaStore>());
        services.AddSingleton<IAiSuggester, ClaudeSuggester>();
        return services;
    }

    /// <summary>Creates or upgrades polstrer.db (EnsureCreated + the columns added later) - once at start.</summary>
    public static async Task UpgradePolstrerDatabaseAsync(this IServiceProvider services)
    {
        await using var db = await services.GetRequiredService<IDbContextFactory<PolstrerDbContext>>().CreateDbContextAsync();
        PolstrerSchema.Ensure(db);
    }

    /// <summary>Where the photos live on disk (the Server serves them at /media).</summary>
    public static string MediaRoot(this IServiceProvider services) => services.GetRequiredService<FileMediaStore>().Root;
}
