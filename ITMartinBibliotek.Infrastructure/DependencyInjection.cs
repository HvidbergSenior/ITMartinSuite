using ITMartinBibliotek.Application;
using ITMartinBibliotek.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITMartinBibliotek.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBibliotekInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var dbPath = config.GetConnectionString("BibliotekDb") ?? "Data Source=/app/data/bibliotek.db";
        services.AddDbContextFactory<BibliotekDbContext>(o => o.UseSqlite(dbPath));

        services.AddSingleton(CustomerConfig.Load(config));
        services.AddSingleton<IMediaRepository, EfMediaRepository>();
        services.AddSingleton<ISettings, SettingsStore>();
        services.AddSingleton<IJellyfin, JellyfinClient>();
        services.AddSingleton<IMetadataLookup, MetadataLookup>();
        services.AddSingleton<IRipStatus, RipStatusStore>();
        services.AddHostedService<CustomerSyncService>();

        // Audio streams run as long as a track plays, so they get no overall timeout.
        services.AddHttpClient("jellyfin-stream", c => c.Timeout = Timeout.InfiniteTimeSpan);

        // MusicBrainz and Discogs reject requests without a descriptive User-Agent.
        const string userAgent = "ITMartinBibliotek/1.0 (hvidbergsenior@gmail.com)";
        foreach (var name in new[] { "jellyfin", "musicbrainz", "discogs", "upc", "tmdb" })
        {
            services.AddHttpClient(name, c =>
            {
                c.Timeout = TimeSpan.FromSeconds(15);
                c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
                c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            });
        }
        return services;
    }

    // EnsureCreated never alters an existing file, so columns added later are added by hand.
    public static void EnsureBibliotekSchema(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        using var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BibliotekDbContext>>().CreateDbContext();
        db.Database.EnsureCreated();
        var conn = db.Database.GetDbConnection();
        conn.Open();
        bool Has(string column)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Items') WHERE name = $c";
            var p = cmd.CreateParameter();
            p.ParameterName = "$c";
            p.Value = column;
            cmd.Parameters.Add(p);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
        if (!Has("Collection")) db.Database.ExecuteSqlRaw("ALTER TABLE Items ADD COLUMN Collection TEXT NOT NULL DEFAULT ''");
        if (!Has("Owner")) db.Database.ExecuteSqlRaw("ALTER TABLE Items ADD COLUMN Owner TEXT NOT NULL DEFAULT ''");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Items_Owner ON Items (Owner)");
    }
}

// Bibliotek__AdminPin plus Bibliotek__Kunder__0__Key / __Navn / __Pin / __Bibliotek / __Upload ...
public static class CustomerConfig
{
    public static Customers Load(IConfiguration cfg) => new(
        cfg["Bibliotek:AdminPin"] ?? "bibliotek2026",
        cfg.GetSection("Bibliotek:Kunder").Get<List<Customer>>() ?? []);
}

// Customers upload CDs through the upload box and expect them in the app without asking
// anyone to press Synkroniser: every 20 minutes their libraries are rescanned and synced.
public sealed class CustomerSyncService(Customers customers, IJellyfin jellyfin, JellyfinSync sync, ILogger<CustomerSyncService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (customers.All.Count == 0) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(20));
        do
        {
            try
            {
                // Jellyfin scans in the background, so new albums land in the sync after this one.
                foreach (var c in customers.All) await jellyfin.RefreshLibraryAsync(c.Bibliotek);
                await sync.RunAsync();
            }
            catch (Exception ex) { log.LogWarning(ex, "Customer sync failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
