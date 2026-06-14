using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Infrastructure.BackgroundJobs;
using ITMartin.Magic.Infrastructure.Persistence;
using ITMartin.Magic.Infrastructure.Services;
using ITMartin.Magic.Infrastructure.Workflows;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Infrastructure.DependencyInjection;
using ITMartin.OCR.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Magic.Infrastructure;

public static class MagicDependencyInjection
{
    public static IServiceCollection AddMagicInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("MagicDb")
            ?? "Data Source=magic.db";

        services.AddMagicPersistence(
            connectionString);
        services.AddScoped<IBackgroundJobHandler, ProcessMediaHandler>();
        services.AddHttpClient<
            IScryfallService,
            ScryfallService>(client =>
        {
            client.BaseAddress =
                new Uri("https://api.scryfall.com/");

            client.DefaultRequestHeaders.Add(
                "User-Agent",
                "ITMartin-MagicScanner/1.0");

            client.DefaultRequestHeaders.Add(
                "Accept",
                "application/json");
        });
        services.AddScoped<
            IPrintingEliminationService,
            PrintingEliminationService>();
        services.AddScoped<
            IMagicSetKnowledgeService,
            MagicSetKnowledgeService>();
        services.AddScoped<
            IMagicKnowledgeService,
            MagicKnowledgeService>();
        services.AddHttpClient<
            IMagicSetImportService,
            MagicSetImportService>(client =>
        {
            client.BaseAddress =
                new Uri("https://api.scryfall.com/");

            client.DefaultRequestHeaders.Add(
                "User-Agent",
                "ITMartin-MagicScanner/1.0");

            client.DefaultRequestHeaders.Add(
                "Accept",
                "application/json");
        });
        services.AddScoped<
            IWorkflowCheckpointStore,
            NullWorkflowCheckpointStore>();

        services.AddScoped<
            IWorkflowStepExecutionStore,
            NullWorkflowStepExecutionStore>();

        services.AddScoped<
            IWorkflowInstanceStore,
            NullWorkflowInstanceStore>();
        services.AddScoped<
            ICardMatchScoringService,
            CardMatchScoringService>();

        services.AddScoped<
            ISetSymbolMatchingService,
            SetSymbolMatchingService>();

        services.AddScoped<
            IOcrRegionExtractor,
            OpenCvMagicCardOcrRegionExtractor>();

        return services;
    }
}