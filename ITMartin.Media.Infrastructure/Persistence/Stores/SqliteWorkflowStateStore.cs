using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Media.Infrastructure.Persistence.Stores;

public sealed class SqliteWorkflowStateStore
    : IWorkflowStateStore
{
    private readonly MediaDbContext _dbContext;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public SqliteWorkflowStateStore(
        MediaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveAsync<TState>(
        Guid workflowId,
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        var serialized =
            JsonSerializer.Serialize(
                context,
                JsonOptions);

        var snapshot =
            await _dbContext.WorkflowStateSnapshots
                .FirstOrDefaultAsync(
                    x => x.WorkflowId == workflowId,
                    cancellationToken);

        if (snapshot is null)
        {
            snapshot = new WorkflowStateSnapshot
            {
                WorkflowId = workflowId
            };

            _dbContext.WorkflowStateSnapshots.Add(snapshot);
        }

        snapshot.SerializedContext = serialized;
        snapshot.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowExecutionContext<TState>?> LoadAsync<TState>(
        Guid workflowId,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        var snapshot =
            await _dbContext.WorkflowStateSnapshots
                .FirstOrDefaultAsync(
                    x => x.WorkflowId == workflowId,
                    cancellationToken);

        if (snapshot is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<WorkflowExecutionContext<TState>>(
            snapshot.SerializedContext,
            JsonOptions);
    }
}