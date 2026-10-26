using Microsoft.EntityFrameworkCore;

namespace ITMartinSalg.Infrastructure.Persistence;

public sealed class SalgDb(DbContextOptions<SalgDb> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Interest> Interests => Set<Interest>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Order>().HasIndex(x => x.Token).IsUnique();
        b.Entity<Order>().HasIndex(x => x.Status);
        b.Entity<Order>().HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId);
        b.Entity<OrderLine>().HasIndex(x => x.ItemId);
        b.Entity<Interest>().HasIndex(x => new { x.Customer, x.ItemId }).IsUnique();
        b.Entity<Interest>().HasIndex(x => x.ItemId);
    }
}

public static class SalgDbInitializer
{
    /// <summary>Builds salg.db. At the first start the orders and day offers that Lager had (before 2026-10-21) are
    /// copied once from a copy of lager.db (importLagerDb, if it is there) - Lager's own file is never opened. Then the
    /// user's example offers, switched off, when there are none.</summary>
    public static void Upgrade(SalgDb db, string? importLagerDb = null)
    {
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) conn.Open();
        // EnsureCreated does nothing when any table exists, so the tables come from the model's own script.
        foreach (var statement in db.Database.GenerateCreateScript().Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            Exec(conn, statement.Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ")
                .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS "));
        Exec(conn, "PRAGMA journal_mode=WAL;");

        if (importLagerDb is not null && File.Exists(importLagerDb) && Count(conn, "Orders") == 0 && Count(conn, "Campaigns") == 0)
        {
            Exec(conn, $"ATTACH DATABASE '{importLagerDb.Replace("'", "''")}' AS old");
            using (var tx = conn.BeginTransaction())
            {
                // Paid orders: Lager already took those copies out of the stock itself.
                Exec(conn, """
                    INSERT INTO "Orders" ("Id", "Token", "ItemId", "Line", "Kr", "Status", "Channel", "CheckoutId", "CreatedAt", "ExpiresAt",
                        "PaidAt", "PaidKr", "Email", "Name", "Phone", "Address", "Delivery", "PackedAt", "DoneAt", "Tracking", "StockBooked")
                    SELECT "Id", "Token", "ItemId", "Line", "Kr", "Status", "Channel", "CheckoutId", "CreatedAt", "ExpiresAt",
                        "PaidAt", "PaidKr", "Email", "Name", "Phone", "Address", "Delivery", "PackedAt", "DoneAt", "Tracking",
                        CASE WHEN "Status" IN ('Betalt', 'Pakket', 'Sendt', 'Afhentet') THEN 1 ELSE 0 END
                    FROM old."Orders";
                    INSERT INTO "OrderLines" ("Id", "OrderId", "ItemId", "Line", "Kind", "Usage", "ListKr", "Kr", "Offer")
                    SELECT "Id", "OrderId", "ItemId", "Line", "Kind", "Usage", "ListKr", "Kr", "Offer" FROM old."OrderLines";
                    INSERT INTO "Campaigns" ("Id", "Name", "Kind", "ProductType", "Usage", "Weekday", "DayOfMonth", "Percent", "Count", "BundleKr", "Active")
                    SELECT "Id", "Name", "Kind", "ProductType", "Usage", "Weekday", "DayOfMonth", "Percent", "Count", "BundleKr", "Active" FROM old."Campaigns";
                    """, tx);
                tx.Commit();
            }
            Exec(conn, "DETACH DATABASE old");
        }

        if (Count(conn, "Campaigns") == 0)
        {
            db.Campaigns.AddRange(Campaign.Examples());
            db.SaveChanges();
        }
        if (opened) conn.Close();
    }

    private static long Count(System.Data.Common.DbConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""SELECT COUNT(*) FROM "{table}" """;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static void Exec(System.Data.Common.DbConnection conn, string sql, System.Data.Common.DbTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
