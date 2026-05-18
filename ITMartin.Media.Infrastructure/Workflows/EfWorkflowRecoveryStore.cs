// File: ITMartin.Media.Infrastructure/Workflows/EfWorkflowRecoveryStore.cs

using ITMartin.Media.Application.Abstractions.Workflows;
using ITMartin.Media.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Media.Infrastructure.Workflows;

public sealed class EfWorkflowRecoveryStore(
    MediaDbContext dbContext)
    : IWorkflowRecoveryStore
{
    public async Task<IReadOnlyCollection<Guid>>
        GetUnfinishedWorkflowIdsAsync(
            CancellationToken cancellationToken = default)
    {
        var latestCheckpoints =
            await dbContext.WorkflowCheckpoints
                .GroupBy(x => x.WorkflowId)
                .Select(x => x
                    .OrderByDescending(y => y.CreatedAtUtc)
                    .First())
                .Where(x => !x.IsCompleted)
                .Select(x => x.WorkflowId)
                .ToListAsync(cancellationToken);

        return latestCheckpoints;
    }
}