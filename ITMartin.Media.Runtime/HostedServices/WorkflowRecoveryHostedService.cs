using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Runtime.BackgroundJobs;
using ITMartin.Media.Runtime.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Runtime.HostedServices;

public sealed class WorkflowRecoveryHostedService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ActiveWorkflowRegistry
        _activeWorkflowRegistry;

    private readonly ILogger<
            WorkflowRecoveryHostedService>
        _logger;

    // GetRecoverableWorkflowIdsAsync also returns Failed workflows, and this
    // loop re-checks every 5 seconds. A workflow that fails deterministically
    // - a corrupt file, a genuine bug - is therefore retried forever, redoing
    // every expensive step each time and never draining. Cap it: after this
    // many attempts the workflow needs a human, not another lap.
    private const int MaxRecoveryAttempts = 3;

    private readonly Dictionary<Guid, int> _attempts = new();

    private readonly HashSet<Guid> _abandoned = [];

    public WorkflowRecoveryHostedService(
        IServiceScopeFactory scopeFactory,
        ActiveWorkflowRegistry activeWorkflowRegistry,
        ILogger<WorkflowRecoveryHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _activeWorkflowRegistry = activeWorkflowRegistry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Workflow recovery started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Polling touches SQLite, which under this pipeline's write
                // load really does throw "database is locked". Letting that
                // escape a BackgroundService stops the whole host, so a
                // transient lock would permanently disable recovery.
                _logger.LogError(
                    ex,
                    "Workflow recovery poll failed - continuing");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(5),
                stoppingToken);
        }
    }

    private async Task PollOnceAsync(
        CancellationToken stoppingToken)
    {
        using var scope =
            _scopeFactory.CreateScope();

        var workflowStore =
            scope.ServiceProvider
                .GetRequiredService<
                    IWorkflowInstanceStore>();

        var workflowExecutor =
            scope.ServiceProvider
                .GetRequiredService<
                    IWorkflowExecutor>();

        var workflowDefinition =
            scope.ServiceProvider
                .GetRequiredService<
                    QuickSortWorkflowDefinition>();

        var workflowIds =
            await workflowStore
                .GetRecoverableWorkflowIdsAsync(
                    stoppingToken);
        foreach (var workflowId in workflowIds)
        {
            // "Running" in the DB includes workflows executing right
            // now, not just ones orphaned by a previous process - so
            // recovering on that alone duplicates a live run. See
            // ActiveWorkflowRegistry.
            if (_activeWorkflowRegistry.IsActive(workflowId))
            {
                continue;
            }

            if (_abandoned.Contains(workflowId))
            {
                continue;
            }

            var attempt =
                _attempts.GetValueOrDefault(workflowId) + 1;

            _attempts[workflowId] = attempt;

            if (attempt > MaxRecoveryAttempts)
            {
                _abandoned.Add(workflowId);

                _logger.LogError(
                    "Workflow {WorkflowId} failed {Attempts} recovery attempts - abandoning it, no further retries until this process restarts",
                    workflowId,
                    MaxRecoveryAttempts);

                continue;
            }

            _logger.LogInformation(
                "Recovering workflow {WorkflowId} (attempt {Attempt} of {MaxAttempts})",
                workflowId,
                attempt,
                MaxRecoveryAttempts);

            var checkpointStore =
                scope.ServiceProvider
                    .GetRequiredService<
                        IWorkflowCheckpointStore>();

            var state =
                await checkpointStore
                    .LoadLatestCheckpointAsync<
                        QuickSortWorkflowState>(
                            workflowId,
                            stoppingToken);

            if (state is null)
            {
                _logger.LogWarning(
                    "No checkpoint found for workflow {WorkflowId}",
                    workflowId);

                continue;
            }

            var context =
                new WorkflowExecutionContext<
                    QuickSortWorkflowState>
                {
                    WorkflowId = workflowId,

                    WorkflowName =
                        workflowDefinition.Name,

                    State = state,

                    CancellationToken =
                        stoppingToken
                };

            try
            {
                await workflowExecutor.ExecuteAsync(
                    workflowDefinition,
                    context,
                    stoppingToken);

                // A recovered workflow used to stop here, having run all 18
                // sort steps and none of the post-sort ones - those lived
                // inside StartQuickSortHandler, which recovery never touches.
                // The result looked like success while quietly producing a
                // less complete library: on ToshibaTest 2026-09-09 it left
                // MediaFaces empty and no static gallery export. Nothing
                // failed; the steps simply did not exist on this path.
                var outputPath = !string.IsNullOrWhiteSpace(state.OutputPath)
                    ? state.OutputPath
                    : scope.ServiceProvider
                        .GetRequiredService<ILibraryPathProvider>()
                        .LibraryRoot;

                await scope.ServiceProvider
                    .GetRequiredService<QuickSortAddonSteps>()
                    .RunAsync(outputPath, stoppingToken);
            }
            catch (Exception ex)
            {
                // WorkflowExecutor rethrows whatever a step threw. An
                // unhandled exception out of a BackgroundService stops
                // the entire host by default, so without this one
                // unrecoverable workflow takes the whole worker down and
                // recovery never runs again. The attempt cap above is
                // what stops this from spinning.
                _logger.LogError(
                    ex,
                    "Recovery attempt {Attempt} for workflow {WorkflowId} failed",
                    attempt,
                    workflowId);
            }
        }
    }
}
