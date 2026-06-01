using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Services;
using ITMartin.Magic.Application.Workflows;
using ITMartin.Magic.Application.Workflows.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Magic.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddMagicApplication(
        this IServiceCollection services)
    {
        // =========================
        // ORCHESTRATION
        // =========================

        services.AddScoped<
            ICardScanOrchestrator,
            CardScanOrchestrator>();
        services.AddScoped<
            CardScanWorkflowRunner>();
        services.AddScoped<
            CardScanWorkflow>();

        services.AddScoped<
            CardScanWorkflowDefinition>();

        services.AddScoped<
            IWorkflowDefinition,
            CardScanWorkflowDefinition>();

        // =========================
        // WORKFLOW STEPS
        // =========================

        services.AddScoped<
            DetectCardWorkflowStep>();

        services.AddScoped<
            CropCardWorkflowStep>();

        services.AddScoped<
            DetectCardCornersWorkflowStep>();

        services.AddScoped<
            PerspectiveCorrectionWorkflowStep>();

        services.AddScoped<
            BlurDetectionWorkflowStep>();

        services.AddScoped<
            OcrWorkflowStep>();

        services.AddScoped<
            OpenAiRecognitionWorkflowStep>();

       // services.AddScoped<
         //   ScryfallMatchWorkflowStep>();

        //services.AddScoped<
          //  CardConditionWorkflowStep>();

        services.AddScoped<
            SaveDebugImagesWorkflowStep>();

        return services;
    }
}