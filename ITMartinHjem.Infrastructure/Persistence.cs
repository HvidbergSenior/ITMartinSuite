using ITMartinHjem.Application;
using ITMartinHjem.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinHjem.Infrastructure;

// Only the tables that have moved out of the flat Server live here (so far: PublicQuestions).
public sealed class HjemQuestionsDb(DbContextOptions<HjemQuestionsDb> options) : DbContext(options)
{
    public DbSet<PublicQuestion> PublicQuestions => Set<PublicQuestion>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        var q = b.Entity<PublicQuestion>();
        q.ToTable("PublicQuestions");
        q.HasKey(x => x.Id);
        q.Property(x => x.Question).IsRequired();
        q.Property(x => x.Answer).IsRequired();
        q.Property(x => x.Topic).IsRequired();
        q.Property(x => x.Site).IsRequired();
    }

    // hjem.db already exists (EnsureCreated by the Server), so this table is added by hand - idempotent.
    public static async Task EnsureTablesAsync(HjemQuestionsDb db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PublicQuestions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PublicQuestions" PRIMARY KEY AUTOINCREMENT,
                "Question" TEXT NOT NULL, "Answer" TEXT NOT NULL, "Topic" TEXT NOT NULL,
                "PublishedAt" TEXT NOT NULL, "SourceThreadId" INTEGER NULL,
                "Site" TEXT NOT NULL DEFAULT 'itmartin')
            """);
        // Added 2026-10-12 (questions per site) - a table made before that gets the column here.
        try { await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "PublicQuestions" ADD COLUMN "Site" TEXT NOT NULL DEFAULT 'itmartin'"""); }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase)) { }
    }
}

// A short-lived DbContext per call: Blazor Server keeps scoped services for the whole visit, so a shared
// context would keep showing the Q&As as they were when the page opened.
public sealed class PublicQuestionStore(IDbContextFactory<HjemQuestionsDb> dbf) : IPublicQuestionStore
{
    public async Task<List<PublicQuestion>> AllAsync(CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.PublicQuestions.AsNoTracking().ToListAsync(ct);
    }

    public async Task AddAsync(PublicQuestion q, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        db.PublicQuestions.Add(q);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdateAsync(int id, Action<PublicQuestion> change, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        var q = await db.PublicQuestions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (q is null) return false;
        change(q);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.PublicQuestions.Where(q => q.Id == id).ExecuteDeleteAsync(ct);
    }
}

public static class DependencyInjection
{
    /// <param name="stemApi">Stem (poll-web) base address, reached over the NAS docker network.</param>
    public static IServiceCollection AddHjemInfrastructure(this IServiceCollection services, string connectionString, string? stemApi = null)
    {
        services.AddHttpClient<StemGateway>(c =>
        {
            c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(stemApi) ? "http://poll-web:8080" : stemApi);
            c.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddTransient<IPollGateway>(sp => sp.GetRequiredService<StemGateway>());
        services.AddTransient<ICustomerGateway>(sp => sp.GetRequiredService<StemGateway>());
        services.AddDbContextFactory<HjemQuestionsDb>(o => o.UseSqlite(connectionString));
        services.AddSingleton<IPublicQuestionStore, PublicQuestionStore>();
        return services;
    }

    /// <summary>Adds the moved-out tables to hjem.db - once at start.</summary>
    public static async Task UpgradeHjemTablesAsync(this IServiceProvider services)
    {
        await using var db = await services.GetRequiredService<IDbContextFactory<HjemQuestionsDb>>().CreateDbContextAsync();
        await HjemQuestionsDb.EnsureTablesAsync(db);
    }
}
