using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ITMartin.Media.Infrastructure.Persistence;

public sealed class SqliteWalInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData) =>
        ApplyPragmas(connection);

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ApplyPragmas(connection);
        return Task.CompletedTask;
    }

    private static void ApplyPragmas(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        // 5s wasn't enough under heavy concurrent write load (ToshibaTest
        // run, 2026-09-08 - dozens of parallel video conversions plus
        // per-file classification/progress writes hitting this same file
        // at once); a "database is locked" mid-checkpoint-save is what
        // triggered the un-transacted-write bug fixed in
        // EfWorkflowCheckpointStore. 30s is still bounded, just gives
        // contention a real chance to clear on a busy run.
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=30000;";
        cmd.ExecuteNonQuery();
    }
}
