using ITMartinForloebet.Application;
using ITMartinForloebet.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinForloebet.Infrastructure;

// A short-lived DbContext per call: the use case is a singleton and Blazor circuits live long.
public sealed class EfForloebStore(IDbContextFactory<ForloebetDbContext> factory) : IForloebStore
{
    public async Task<Forloeb?> GetAsync(string slug)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Forloeb.Include(f => f.Trin).Include(f => f.Dokumenter).AsNoTracking().FirstOrDefaultAsync(f => f.Slug == slug);
    }

    public async Task<Forloeb?> GetByIdAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Forloeb.Include(f => f.Trin).AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<List<Forloeb>> DelteAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Forloeb.Include(f => f.Trin).AsNoTracking()
            .Where(f => f.Delt)
            .OrderBy(f => f.ClosedAt != null).ThenByDescending(f => f.UpdatedAt)
            .ToListAsync();
    }

    public async Task<List<Forloeb>> DelteLukkedeAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Forloeb.AsNoTracking().Where(x => x.Delt && x.ClosedAt != null).ToListAsync();
    }

    public async Task AddAsync(Forloeb f)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Forloeb.Add(f);
        await db.SaveChangesAsync();
    }

    public async Task<bool> ChangeAsync(string slug, Func<Forloeb, bool> change, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync();
        var f = await db.Forloeb.Include(x => x.Trin).Include(x => x.Dokumenter).FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (f is null || !change(f)) return false;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ChangeAsync(int id, Func<Forloeb, bool> change)
    {
        await using var db = await factory.CreateDbContextAsync();
        var f = await db.Forloeb.Include(x => x.Trin).Include(x => x.Dokumenter).FirstOrDefaultAsync(x => x.Id == id);
        if (f is null || !change(f)) return false;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<SiteState> SiteStateAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.SiteState.AsNoTracking().FirstOrDefaultAsync() ?? new SiteState();
    }

    public async Task SaveSiteStateAsync(SiteState s)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (s.Id == 0) db.SiteState.Add(s); else db.SiteState.Update(s);
        await db.SaveChangesAsync();
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddForloebetInfrastructure(this IServiceCollection services, string connectionString, string docsDir, string knowledgeDir)
    {
        services.AddDbContextFactory<ForloebetDbContext>(o => o.UseSqlite(connectionString));
        services.AddSingleton<IForloebStore, EfForloebStore>();
        services.AddSingleton<IDocumentFiles>(new FileDocumentStore(docsDir));
        services.AddSingleton(sp => new Knowledge(knowledgeDir, sp.GetRequiredService<ILogger<Knowledge>>()));
        services.AddSingleton<IForloebAi, ClaudeForloebAi>();
        return services;
    }

    /// <summary>Creates the database and, when it is empty, the owner's own forløb (no AI call).</summary>
    public static async Task UpgradeForloebetDatabaseAsync(this IServiceProvider services)
    {
        var factory = services.GetRequiredService<IDbContextFactory<ForloebetDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        await Seed.EnsureAsync(db);
    }
}
