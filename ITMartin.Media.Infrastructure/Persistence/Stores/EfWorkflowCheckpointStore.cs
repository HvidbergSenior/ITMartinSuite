using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using ITMartin.Media.Infrastructure.Serialization;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Media.Infrastructure.Persistence.Stores;

public sealed class EfWorkflowCheckpointStore
    : IWorkflowCheckpointStore
{
    // How many already-superseded checkpoints to keep per workflow, beyond
    // the current one. Enough to look at how a bad step transitioned;
    // see the pruning comment in SaveCheckpointAsync for why it is not
    // unlimited.
    private const int SupersededCheckpointsToKeep = 2;

    private readonly MediaDbContext _dbContext;

    public EfWorkflowCheckpointStore(
        MediaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveCheckpointAsync<T>(
        Guid workflowId,
        string workflowName,
        string stepName,
        T state,
        CancellationToken cancellationToken = default)
    {
        var json =
            JsonSerializer.Serialize(
                state,
                MediaJson.Default);

        // Demoting the old IsLatest row and inserting the new one used to be
        // two separate, un-transacted writes (ExecuteUpdateAsync commits
        // immediately; the insert only commits later via SaveChangesAsync).
        // If the process died in between - exactly what happened during the
        // ToshibaTest run 2026-09-08, a "database is locked" exception hit
        // in that exact window under heavy concurrent write load from
        // parallel video conversions - every checkpoint ends up with
        // IsLatest=false and nothing to resume from, permanently breaking
        // recovery. Wrapping both writes in one transaction makes it
        // atomic: either both happen, or neither does and the previous
        // checkpoint stays valid.
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        await _dbContext.WorkflowCheckpoints
            .Where(x =>
                x.WorkflowId == workflowId &&
                x.IsLatest)
            .ExecuteUpdateAsync(
                x => x.SetProperty(
                    y => y.IsLatest,
                    false),
                cancellationToken);

        var entity =
            new WorkflowCheckpointEntity
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflowId,
                WorkflowName = workflowName,
                StepName = stepName,
                StateJson = json,
                Status = "Running",
                Attempt = 1,
                IsLatest = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

        await _dbContext.WorkflowCheckpoints
            .AddAsync(
                entity,
                cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        // Every checkpoint row holds the ENTIRE serialized workflow state -
        // on a real library that is the whole MediaFiles list, tens of
        // megabytes per row, written once per step. Nothing ever deleted
        // them, so the database grew without bound: the ToshibaTest run
        // 2026-09-09 left a 5.6 GB .media.db with a 530 MB WAL beside it, on
        // the same slow external drive the export was competing for. Keeping
        // a couple of superseded rows is enough to inspect a bad transition;
        // the rest are dead weight that make every later write slower.
        // Ordered in memory, not in SQL. SQLite cannot ORDER BY a
        // DateTimeOffset column - it throws NotSupportedException outright -
        // and CreatedAtUtc is one. Every other query in this file already
        // materialises before ordering for exactly this reason; ordering
        // server-side here broke the very first checkpoint save of every run,
        // which failed the job, requeued it, and left the worker looping.
        var supersededIds =
            (await _dbContext.WorkflowCheckpoints
                .Where(x =>
                    x.WorkflowId == workflowId &&
                    !x.IsLatest)
                .Select(x => new { x.Id, x.CreatedAtUtc })
                .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(SupersededCheckpointsToKeep)
            .Select(x => x.Id)
            .ToList();

        if (supersededIds.Count > 0)
        {
            await _dbContext.WorkflowCheckpoints
                .Where(x => supersededIds.Contains(x.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<T?> LoadLatestCheckpointAsync<T>(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var entity =
            (await _dbContext.WorkflowCheckpoints
                .AsNoTracking()
                .Where(x =>
                    x.WorkflowId == workflowId &&
                    x.IsLatest)
                .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();

        if (entity is null)
        {
            return default;
        }

        if (string.IsNullOrWhiteSpace(entity.StateJson))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(
                entity.StateJson,
                MediaJson.Default);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize checkpoint for workflow {workflowId}",
                ex);
        }
    }

    public async Task<IReadOnlyList<WorkflowCheckpoint>>
        GetCheckpointHistoryAsync(
            Guid workflowId,
            CancellationToken cancellationToken = default)
    {
        var entities =
            (await _dbContext.WorkflowCheckpoints
                .AsNoTracking()
                .Where(x => x.WorkflowId == workflowId)
                .ToListAsync(cancellationToken))
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();

        return entities
            .Select(x =>
                new WorkflowCheckpoint(
                    x.WorkflowId,
                    x.WorkflowName,
                    x.StepName,
                    x.StateJson,
                    x.CreatedAtUtc))
            .ToList();
    }

    public async Task MarkCompletedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        var entity =
            (await _dbContext.WorkflowCheckpoints
                .Where(x =>
                    x.WorkflowId == workflowId &&
                    x.StepName == stepName &&
                    x.IsLatest)
                .ToListAsync(cancellationToken))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();

        if (entity is null)
        {
            return;
        }

        entity.Status = "Completed";
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}