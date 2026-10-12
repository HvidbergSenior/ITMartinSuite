using ITMartinAuction.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinAuction.Infrastructure;

// Item photos as files (Auction:PhotosDir, default /app/data/photos - a volume in compose), served at /photos.
public sealed class FilePhotoStore(string dir) : IPhotoStore
{
    public string Dir => dir;

    public async Task<string> SaveAsync(Stream photo, string extension, CancellationToken ct = default)
    {
        Directory.CreateDirectory(dir);
        var ext = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
        var full = Path.Combine(dir, $"{Guid.NewGuid()}{ext}");
        await using var fs = new FileStream(full, FileMode.Create);
        await photo.CopyToAsync(fs, ct);
        return full;
    }

    public string PublicUrl(string fullPath) => "/photos/" + Path.GetFileName(fullPath);
}

public static class DependencyInjection
{
    public static string PhotosDir(IConfiguration cfg) => cfg["Auction:PhotosDir"] ?? "/app/data/photos";

    public static IServiceCollection AddAuctionInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var db = cfg.GetConnectionString("AuctionDb") ?? "Data Source=/app/db/auction.db";
        services.AddDbContext<AuctionDbContext>(o => o.UseSqlite(db));
        services.AddScoped<IAuctionStore, AuctionStore>();
        services.AddSingleton<IPhotoStore>(new FilePhotoStore(PhotosDir(cfg)));
        return services;
    }

    /// <summary>Creates or upgrades auction.db (and seeds the demo when Auction:SeedDemoData=true) - once at start.</summary>
    public static async Task UpgradeAuctionDatabaseAsync(this IServiceProvider services, IConfiguration cfg)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();
        await AuctionSchema.UpgradeAsync(db);
        if (cfg.GetValue<bool>("Auction:SeedDemoData")) await DemoSeeder.SeedAsync(db);
    }
}
