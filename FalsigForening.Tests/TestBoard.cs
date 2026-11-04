using FalsigForening.Application;
using FalsigForening.Domain;
using FalsigForening.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FalsigForening.Tests;

// The task tests (written 2026-09 against the old flat AssignmentTaskService) run against today's TaskBoard use case
// with the real EF stores on one SQLite database in memory (the real provider: ExecuteDelete etc. work as in production).
// The use case saves through its own contexts, so a test reads results back with Fresh(_db), which forgets what the
// test's own context remembers and asks the database.
internal static class TestBoard
{
    public static (ClubDb Db, TaskBoard Board) Create()
    {
        // Task logic, not table links: test tasks need no real group row.
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        connection.Open();   // the in-memory database lives as long as this connection
        var options = new DbContextOptionsBuilder<ClubDb>().UseSqlite(connection).Options;
        var db = new ClubDb(options);
        db.Database.EnsureCreated();
        var factory = new Factory(options);
        var board = new TaskBoard(new TeamItems<Assignment>(factory), new TeamItems<MainTask>(factory), new TaskStore(factory),
            new NoPush(), TimeProvider.System);
        return (db, board);
    }

    /// <summary>The same context, with nothing remembered - the next read comes from the database.</summary>
    public static ClubDb Fresh(ClubDb db)
    {
        db.ChangeTracker.Clear();
        return db;
    }

    private sealed class Factory(DbContextOptions<ClubDb> options) : IDbContextFactory<ClubDb>
    {
        public ClubDb CreateDbContext() => new(options);
    }

    private sealed class NoPush : IPushNotifier
    {
        public string PublicKey => "";
        public Task SubscribeAsync(ClubPushSubscription subscription, CancellationToken ct = default) => Task.CompletedTask;
        public Task ToTeamAsync(Guid groupId, string? exceptMember, string title, string body) => Task.CompletedTask;
        public Task ToMembersAsync(Guid groupId, IEnumerable<string> memberNames, string title, string body, string? url = null) => Task.CompletedTask;
    }
}
