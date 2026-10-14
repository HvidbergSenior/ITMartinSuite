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
            CREATE TABLE IF NOT EXISTS "GoldEntries" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_GoldEntries" PRIMARY KEY AUTOINCREMENT,
                "Category" TEXT NOT NULL DEFAULT '', "Kind" TEXT NOT NULL DEFAULT '', "Number" TEXT NOT NULL DEFAULT '',
                "Year" INTEGER NULL, "Name" TEXT NOT NULL DEFAULT '', "Kr" INTEGER NOT NULL DEFAULT 0, "Listings" INTEGER NOT NULL DEFAULT 0,
                "Source" TEXT NOT NULL DEFAULT '', "Url" TEXT NOT NULL DEFAULT '', "SeenAt" TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS "IX_GoldEntries_Category" ON "GoldEntries" ("Category");
            CREATE TABLE IF NOT EXISTS "Orders" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Orders" PRIMARY KEY AUTOINCREMENT,
                "Token" TEXT NOT NULL, "ItemId" INTEGER NOT NULL, "Line" TEXT NOT NULL DEFAULT '', "Kr" INTEGER NOT NULL DEFAULT 0,
                "Status" TEXT NOT NULL DEFAULT '', "CheckoutId" TEXT NOT NULL DEFAULT '', "CreatedAt" TEXT NOT NULL,
                "ExpiresAt" TEXT NOT NULL, "PaidAt" TEXT NULL, "PaidKr" INTEGER NOT NULL DEFAULT 0,
                "Email" TEXT NOT NULL DEFAULT '', "Name" TEXT NOT NULL DEFAULT '', "Phone" TEXT NOT NULL DEFAULT '',
                "Address" TEXT NOT NULL DEFAULT '', "Delivery" TEXT NOT NULL DEFAULT '', "Handled" INTEGER NOT NULL DEFAULT 0);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Orders_Token" ON "Orders" ("Token");
            CREATE INDEX IF NOT EXISTS "IX_Orders_Status" ON "Orders" ("Status");
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
            ("SimilarHint", "REAL NULL"),
        })
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('Items') WHERE name = '{col}'";
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                db.Database.ExecuteSqlRaw($"ALTER TABLE Items ADD COLUMN {col} {type}");
        }
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('PriceFinds') WHERE name = 'Similar'";
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                db.Database.ExecuteSqlRaw("ALTER TABLE PriceFinds ADD COLUMN Similar INTEGER NOT NULL DEFAULT 0");
        }
    }
}
