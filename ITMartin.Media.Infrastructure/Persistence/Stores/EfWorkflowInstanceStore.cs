using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Media.Infrastructure.Persistence.Stores;

public sealed class EfWorkflowInstanceStore(
    MediaDbContext dbContext)
    : IWorkflowInstanceStore
{
    public async Task CreateAsync(
        Guid workflowId,
        string workflowName,
        CancellationToken cancellationToken = default)
    {
        var entity =
            new WorkflowInstanceEntity
            {
                WorkflowId = workflowId,
                WorkflowName = workflowName,
                Status = "Running",
                StartedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

        await dbContext.WorkflowInstances.AddAsync(
            entity,
            cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task SetRunningStepAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        var entity =
            await dbContext.WorkflowInstances
                .FirstAsync(
                    x => x.WorkflowId == workflowId,
                    cancellationToken);

        entity.CurrentStep = stepName;

        entity.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task MarkCompletedAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var entity =
            await dbContext.WorkflowInstances
                .FirstAsync(
                    x => x.WorkflowId == workflowId,
                    cancellationToken);

        entity.Status = "Completed";

        entity.CompletedAtUtc = DateTime.UtcNow;

        entity.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task MarkFailedAsync(
        Guid workflowId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var entity =
            await dbContext.WorkflowInstances
                .FirstAsync(
                    x => x.WorkflowId == workflowId,
                    cancellationToken);

        entity.Status = "Failed";

        entity.FailureReason = reason;

        entity.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }
}