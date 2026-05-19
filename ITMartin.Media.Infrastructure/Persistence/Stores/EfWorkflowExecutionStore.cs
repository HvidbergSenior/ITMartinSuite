using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Media.Infrastructure.Persistence.Stores;

public sealed class EfWorkflowStepExecutionStore(
    IDbContextFactory<MediaDbContext> dbContextFactory)
    : IWorkflowStepExecutionStore
{
    public async Task<bool> IsCompletedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        return await dbContext.WorkflowStepExecutions
            .AnyAsync(
                x =>
                    x.WorkflowId == workflowId
                    && x.StepName == stepName
                    && x.Status == "Completed",
                cancellationToken);
    }

    public async Task MarkStartedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        dbContext.WorkflowStepExecutions.Add(
            new WorkflowStepExecutionEntity
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflowId,
                StepName = stepName,
                Status = "Started",
                CreatedAt = DateTimeOffset.UtcNow
            });

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task MarkCompletedAsync(
        Guid workflowId,
        string stepName,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await dbContext.WorkflowStepExecutions
                .Where(x =>
                    x.WorkflowId == workflowId
                    && x.StepName == stepName)
                .ToListAsync(cancellationToken);

        var latest =
            entity
                .OrderByDescending(x => x.CreatedAt)
                .First();

        latest.Status = "Completed";

        latest.CompletedAt = DateTimeOffset.UtcNow;
        
        await dbContext.SaveChangesAsync(
            cancellationToken);
    }
}