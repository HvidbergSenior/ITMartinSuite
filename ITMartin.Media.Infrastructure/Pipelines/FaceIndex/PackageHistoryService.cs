using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Media.Infrastructure.Pipelines.FaceIndex;

public sealed class PackageHistoryService : IPackageHistoryService
{
    private readonly IDbContextFactory<MediaDbContext> _dbFactory;

    public PackageHistoryService(IDbContextFactory<MediaDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<PackageRunSummary>> GetRunsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var instances = await db.WorkflowInstances
            .Where(x => x.WorkflowName == "QuickSortWorkflow")
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync();

        var summaries = new List<PackageRunSummary>();

        foreach (var instance in instances)
        {
            // IsLatest should already narrow this to one row per workflow;
            // ordering happens client-side since SQLite can't translate
            // OrderBy over a DateTimeOffset column server-side.
            var checkpoint = (await db.WorkflowCheckpoints
                .Where(x => x.WorkflowId == instance.WorkflowId && x.IsLatest)
                .ToListAsync())
                .OrderByDescending(x => x.UpdatedAtUtc)
                .FirstOrDefault();

            summaries.Add(BuildSummary(instance, checkpoint));
        }

        return summaries;
    }

    private static PackageRunSummary BuildSummary(
        Persistence.Entities.WorkflowInstanceEntity instance,
        Persistence.Entities.WorkflowCheckpointEntity? checkpoint)
    {
        if (checkpoint is null)
        {
            return new PackageRunSummary
            {
                WorkflowId = instance.WorkflowId,
                WorkflowName = instance.WorkflowName,
                Status = instance.Status,
                StartedAtUtc = instance.StartedAtUtc,
                CompletedAtUtc = instance.CompletedAtUtc,
                Verdict = ReconciliationVerdict.Unknown
            };
        }

        QuickSortWorkflowState? state;
        try
        {
            state = JsonSerializer.Deserialize<QuickSortWorkflowState>(checkpoint.StateJson);
        }
        catch (JsonException)
        {
            state = null;
        }

        if (state is null)
        {
            return new PackageRunSummary
            {
                WorkflowId = instance.WorkflowId,
                WorkflowName = instance.WorkflowName,
                Status = instance.Status,
                StartedAtUtc = instance.StartedAtUtc,
                CompletedAtUtc = instance.CompletedAtUtc,
                Verdict = ReconciliationVerdict.Unknown
            };
        }

        var sourceCount = state.MediaFiles.Count;
        var sourceBytes = state.MediaFiles.Sum(f => f.SizeBytes);
        var exportedCount = state.ExportResult?.ExportedFiles ?? 0;
        var exportedBytes = state.ExportResult?.ExportedBytes ?? 0;
        var duplicateCount = state.CleanupResult?.DeleteCount ?? 0;
        var failedFiles = state.FailedFiles.Select(f => (f.FilePath, f.Error)).ToList();

        var verdict = instance.Status == "Running"
            ? ReconciliationVerdict.InProgress
            : state.ExportResult is null
                ? ReconciliationVerdict.Unknown
                : sourceCount == exportedCount + duplicateCount + failedFiles.Count
                    ? ReconciliationVerdict.Verified
                    : ReconciliationVerdict.Mismatch;

        return new PackageRunSummary
        {
            WorkflowId = instance.WorkflowId,
            WorkflowName = instance.WorkflowName,
            Status = instance.Status,
            StartedAtUtc = instance.StartedAtUtc,
            CompletedAtUtc = instance.CompletedAtUtc,
            SourcePath = state.RootPath,
            OutputPath = state.OutputPath,
            SourceFileCount = sourceCount,
            ExportedFileCount = exportedCount,
            DuplicateFileCount = duplicateCount,
            FailedFileCount = failedFiles.Count,
            SourceSizeBytes = sourceBytes,
            ExportedSizeBytes = exportedBytes,
            FailedFiles = failedFiles,
            Verdict = verdict
        };
    }
}
