using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Runtime.Execution;
using ITMartin.Media.Runtime.Recovery;
using ITMartin.Media.Runtime.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        //services.AddHostedService<
          //  WorkflowRecoveryHostedService>();

        services.Configure<HostOptions>(
            options =>
            {
                options.ShutdownTimeout =
                    TimeSpan.FromHours(3);
            });

        return services;
    }
}