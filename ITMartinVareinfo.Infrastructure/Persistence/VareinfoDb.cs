using Microsoft.EntityFrameworkCore;

namespace ITMartinVareinfo.Infrastructure.Persistence;

public sealed class VareinfoDb(DbContextOptions<VareinfoDb> options) : DbContext(options)
{
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Scan> Scans => Set<Scan>();
    public DbSet<Helper> Helpers => Set<Helper>();
    public DbSet<WorkSession> Sessions => Set<WorkSession>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Photo>().HasIndex(x => x.SessionId);
        b.Entity<Photo>().HasIndex(x => new { x.Ready, x.SentAt });
        b.Entity<Scan>().HasIndex(x => x.SessionId);
        b.Entity<Scan>().HasIndex(x => x.SentAt);
        b.Entity<Helper>().HasIndex(x => x.Code).IsUnique();
        b.Entity<WorkSession>().Ignore(s => s.Open).Ignore(s => s.IsPractice).Ignore(s => s.Title);
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
        // Columns added after a table was made (CREATE TABLE IF NOT EXISTS does not add them) - before the script
        // below, whose indexes may use them. 2026-10-29 sessions instead of boxes; the box tables of 2026-10-26
        // (BoxClaims, SessionBoxes) are left in the file as they are.
        AddColumn(conn, "Photos", "SessionId", "INTEGER NOT NULL DEFAULT 0");
        AddColumn(conn, "Scans", "SessionId", "INTEGER NOT NULL DEFAULT 0");
        AddColumn(conn, "Sessions", "Number", "INTEGER NOT NULL DEFAULT 0");
        AddColumn(conn, "Sessions", "BoxCode", "TEXT NOT NULL DEFAULT ''");
        AddColumn(conn, "Sessions", "HelperId", "INTEGER NULL");
        AddColumn(conn, "Sessions", "HelperName", "TEXT NOT NULL DEFAULT ''");
        // EnsureCreated does nothing when any table exists, so the tables come from the model's own script - on every
        // start, IF NOT EXISTS: a table added later is made in the live file too.
        foreach (var statement in db.Database.GenerateCreateScript().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            Exec(conn, statement.Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ")
                .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS "));
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
