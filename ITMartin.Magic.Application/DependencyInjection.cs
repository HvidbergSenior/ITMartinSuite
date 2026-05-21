using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Services;
using ITMartin.Magic.Application.Workflows;
using ITMartin.Magic.Application.Workflows.Steps;
using ITMartin.Media.Application.Pipelines.MagicScan.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Magic.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddMagicApplication(
        this IServiceCollection services)
    {
        // =========================
        // WORKFLOW
        // =========================
        services.AddScoped<
            ICardScanOrchestrator,
            CardScanOrchestrator>();
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
            IWorkflowStep<CardScanContext>,
            DetectCardWorkflowStep>();

        services.AddScoped<
            IWorkflowStep<CardScanContext>,
            DetectCardCornersWorkflowStep>();

        services.AddScoped<
            IWorkflowStep<CardScanContext>,
            PerspectiveCorrectionWorkflowStep>();

        services.AddScoped<
            IWorkflowStep<CardScanContext>,
            BlurDetectionWorkflowStep>();

        services.AddScoped<
            IWorkflowStep<CardScanContext>,
            OcrWorkflowStep>();

        services.AddScoped<
            IWorkflowStep<CardScanContext>,
            RecognitionWorkflowStep>();

        services.AddScoped<
            IWorkflowStep<CardScanContext>,
            ScryfallMatchWorkflowStep>();

        return services;
    }
}