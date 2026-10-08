using Anthropic;
using ITMartinNotatskriver.Application;
using ITMartinNotatskriver.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinNotatskriver.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Everything Notatskriver needs behind the web pages. Config:
    /// Claude:ApiKey, Notatskriver:MaxAiCallsPerDay (150), Notatskriver:KunTekst (true = the free plan).</summary>
    public static IServiceCollection AddNotatskriver(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(_ => new AnthropicClient
        {
            ApiKey = config["Claude:ApiKey"] ?? throw new InvalidOperationException("Missing Claude:ApiKey"),
        });
        services.AddSingleton<IDocumentWriter, ClaudeDocumentWriter>();
        services.AddSingleton<IAttachmentReader, AttachmentReader>();
        services.AddSingleton<IWordExporter, WordExporter>();
        services.AddSingleton<INameHiderFactory, PseudonymizerHiderFactory>();
        services.AddSingleton<IAiBudget>(sp => new DailyAiBudget(
            config.GetValue("Notatskriver:MaxAiCallsPerDay", 150),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<DailyAiBudget>>()));
        // Free unless the extended version is switched on (Notatskriver__KunTekst=false).
        services.AddSingleton(config.GetValue("Notatskriver:KunTekst", true) ? UsagePlan.Free : UsagePlan.Extended);
        services.AddSingleton<WriteDocument>();
        services.AddSingleton<AddAttachment>();
        return services;
    }
}
