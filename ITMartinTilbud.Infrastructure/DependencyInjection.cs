using ITMartinTilbud.Application;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinTilbud.Infrastructure;

public static class DependencyInjection
{
    private const string UserAgent = "ITMartinTilbud/1.0 (ITMartin@Mensa.dk)";

    /// <summary>Use cases + adapters. Config: Tilbud:SallingKey (madspild off without it), Tilbud:DataDir (faste tilbud).</summary>
    public static IServiceCollection AddTilbud(this IServiceCollection services, IConfiguration config)
    {
        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);
        services.AddHttpClient<IOfferSource, TjekClient>(c => { c.Timeout = TimeSpan.FromSeconds(30); c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent); });
        services.AddHttpClient<IPlaceLookup, NominatimPlaces>(c => { c.Timeout = TimeSpan.FromSeconds(15); c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent); });
        services.AddHttpClient("salling", c => { c.Timeout = TimeSpan.FromSeconds(20); c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent); });
        // One instance: it counts Salling's daily calls.
        services.AddSingleton<IFoodWasteSource>(sp => new SallingFoodWaste(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("salling"), sp.GetRequiredService<IMemoryCache>(),
            sp.GetRequiredService<TimeProvider>(), config["Tilbud:SallingKey"] ?? "", sp.GetRequiredService<ILogger<SallingFoodWaste>>()));
        services.AddSingleton<IFixedDealStore>(_ => new JsonFixedDealStore(config["Tilbud:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data")));

        services.AddTransient<SearchOffers>();
        services.AddTransient<ThisWeeksAppGap>();
        services.AddTransient<NearbyFoodWaste>();
        services.AddSingleton<FixedDealsBoard>();
        return services;
    }
}
