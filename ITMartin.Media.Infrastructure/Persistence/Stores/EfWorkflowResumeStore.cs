using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Application.Models.Workflows;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Media.Infrastructure.Persistence.Stores;

public sealed class EfWorkflowResumeStore(
    MediaDbContext dbContext)
    : IWorkflowResumeStore
{
    public async Task SaveAsync(
        WorkflowResumeState state,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.WorkflowResumes
            .FirstOrDefaultAsync(
                x => x.WorkflowId == state.WorkflowId,
                cancellationToken);

        if (entity is null)
        {
            entity = new WorkflowResumeEntity
            {
                WorkflowId = state.WorkflowId
            };

            dbContext.WorkflowResumes.Add(entity);
        }

        entity.LastCompletedStep = state.LastCompletedStep!;
        entity.UpdatedAtUtc = state.UpdatedAt;
        entity.IsCompleted = false;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowResumeState?> GetAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.WorkflowResumes
            .FirstOrDefaultAsync(
                x => x.WorkflowId == workflowId
                     && !x.IsCompleted,
                cancellationToken);

        if (entity is null)
        {
            return null;
        }

        return new WorkflowResumeState(
            entity.WorkflowId,
            entity.LastCompletedStep,
            entity.UpdatedAtUtc);
    }

    public async Task MarkCompletedAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.WorkflowResumes
            .FirstOrDefaultAsync(
                x => x.WorkflowId == workflowId,
                cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.IsCompleted = true;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public async Task<IReadOnlyCollection<Guid>> GetUnfinishedWorkflowIdsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.WorkflowResumes
            .Where(x => !x.IsCompleted)
            .Select(x => x.WorkflowId)
            .ToListAsync(cancellationToken);
    }
}