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
    ];

    public static async Task EnsureColumnsAsync(HjemDb db)
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
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
