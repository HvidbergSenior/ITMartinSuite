using ITMartinLager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Tests;

// The repositories open a context per call - in tests they all share one in-memory SQLite connection.
public sealed class TestDbFactory(SqliteConnection conn) : IDbContextFactory<LagerDb>
{
    public LagerDb CreateDbContext() => new(new DbContextOptionsBuilder<LagerDb>().UseSqlite(conn).Options);
}
