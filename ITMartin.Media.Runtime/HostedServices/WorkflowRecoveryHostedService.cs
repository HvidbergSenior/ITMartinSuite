using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Runtime.HostedServices;

public sealed class WorkflowRecoveryHostedService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<
            WorkflowRecoveryHostedService>
        _logger;

    public WorkflowRecoveryHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkflowRecoveryHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Workflow recovery started");

        while (!stoppingToken.IsCancellationRequested)
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
                        Package1WorkflowDefinition>();

            var workflowIds =
                await workflowStore
                    .GetRecoverableWorkflowIdsAsync(
                        stoppingToken);
            Console.WriteLine(
                "Current dir: " +
                Environment.CurrentDirectory);
            foreach (var workflowId in workflowIds)
            {
                _logger.LogInformation(
                    "Recovering workflow {WorkflowId}",
                    workflowId);

                var checkpointStore =
                    scope.ServiceProvider
                        .GetRequiredService<
                            IWorkflowCheckpointStore>();

                var state =
                    await checkpointStore
                        .LoadLatestCheckpointAsync<
                            Package1WorkflowState>(
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
                        Package1WorkflowState>
                    {
                        WorkflowId = workflowId,

                        WorkflowName =
                            workflowDefinition.Name,

                        State = state,

                        CancellationToken =
                            stoppingToken
                    };

                await workflowExecutor.ExecuteAsync(
                    workflowDefinition,
                    context,
                    stoppingToken);
            }

            await Task.Delay(
                TimeSpan.FromSeconds(5),
                stoppingToken);
        }
    }
}