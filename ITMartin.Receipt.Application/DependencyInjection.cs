using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Receipt.Application.Workflows;
using ITMartin.Receipt.Application.Workflows.Steps;

namespace ITMartin.Receipt.Application;

public static class DependencyInjection
{
    public static IServiceCollection
        AddReceiptApplication(
            this IServiceCollection services)
    {
        services.AddScoped<
            ReceiptOcrWorkflowStep>();

        services.AddScoped<
            OpenAiReceiptExtractionWorkflowStep>();

        services.AddScoped<
            SaveTransactionWorkflowStep>();

        services.AddScoped<
            IWorkflowDefinition,
            ReceiptWorkflowDefinition>();

        return services;
    }
}