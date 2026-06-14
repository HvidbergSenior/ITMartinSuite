using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Services;
using ITMartin.Magic.Application.Workflows;
using ITMartin.Magic.Application.Workflows.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Magic.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddMagicApplication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<
            ICardScanOrchestrator,
            CardScanOrchestrator>();
        services.AddScoped<
            CardScanWorkflowRunner>();
        services.AddScoped<
            CardScanWorkflow>();

        services.AddScoped<
            CardScanWorkflowDefinition>();

        // =========================
        // WORKFLOW STEPS
        // =========================

        services.AddScoped<
            AiCardRecognitionWorkflowStep>();

       services.AddScoped<
           FinalScryfallMatchWorkflowStep>();

       services.AddScoped<
        CardConditionWorkflowStep>();
       services.AddScoped<
           ResultMappingWorkflowStep>();
     
        return services;
    }
}