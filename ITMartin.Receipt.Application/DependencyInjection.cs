using ITMartin.Receipt.Application.Interfaces;
using ITMartin.Receipt.Application.Services;
using ITMartin.Receipt.Application.Workflows;
using ITMartin.Receipt.Application.Workflows.Steps;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Receipt.Application;

public static class DependencyInjection
{
    public static IServiceCollection
        AddReceiptApplication(
            this IServiceCollection services)
    {
        services.AddScoped<
            IReceiptWorkflowOrchestrator,
            ReceiptWorkflowOrchestrator>();

        services.AddScoped<
            ReceiptWorkflowRunner>();

        services.AddScoped<
            ReceiptWorkflowDefinition>();

        // =========================
        // WORKFLOW STEPS
        // =========================

        services.AddScoped<
            ReceiptOcrWorkflowStep>();

        services.AddScoped<
            AiReceiptExtractionWorkflowStep>();

        services.AddScoped<
            SaveTransactionWorkflowStep>();

        return services;
    }
}