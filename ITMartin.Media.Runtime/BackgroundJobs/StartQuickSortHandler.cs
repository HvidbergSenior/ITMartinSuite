using System.Text.Json;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Runtime.BackgroundJobs;

public sealed class StartQuickSortHandler
    : IBackgroundJobHandler
{
    private readonly IScanOrchestrator _orchestrator;
    private readonly QuickSortWorkflowRunner _runner;
    private readonly QuickSortAddonSteps _addonSteps;
    private readonly ILibraryPathProvider _libraryPathProvider;
    private readonly ILogger<StartQuickSortHandler> _logger;

    public string JobType =>
        BackgroundJobTypes.StartQuickSort;

    public StartQuickSortHandler(
        IScanOrchestrator orchestrator,
        QuickSortWorkflowRunner runner,
        QuickSortAddonSteps addonSteps,
        ILibraryPathProvider libraryPathProvider,
        ILogger<StartQuickSortHandler> logger)
    {
        _orchestrator = orchestrator;
        _runner = runner;
        _addonSteps = addonSteps;
        _libraryPathProvider = libraryPathProvider;
        _logger = logger;
    }

    public async Task HandleAsync(
        BackgroundJob job,
        CancellationToken cancellationToken)
    {
        var request =
            JsonSerializer.Deserialize<
                QuickSortWorkflowState>(
                job.Payload);

        if (request is null)
        {
            return;
        }

        var workflowId =
            await _orchestrator.StartAsync(
                request,
                cancellationToken);

        await _runner.ExecuteAsync(
            workflowId,
            request,
            cancellationToken);

        // Same fallback ExportWorkflowExecutionStep itself uses - state.OutputPath
        // is only set when the caller passed one explicitly, otherwise the actual
        // export root is ILibraryPathProvider.LibraryRoot. Every add-on below has
        // to agree with export on where the files actually landed.
        var outputPath =
            !string.IsNullOrWhiteSpace(request.OutputPath)
                ? request.OutputPath
                : _libraryPathProvider.LibraryRoot;

        // Full mirror of QuickSort's own output, taken before any FaceIndex/
        // add-on step below gets a chance to touch it - see QuickSortBaselineHelper
        // for why. Refreshed on every run so it always reflects the latest
        // sort, not just the first one. A failure here shouldn't block the
        // add-on chain - the sort itself already succeeded either way, but
        // it does mean no safe rollback point exists for this run.
        if (request.EnableBaselineSnapshot)
        {
            try
            {
                await QuickSortBaselineHelper.MirrorDirectoryAsync(
                    outputPath,
                    QuickSortBaselineHelper.GetBaselinePath(outputPath),
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Baseline snapshot failed for {OutputPath}", outputPath);
            }
        }


        // The post-sort steps live in QuickSortAddonSteps rather than inline
        // here, because a workflow recovered by WorkflowRecoveryHostedService
        // never reaches this handler - it goes straight to the executor. When
        // they lived here, a recovered run finished all 18 sort steps,
        // reported success, and silently skipped every one of them. See that
        // type for the full account and for what is deliberately excluded.
        await _addonSteps.RunAsync(outputPath, cancellationToken);
    }
}
