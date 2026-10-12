using ITMartinTjek.Application;
using ITMartinTjek.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinTjek.Infrastructure;

// A short-lived DbContext per call: the use cases are singletons and Blazor circuits live long.
public sealed class EfSnapshotStore(IDbContextFactory<TjekDbContext> factory) : ISnapshotStore
{
    public async Task AddAsync(Snapshot s)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Snapshots.Add(s);
        await db.SaveChangesAsync();
    }

    public async Task<List<string>> DevicesAsync(string ownerKey)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Snapshots.Where(s => s.OwnerKey == ownerKey).Select(s => s.Device).Distinct().OrderBy(d => d).ToListAsync();
    }

    public async Task<List<Snapshot>> RunsAsync(string ownerKey, string device, int take)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Snapshots.AsNoTracking().Where(s => s.Device == device && s.OwnerKey == ownerKey)
            .OrderByDescending(s => s.At).Take(take).ToListAsync();
    }

    public async Task<Snapshot?> LatestAsync(string ownerKey, string device, string kind)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Snapshots.AsNoTracking().Where(s => s.Kind == kind && s.OwnerKey == ownerKey && s.Device == device)
            .OrderByDescending(s => s.At).FirstOrDefaultAsync();
    }

    public async Task DeleteAsync(string ownerKey, int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Snapshots.Where(s => s.Id == id && s.OwnerKey == ownerKey).ExecuteDeleteAsync();
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddTjekInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<TjekDbContext>(o => o.UseSqlite(connectionString));
        services.AddSingleton<ISnapshotStore, EfSnapshotStore>();
        services.AddSingleton<IAdvisor, ClaudeAdvisor>();
        return services;
    }

    /// <summary>Creates the database, and adds columns that came later to an older one.</summary>
    public static async Task UpgradeTjekDatabaseAsync(this IServiceProvider services)
    {
        var factory = services.GetRequiredService<IDbContextFactory<TjekDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Snapshots\" ADD COLUMN \"OwnerKey\" TEXT NOT NULL DEFAULT '';"); } catch { /* already there */ }
    }
}
