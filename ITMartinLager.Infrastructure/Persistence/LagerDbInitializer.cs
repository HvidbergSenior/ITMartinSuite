using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Infrastructure.Persistence;

// Builds a new lager.db, and brings an old one up to date: EnsureCreated never changes an existing database,
// so tables and columns added later are added here.
public static class LagerDbInitializer
{
    public static void Upgrade(LagerDb db)
    {
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        // EnsureCreated never changes an existing database, so columns added later are added here.
        var conn = db.Database.GetDbConnection();
        conn.Open();
        // Tables added later (EnsureCreated only builds a brand-new database).
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "BookChecks" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BookChecks" PRIMARY KEY AUTOINCREMENT,
                "Title" TEXT NOT NULL DEFAULT '', "Author" TEXT NOT NULL DEFAULT '', "Year" INTEGER NULL,
                "Condition" TEXT NOT NULL DEFAULT '', "Isbn" TEXT NOT NULL DEFAULT '', "Quantity" INTEGER NOT NULL DEFAULT 1,
                "PhotoId" INTEGER NULL, "Verdict" TEXT NOT NULL DEFAULT '', "Low" INTEGER NULL, "High" INTEGER NULL,
                "Demand" TEXT NOT NULL DEFAULT '', "SellAt" TEXT NOT NULL DEFAULT '', "Reason" TEXT NOT NULL DEFAULT '',
                "Dba" TEXT NOT NULL DEFAULT '', "AbeBooks" TEXT NOT NULL DEFAULT '', "AmazonDe" TEXT NOT NULL DEFAULT '',
                "BoxCode" TEXT NOT NULL DEFAULT '', "Search" TEXT NOT NULL DEFAULT '', "CheckedAt" TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS "IX_BookChecks_Search" ON "BookChecks" ("Search");
            CREATE INDEX IF NOT EXISTS "IX_BookChecks_CheckedAt" ON "BookChecks" ("CheckedAt");
            CREATE TABLE IF NOT EXISTS "PriceChecks" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PriceChecks" PRIMARY KEY AUTOINCREMENT,
                "ItemId" INTEGER NOT NULL, "Source" TEXT NOT NULL DEFAULT '', "IsNew" INTEGER NOT NULL DEFAULT 0,
                "Ok" INTEGER NOT NULL DEFAULT 0, "Count" INTEGER NOT NULL DEFAULT 0, "Low" INTEGER NULL, "Median" INTEGER NULL,
                "High" INTEGER NULL, "Wild" INTEGER NULL, "Ambiguous" INTEGER NOT NULL DEFAULT 0,
                "SearchUrl" TEXT NOT NULL DEFAULT '', "CheckedAt" TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS "IX_PriceChecks_ItemId" ON "PriceChecks" ("ItemId");
            CREATE TABLE IF NOT EXISTS "PriceFinds" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PriceFinds" PRIMARY KEY AUTOINCREMENT,
                "ItemId" INTEGER NOT NULL, "Source" TEXT NOT NULL DEFAULT '', "IsNew" INTEGER NOT NULL DEFAULT 0,
                "Name" TEXT NOT NULL DEFAULT '', "Kr" INTEGER NOT NULL DEFAULT 0, "Original" TEXT NOT NULL DEFAULT '',
                "Url" TEXT NOT NULL DEFAULT '', "FoundAt" TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS "IX_PriceFinds_ItemId" ON "PriceFinds" ("ItemId");
            """);
        foreach (var col in new[] { "Nemos", "Proof", "Faraos", "Bokborsen" })
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('BookChecks') WHERE name = '{col}'";
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                db.Database.ExecuteSqlRaw($"ALTER TABLE BookChecks ADD COLUMN {col} TEXT NOT NULL DEFAULT ''");
        }
        foreach (var (col, type) in new[]
        {
            ("Interest", "TEXT NOT NULL DEFAULT ''"), ("ItemPhoto", "TEXT NOT NULL DEFAULT ''"),
            ("AuctionItemId", "TEXT NOT NULL DEFAULT ''"), ("AuctionCode", "TEXT NOT NULL DEFAULT ''"),
            ("SoldPrice", "REAL NULL"), ("SoldTo", "TEXT NOT NULL DEFAULT ''"), ("SoldAt", "TEXT NULL"),
            ("PriceHint", "REAL NULL"), ("MagicRef", "TEXT NOT NULL DEFAULT ''"), ("Barcode", "TEXT NOT NULL DEFAULT ''"),
            ("PricesAt", "TEXT NULL"), ("KolofonPhoto", "TEXT NOT NULL DEFAULT ''"), ("KolofonAt", "TEXT NULL"),
            ("Edition", "TEXT NOT NULL DEFAULT ''"), ("Publisher", "TEXT NOT NULL DEFAULT ''"), ("EditionHint", "TEXT NOT NULL DEFAULT ''"),
        })
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('Items') WHERE name = '{col}'";
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                db.Database.ExecuteSqlRaw($"ALTER TABLE Items ADD COLUMN {col} {type}");
        }
    }
}
