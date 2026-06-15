using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.Persistence.Stores;
using ITMartin.Media.Runtime.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Receipt.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReceiptInfrastructure(
        this IServiceCollection services)
    {
        // =========================
        // WORKFLOW ENGINE
        // =========================

        services.AddScoped<
            IWorkflowExecutor,
            WorkflowExecutor>();

        services.AddScoped<
            IWorkflowCheckpointStore,
            NullWorkflowCheckpointStore>();

        services.AddScoped<
            IWorkflowStepExecutionStore,
            NullWorkflowStepExecutionStore>();

        services.AddScoped<
            IWorkflowInstanceStore,
            NullWorkflowInstanceStore>();

        return services;
    }
}
