using Microsoft.EntityFrameworkCore;

namespace ITMartinVareinfo.Infrastructure.Persistence;

public sealed class VareinfoDb(DbContextOptions<VareinfoDb> options) : DbContext(options)
{
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Scan> Scans => Set<Scan>();
    public DbSet<Helper> Helpers => Set<Helper>();
    public DbSet<BoxClaim> BoxClaims => Set<BoxClaim>();
    public DbSet<WorkSession> Sessions => Set<WorkSession>();
    public DbSet<SessionBox> SessionBoxes => Set<SessionBox>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Photo>().HasIndex(x => x.BoxCode);
        b.Entity<Photo>().HasIndex(x => new { x.Ready, x.SentAt });
        b.Entity<Scan>().HasIndex(x => x.BoxCode);
        b.Entity<Scan>().HasIndex(x => x.SentAt);
        b.Entity<Helper>().HasIndex(x => x.Code).IsUnique();
        b.Entity<BoxClaim>().HasKey(x => x.BoxCode);
        b.Entity<BoxClaim>().HasIndex(x => x.HelperId);
        b.Entity<WorkSession>().HasMany(s => s.Boxes).WithOne().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<WorkSession>().Ignore(s => s.Open);
        b.Entity<SessionBox>().Ignore(x => x.Number).Ignore(x => x.Title);
        b.Entity<SessionBox>().HasIndex(x => x.BoxCode);
    }
}

public static class VareinfoDbInitializer
{
    public static void Upgrade(VareinfoDb db)
    {
        // The reading app before 2026-10-20 had other tables (Photos with Waiting/ReadWanted, KolofonShots, AiDays).
        // They are kept under an Old_ name, never deleted, and the capture tables are built new.
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) conn.Open();
        if (Columns(conn, "Photos").Count > 0 && !Columns(conn, "Photos").Contains("RawPath"))
            foreach (var table in new[] { "Photos", "Scans", "KolofonShots", "AiDays" })
                if (Columns(conn, table).Count > 0) Exec(conn, $"""ALTER TABLE "{table}" RENAME TO "Old_{table}_{DateTime.UtcNow:yyyyMMdd}" """);
        // EnsureCreated does nothing when any table exists, so the tables come from the model's own script - on every
        // start, IF NOT EXISTS: a table added later (Sessions, 2026-10-26) is made in the live file too.
        foreach (var statement in db.Database.GenerateCreateScript().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            Exec(conn, statement.Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ")
                .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS "));
        // Columns added after a table was made (IF NOT EXISTS does not add them).
        AddColumn(conn, "SessionBoxes", "HelperId", "INTEGER NULL");          // 2026-10-26 box per helper
        AddColumn(conn, "SessionBoxes", "HelperName", "TEXT NOT NULL DEFAULT ''");
        // Many phones at once: readers never wait for the writer.
        Exec(conn, "PRAGMA journal_mode=WAL;");
        if (opened) conn.Close();
    }

    private static HashSet<string> Columns(System.Data.Common.DbConnection conn, string table)
    {
        var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""PRAGMA table_info("{table}")""";
        using var r = cmd.ExecuteReader();
        while (r.Read()) have.Add(r.GetString(1));
        return have;
    }

    private static void AddColumn(System.Data.Common.DbConnection conn, string table, string column, string type)
    {
        if (Columns(conn, table) is { Count: > 0 } have && !have.Contains(column)) Exec(conn, $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type}");
    }

    private static void Exec(System.Data.Common.DbConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
