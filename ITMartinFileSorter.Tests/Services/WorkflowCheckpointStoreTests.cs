using FluentAssertions;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Infrastructure.Persistence.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ITMartinFileSorter.Tests.Services;

// Runs against REAL SQLite, not the in-memory provider, and that is the whole
// point of it.
//
// Checkpoint pruning was added on 2026-09-09 and shipped with an
// OrderByDescending(x => x.CreatedAtUtc) evaluated server-side. The full unit
// suite passed - because every other test uses UseInMemoryDatabase, which
// happily orders anything. On real SQLite that throws outright:
//
//   SQLite does not support expressions of type 'DateTimeOffset' in ORDER BY
//
// The first checkpoint save of every run failed, which failed the job, which
// requeued it, which started another run: the worker looped instead of
// sorting, and a 5% test run was what caught it. Every other query in
// EfWorkflowCheckpointStore already materialises before ordering for exactly
// this reason.
[TestFixture]
public class WorkflowCheckpointStoreTests
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<MediaDbContext> _options = null!;

    [SetUp]
    public void SetUp()
    {
        // In-memory SQLite is still the real SQLite engine and its real
        // limitations - unlike EF's InMemory provider, which is not SQL at all.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new MediaDbContext(_options);
        db.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown() => _connection.Dispose();

    private async Task<int> CheckpointCountAsync(Guid workflowId)
    {
        await using var db = new MediaDbContext(_options);
        return await db.WorkflowCheckpoints.CountAsync(x => x.WorkflowId == workflowId);
    }

    private sealed record State(string Value);

    [Test]
    public async Task Saving_a_checkpoint_works_on_real_sqlite()
    {
        var workflowId = Guid.NewGuid();

        await using var db = new MediaDbContext(_options);
        var store = new EfWorkflowCheckpointStore(db);

        await store.SaveCheckpointAsync(workflowId, "QuickSortWorkflow", "FileDiscovery", new State("one"));

        (await CheckpointCountAsync(workflowId)).Should().Be(1);

        var loaded = await store.LoadLatestCheckpointAsync<State>(workflowId);
        loaded.Should().NotBeNull();
        loaded!.Value.Should().Be("one");
    }

    // The state blob is the entire MediaFiles list on a real library - tens of
    // megabytes per row, once per step. Unpruned, that is what grew one
    // library's .media.db to 5.6 GB with a 530 MB WAL beside it.
    [Test]
    public async Task Old_checkpoints_are_pruned_and_the_latest_still_loads()
    {
        var workflowId = Guid.NewGuid();

        await using var db = new MediaDbContext(_options);
        var store = new EfWorkflowCheckpointStore(db);

        foreach (var step in new[] { "one", "two", "three", "four", "five", "six" })
        {
            await store.SaveCheckpointAsync(workflowId, "QuickSortWorkflow", step, new State(step));
        }

        // The current checkpoint plus the two superseded ones kept for
        // inspecting a bad transition.
        (await CheckpointCountAsync(workflowId)).Should().Be(3);

        var loaded = await store.LoadLatestCheckpointAsync<State>(workflowId);
        loaded!.Value.Should().Be("six", "pruning must never remove the checkpoint recovery resumes from");
    }

    [Test]
    public async Task Another_workflows_checkpoints_are_untouched()
    {
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();

        await using var db = new MediaDbContext(_options);
        var store = new EfWorkflowCheckpointStore(db);

        await store.SaveCheckpointAsync(theirs, "QuickSortWorkflow", "only", new State("theirs"));

        foreach (var step in new[] { "a", "b", "c", "d", "e" })
        {
            await store.SaveCheckpointAsync(mine, "QuickSortWorkflow", step, new State(step));
        }

        (await CheckpointCountAsync(theirs)).Should().Be(1);
        (await store.LoadLatestCheckpointAsync<State>(theirs))!.Value.Should().Be("theirs");
    }
}
