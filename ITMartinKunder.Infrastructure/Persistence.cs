using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinKunder.Infrastructure;

public sealed class KunderDb(DbContextOptions<KunderDb> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<CustomerEvent> Events => Set<CustomerEvent>();
    public DbSet<FreeApp> FreeApps => Set<FreeApp>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Customer>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Customer>().HasIndex(x => x.UnsubscribeToken).IsUnique();
        b.Entity<Customer>().HasIndex(x => x.Code);
        b.Entity<Customer>().Ignore(x => x.Active).Ignore(x => x.Apps);
        b.Entity<Customer>().HasMany(x => x.Consents).WithOne().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Customer>().HasMany(x => x.Events).WithOne().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CustomerEvent>().HasIndex(x => new { x.App, x.Ref });
    }
}

// A short-lived DbContext per call (Blazor Server keeps scoped services for the whole visit).
internal sealed class CustomerRepository(IDbContextFactory<KunderDb> dbf) : ICustomerRepository
{
    private static IQueryable<Customer> Whole(KunderDb db) => db.Customers.Include(c => c.Consents).Include(c => c.Events);

    public Task<Customer?> ByEmailAsync(string email, CancellationToken ct) => One(db => Whole(db).FirstOrDefaultAsync(c => c.Email == email, ct));
    public Task<Customer?> ByTokenAsync(string token, CancellationToken ct) => One(db => Whole(db).FirstOrDefaultAsync(c => c.UnsubscribeToken == token, ct));
    public Task<Customer?> GetAsync(int id, CancellationToken ct) => One(db => Whole(db).FirstOrDefaultAsync(c => c.Id == id, ct));
    public Task<Customer?> ByCodeAsync(string code, CancellationToken ct) => One(db => Whole(db).FirstOrDefaultAsync(c => c.Code == code, ct));

    public async Task<bool> CodeTakenAsync(string code, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.Customers.AnyAsync(c => c.Code == code, ct);
    }

    public async Task<List<Customer>> AllAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await Whole(db).AsNoTracking().AsSplitQuery().ToListAsync(ct);
    }

    public async Task SaveAsync(Customer customer, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        if (customer.Id == 0) db.Customers.Add(customer);
        else
        {
            // Loaded in another context: attach the graph, new consents/events (Id 0) are added, the rest updated.
            db.Customers.Update(customer);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        if (await Whole(db).FirstOrDefaultAsync(c => c.Id == id, ct) is { } c) { db.Customers.Remove(c); await db.SaveChangesAsync(ct); }
    }

    private async Task<Customer?> One(Func<KunderDb, Task<Customer?>> query)
    {
        await using var db = await dbf.CreateDbContextAsync();
        var c = await query(db);
        if (c is not null) db.Entry(c).State = EntityState.Detached;
        return c;
    }
}

internal sealed class FreeAppRepository(IDbContextFactory<KunderDb> dbf) : IFreeAppRepository
{
    public async Task<List<FreeApp>> AllAsync(CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        return await db.FreeApps.AsNoTracking().ToListAsync(ct);
    }

    public async Task SaveAsync(FreeApp app, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        if (app.Id == 0) db.FreeApps.Add(app); else db.FreeApps.Update(app);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await using var db = await dbf.CreateDbContextAsync(ct);
        await db.FreeApps.Where(a => a.Id == id).ExecuteDeleteAsync(ct);
    }
}

public static class DependencyInjection
{
    /// <summary>The data folder: kunder.db, keys/ (Kunder__DataDir, default /app/data in the container).</summary>
    public static string DataDir(IConfiguration cfg) => cfg["Kunder:DataDir"] ?? "/app/data";

    public static IServiceCollection AddKunderInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var dataDir = DataDir(cfg);
        Directory.CreateDirectory(dataDir);
        services.AddDbContextFactory<KunderDb>(o => o.UseSqlite($"Data Source={Path.Combine(dataDir, "kunder.db")};Default Timeout=30"));
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IFreeAppRepository, FreeAppRepository>();
        services.AddSingleton<IMailSender, SmtpMailSender>();
        return services;
    }

    /// <summary>Creates or upgrades kunder.db - call once at start. The first time: the free apps the user named.</summary>
    public static void UpgradeKunderDatabase(this IServiceProvider services)
    {
        using var db = services.GetRequiredService<IDbContextFactory<KunderDb>>().CreateDbContext();
        Upgrade(db);
    }

    internal static void Upgrade(KunderDb db)
    {
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) conn.Open();
        // EnsureCreated does nothing when any table exists, so the tables come from the model's own script.
        foreach (var statement in db.Database.GenerateCreateScript().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = statement.Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ")
                .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ");
            cmd.ExecuteNonQuery();
        }
        using (var wal = conn.CreateCommand()) { wal.CommandText = "PRAGMA journal_mode=WAL;"; wal.ExecuteNonQuery(); }
        // Columns that came later (CREATE TABLE IF NOT EXISTS leaves an existing table alone).
        if (AddColumn(conn, "FreeApps", "NeedsCode", "INTEGER NOT NULL DEFAULT 1"))
            Run(conn, "UPDATE \"FreeApps\" SET \"NeedsCode\" = 0 WHERE \"Name\" = 'Temperatur'");   // user 2026-10-21: Temperatur stays open
        // The first codes had letters; the kundenummer is digits (user 2026-10-21) - none had been sent to anyone yet.
        foreach (var id in Ids(conn, "SELECT \"Id\" FROM \"Customers\" WHERE \"Code\" <> '' AND \"Code\" GLOB '*[^0-9]*'"))
        {
            string code;
            do code = Customer.NewCode(); while (Ids(conn, $"SELECT \"Id\" FROM \"Customers\" WHERE \"Code\" = '{code}'").Count > 0);
            Run(conn, $"UPDATE \"Customers\" SET \"Code\" = '{code}' WHERE \"Id\" = {id}");
        }
        if (!db.FreeApps.Any()) { db.FreeApps.AddRange(FreeApp.Start()); db.SaveChanges(); }
        if (opened) conn.Close();
    }

    /// <returns>True when the column was added now.</returns>
    private static bool AddColumn(System.Data.Common.DbConnection conn, string table, string column, string type)
    {
        if (Ids(conn, $"SELECT 1 FROM pragma_table_info('{table}') WHERE name = '{column}'").Count > 0) return false;
        Run(conn, $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type}");
        return true;
    }

    private static void Run(System.Data.Common.DbConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static List<long> Ids(System.Data.Common.DbConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var ids = new List<long>();
        while (r.Read()) ids.Add(r.GetInt64(0));
        return ids;
    }
}
