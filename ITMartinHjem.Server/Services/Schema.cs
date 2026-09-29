using ITMartinHjem.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinHjem.Server.Services;

// EnsureCreated never changes an existing database, so columns added later are put in here.
public static class Schema
{
    private static readonly (string Table, string Column, string Sql)[] Added =
    [
        ("Settings", "NowMediaName", "TEXT NOT NULL DEFAULT ''"),
        ("Settings", "NowMediaThumb", "TEXT NOT NULL DEFAULT ''"),
        ("Settings", "NowMediaType", "TEXT NOT NULL DEFAULT ''"),
        ("Settings", "NowLink", "TEXT NOT NULL DEFAULT ''"),
        ("Settings", "NowPhotos", "TEXT NOT NULL DEFAULT ''"),
        ("AppLinks", "FreeText", "TEXT NOT NULL DEFAULT ''"),
        ("AppLinks", "PaidText", "TEXT NOT NULL DEFAULT ''"),
        ("AppLinks", "Awake", "INTEGER NOT NULL DEFAULT 0"),
    ];

    public static async Task EnsureColumnsAsync(HjemDb db)
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using (var create = conn.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE IF NOT EXISTS "Pages" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Pages" PRIMARY KEY AUTOINCREMENT,
                    "Slug" TEXT NOT NULL, "Title" TEXT NOT NULL, "Body" TEXT NOT NULL,
                    "InMenu" INTEGER NOT NULL, "Sort" INTEGER NOT NULL, "UpdatedAt" TEXT NOT NULL);
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Pages_Slug" ON "Pages" ("Slug");
                CREATE TABLE IF NOT EXISTS "AppLinks" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_AppLinks" PRIMARY KEY AUTOINCREMENT,
                    "Name" TEXT NOT NULL, "Icon" TEXT NOT NULL, "Url" TEXT NOT NULL, "Description" TEXT NOT NULL,
                    "Show" INTEGER NOT NULL, "Sort" INTEGER NOT NULL);
                """;
            await create.ExecuteNonQueryAsync();
        }
        foreach (var (table, column, sql) in Added)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'";
            if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0) continue;
            cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {sql}";
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
