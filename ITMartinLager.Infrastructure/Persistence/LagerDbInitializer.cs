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
            CREATE TABLE IF NOT EXISTS "BoxShots" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BoxShots" PRIMARY KEY AUTOINCREMENT,
                "VareinfoId" INTEGER NOT NULL, "BoxCode" TEXT NOT NULL, "Helper" TEXT NOT NULL, "Tiles" INTEGER NOT NULL,
                "TakenAt" TEXT NOT NULL, "Wanted" INTEGER NOT NULL, "ReadAt" TEXT NULL, "Found" INTEGER NOT NULL);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_BoxShots_VareinfoId" ON "BoxShots" ("VareinfoId");
            CREATE INDEX IF NOT EXISTS "IX_BoxShots_BoxCode" ON "BoxShots" ("BoxCode");
            CREATE TABLE IF NOT EXISTS "BoxScans" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BoxScans" PRIMARY KEY AUTOINCREMENT,
                "VareinfoId" INTEGER NOT NULL, "BoxCode" TEXT NOT NULL, "Helper" TEXT NOT NULL, "Code" TEXT NOT NULL,
                "Quantity" INTEGER NOT NULL, "TakenAt" TEXT NOT NULL, "Wanted" INTEGER NOT NULL, "ReadAt" TEXT NULL, "Known" INTEGER NOT NULL);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_BoxScans_VareinfoId" ON "BoxScans" ("VareinfoId");
            CREATE INDEX IF NOT EXISTS "IX_BoxScans_BoxCode" ON "BoxScans" ("BoxCode");
            CREATE TABLE IF NOT EXISTS "OrderLines" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_OrderLines" PRIMARY KEY AUTOINCREMENT,
                "OrderId" INTEGER NOT NULL, "ItemId" INTEGER NOT NULL, "Line" TEXT NOT NULL DEFAULT '', "Kind" TEXT NOT NULL DEFAULT '',
                "ListKr" INTEGER NOT NULL DEFAULT 0, "Kr" INTEGER NOT NULL DEFAULT 0, "Offer" TEXT NOT NULL DEFAULT '',
                CONSTRAINT "FK_OrderLines_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE CASCADE);
            CREATE INDEX IF NOT EXISTS "IX_OrderLines_OrderId" ON "OrderLines" ("OrderId");
            CREATE INDEX IF NOT EXISTS "IX_OrderLines_ItemId" ON "OrderLines" ("ItemId");
            CREATE TABLE IF NOT EXISTS "Campaigns" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Campaigns" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL DEFAULT '', "Kind" TEXT NOT NULL DEFAULT '', "ProductType" TEXT NOT NULL DEFAULT '',
                "Weekday" INTEGER NULL, "DayOfMonth" INTEGER NULL, "Percent" INTEGER NOT NULL DEFAULT 0, "Count" INTEGER NOT NULL DEFAULT 0,
                "BundleKr" INTEGER NOT NULL DEFAULT 0, "Active" INTEGER NOT NULL DEFAULT 0);
            """);
        // Orders before baskets and the sale steps (2026-10-20): Channel/PackedAt/DoneAt/Tracking added; each old order
        // gets its one line; "Handled" (sent or picked up) becomes the status it meant.
        foreach (var (col, type) in new[] { ("Channel", "TEXT NOT NULL DEFAULT 'Køb nu'"), ("PackedAt", "TEXT NULL"), ("DoneAt", "TEXT NULL"),
                     ("Tracking", "TEXT NOT NULL DEFAULT ''") })
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('Orders') WHERE name = '{col}'";
            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                db.Database.ExecuteSqlRaw($"ALTER TABLE Orders ADD COLUMN {col} {type}");
        }
        db.Database.ExecuteSqlRaw("""
            INSERT INTO "OrderLines" ("OrderId", "ItemId", "Line", "Kind", "ListKr", "Kr", "Offer")
            SELECT o."Id", o."ItemId", o."Line", '', o."Kr", o."Kr", '' FROM "Orders" o
            WHERE NOT EXISTS (SELECT 1 FROM "OrderLines" l WHERE l."OrderId" = o."Id");
            """);
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Orders') WHERE name = 'Handled'";
            if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                db.Database.ExecuteSqlRaw("""
                    UPDATE "Orders" SET "Status" = CASE WHEN "Delivery" LIKE 'Afhent%' THEN 'Afhentet' ELSE 'Sendt' END,
                        "DoneAt" = "PaidAt", "Handled" = 0
                    WHERE "Status" = 'Betalt' AND "Handled" = 1;
                    """);
        }
        // The user's day offers, switched off until a person turns them on in Tilbud.
        if (!db.Campaigns.Any())
        {
            db.Campaigns.AddRange(Campaign.Examples());
            db.SaveChanges();
        }
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
            ("SimilarHint", "REAL NULL"), ("FeaturedAt", "TEXT NULL"),
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
