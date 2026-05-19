using ITMartin.Media.Runtime.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Runtime.Services;

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

        using var scope =
            serviceScopeFactory.CreateScope();

        var workflowInstanceStore =
            scope.ServiceProvider
                .GetRequiredService<IWorkflowInstanceStore>();

        var workflowIds =
            await workflowInstanceStore
                .GetRecoverableWorkflowIdsAsync(
                    stoppingToken);

        var recoveryService =
            scope.ServiceProvider
                .GetRequiredService<IWorkflowRecoveryService>();

        foreach (var workflowId in workflowIds)
        {
            logger.LogInformation(
                "Recovering workflow {WorkflowId}",
                workflowId);

            await recoveryService.RecoverAsync(
                workflowId,
                stoppingToken);
        }
    }
}