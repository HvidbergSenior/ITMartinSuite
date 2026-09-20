using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Runtime.Execution;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ITMartin.Receipt.Application.Interfaces;
using ITMartin.Receipt.Infrastructure.Repositories;
using ITMartin.Receipt.Infrastructure.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Receipt.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReceiptInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=receipts.db";

        services.AddDbContext<ReceiptDbContext>(
            options => options.UseSqlite(connectionString));

        services.AddScoped<
            IReceiptRepository,
            ReceiptRepository>();

        // =========================
        // WORKFLOW ENGINE
        // =========================

        services.AddScoped<
            IWorkflowExecutor,
            WorkflowExecutor>();

        // WorkflowExecutor now takes an alert notifier; Receipt has no alert
        // store, so use a no-op one.
        services.TryAddScoped<
            IWorkflowAlertNotifier,
            NoopWorkflowAlertNotifier>();

        services.AddSingleton<
            ActiveWorkflowRegistry>();

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
