using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.BackgroundJobs;
using ITMartin.Media.Infrastructure.Persistence.Stores;
using ITMartin.Media.Runtime.BackgroundJobs;
using ITMartin.Media.Runtime.Execution;
using ITMartin.Media.Runtime.HostedServices;
using ITMartin.Media.Runtime.Recovery;
using ITMartin.Media.Runtime.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ITMartin.Media.Infrastructure.DependencyInjection;

public static class RuntimeDependencyInjection
{
    public static IServiceCollection AddMediaRuntime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<
            IWorkflowExecutor,
            WorkflowExecutor>();

        services.AddScoped<
            IWorkflowRegistry,
            WorkflowRegistry>();

        services.AddScoped<
            IWorkflowRecoveryService,
            WorkflowRecoveryService>();
        services.TryAddScoped<
            IWorkflowAlertNotifier,
            NullWorkflowAlertNotifier>();
        services.AddHostedService<
            WorkflowQueueConsumerHostedService>();
        services.AddSingleton<
            IBackgroundJobQueue,
            RabbitMqBackgroundJobQueue>();
        //services.AddHostedService<
          //WorkflowRecoveryHostedService>();

        services.Configure<HostOptions>(
            options =>
            {
                options.ShutdownTimeout =
                    TimeSpan.FromHours(3);
            });

        return services;
    }
}