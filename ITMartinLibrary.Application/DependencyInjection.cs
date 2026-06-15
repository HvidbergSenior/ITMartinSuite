using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Application.Services;
using ITMartinLibrary.Application.Workflows;
using ITMartinLibrary.Application.Workflows.Steps;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinLibrary.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddLibraryApplication(
        this IServiceCollection services)
    {
        services.AddScoped<InventoryService>();

        services.AddScoped<
            IShelfScanOrchestrator,
            ShelfScanOrchestrator>();

        services.AddScoped<
            ShelfScanWorkflowRunner>();

        services.AddScoped<
            ShelfScanWorkflowDefinition>();

        // =========================
        // WORKFLOW STEPS
        // =========================

        services.AddScoped<
            AiShelfRecognitionWorkflowStep>();

        services.AddScoped<
            ItemLookupWorkflowStep>();

        services.AddScoped<
            ShelfResultMappingWorkflowStep>();

        return services;
    }
}
