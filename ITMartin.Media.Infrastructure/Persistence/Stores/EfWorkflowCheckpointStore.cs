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