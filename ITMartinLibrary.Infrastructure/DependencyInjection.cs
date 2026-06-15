using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Runtime.Execution;
using ITMartinLibrary.Infrastructure.Workflows;
using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Infrastructure.Options;
using ITMartinLibrary.Infrastructure.Repositories;
using ITMartinLibrary.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinLibrary.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLibraryInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=library.db";

        services.AddDbContext<LibraryDbContext>(
            options => options.UseSqlite(connectionString));

        services.AddScoped<
            IInventoryRepository,
            InventoryRepository>();

        services.AddSingleton<
            IBarcodeEnrichmentQueue,
            BarcodeEnrichmentQueue>();

        services.AddHttpClient<
            IBarcodeLookupService,
            BarcodeLookupService>();

        services.Configure<OmdbOptions>(
            configuration.GetSection("Omdb"));

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
