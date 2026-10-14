using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinVareinfo.Infrastructure.Persistence;

public sealed class VareinfoDb(DbContextOptions<VareinfoDb> options) : DbContext(options)
{
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<KolofonShot> KolofonShots => Set<KolofonShot>();
    public DbSet<AiDay> AiDays => Set<AiDay>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AiDay>().HasKey(x => x.Day);
        b.Entity<KolofonShot>().HasIndex(x => x.ItemId);
        b.Entity<KolofonShot>().HasIndex(x => x.ReadAt);
    }
}

public static class VareinfoDbInitializer
{
    public static void Upgrade(VareinfoDb db)
    {
        db.Database.EnsureCreated();
        // Box photos (2026-10-15) - columns added to an existing database. Old photos were all read at once: Waiting = 0.
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) conn.Open();
        var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """PRAGMA table_info("Photos")""";
            using var r = cmd.ExecuteReader();
            while (r.Read()) have.Add(r.GetString(1));
        }
        foreach (var (col, type) in new[] { ("BoxCode", "TEXT NOT NULL DEFAULT ''"), ("Tiles", "INTEGER NOT NULL DEFAULT 0"),
                     ("Waiting", "INTEGER NOT NULL DEFAULT 0"), ("ReadWanted", "INTEGER NOT NULL DEFAULT 0") })
            if (!have.Contains(col))
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"""ALTER TABLE "Photos" ADD COLUMN "{col}" {type}""";
                    cmd.ExecuteNonQuery();
                }
        if (opened) conn.Close();
    }

    // One time, at the split (2026-10-12): the registration photos and kolofon photos Lager had are moved here WITH
    // their ids, so every item in Lager still points at its own photo. Lager's database is read, never written.
    public static int ImportFromLager(VareinfoDb db, string lagerDbFile)
    {
        if (!File.Exists(lagerDbFile) || db.Photos.Any() || db.KolofonShots.Any()) return 0;
        using var conn = new SqliteConnection($"Data Source={lagerDbFile};Mode=ReadOnly");
        conn.Open();
        var count = 0;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """SELECT "Id", "Path", "BoxId", "Found", "TakenAt" FROM "Photos" ORDER BY "Id" """;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                db.Photos.Add(new Photo
                {
                    Id = r.GetInt32(0), Path = r.GetString(1), BoxId = r.IsDBNull(2) ? null : r.GetInt32(2),
                    Found = r.GetInt32(3), TakenAt = DateTime.Parse(r.GetString(4)),
                });
                count++;
            }
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """SELECT "Id", "Title", "KolofonPhoto", "KolofonAt" FROM "Items" WHERE "KolofonPhoto" <> '' """;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                db.KolofonShots.Add(new KolofonShot
                {
                    ItemId = r.GetInt32(0), Title = r.GetString(1), Path = r.GetString(2),
                    ReadAt = r.IsDBNull(3) ? null : DateTime.Parse(r.GetString(3)), Result = r.IsDBNull(3) ? "" : "Læst i Lager før opdelingen",
                });
                count++;
            }
        }
        db.SaveChanges();
        return count;
    }
}
