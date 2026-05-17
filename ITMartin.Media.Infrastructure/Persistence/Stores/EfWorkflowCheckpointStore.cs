using System.Text.Json;
using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Media.Infrastructure.Persistence.Stores;

public sealed class EfWorkflowCheckpointStore
    : IWorkflowCheckpointStore
{
    private readonly MediaDbContext _dbContext;

    public EfWorkflowCheckpointStore(MediaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveCheckpointAsync(
        Guid workflowId,
        string workflowName,
        string stepName,
        object state,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(state);

        var entity = await _dbContext.WorkflowCheckpoints
            .FirstOrDefaultAsync(
                x => x.WorkflowId == workflowId &&
                     x.StepName == stepName,
                cancellationToken);

        if (entity is null)
        {
            entity = new WorkflowCheckpointEntity
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflowId,
                WorkflowName = workflowName,
                StepName = stepName,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            await _dbContext.WorkflowCheckpoints.AddAsync(entity, cancellationToken);
        }

        entity.StateJson = json;
        entity.IsCompleted = false;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<T?> LoadCheckpointAsync<T>(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.WorkflowCheckpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.WorkflowId == workflowId &&
                     x.StepName == stepName,
                cancellationToken);

        if (entity is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(entity.StateJson);
    }

    public async Task MarkCompletedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.WorkflowCheckpoints
            .FirstOrDefaultAsync(
                x => x.WorkflowId == workflowId &&
                     x.StepName == stepName,
                cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.IsCompleted = true;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}