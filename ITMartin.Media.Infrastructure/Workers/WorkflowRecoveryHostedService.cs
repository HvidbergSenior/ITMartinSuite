using ITMartin.Media.Application.Abstractions.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Infrastructure.Workers;

public sealed class WorkflowRecoveryHostedService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<WorkflowRecoveryHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await Task.Delay(
            TimeSpan.FromSeconds(5),
            stoppingToken);

        using var scope = serviceScopeFactory.CreateScope();

        var recoveryStore =
            scope.ServiceProvider
                .GetRequiredService<IWorkflowRecoveryStore>();

        var workflowIds =
            await recoveryStore.GetUnfinishedWorkflowIdsAsync(
                stoppingToken);

        foreach (var workflowId in workflowIds)
        {
            logger.LogInformation(
                "Recovering workflow {WorkflowId}",
                workflowId);

            // temporary placeholder
        }
    }
}