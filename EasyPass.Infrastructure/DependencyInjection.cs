using EasyPass.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyPass.Infrastructure;

public static class DependencyInjection
{
    /// <summary>EasyPass__DataDir = the data folder (default ./data next to the app): steder.json + tilbagemeldinger.json.</summary>
    public static IServiceCollection AddEasyPass(this IServiceCollection services, IConfiguration config)
    {
        var dir = config["EasyPass:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
        var steder = new JsonStedStore(dir);
        if (steder.Tom) steder.GemMange(Eksempler.Alle());

        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        // OpenStreetMap's services ask for a real User-Agent with a way to reach the operator.
        const string ua = "ITMartin-EasyPass/1.0 (+https://easypass.itmartin.dk/om)";
        services.AddHttpClient<IOpenStreetMapKilde, OverpassKilde>(c => { c.Timeout = TimeSpan.FromMinutes(6); c.DefaultRequestHeaders.UserAgent.ParseAdd(ua); });
        services.AddHttpClient<IByOpslag, NominatimByOpslag>(c => { c.Timeout = TimeSpan.FromSeconds(10); c.DefaultRequestHeaders.UserAgent.ParseAdd(ua); });
        services.AddTransient<OpdaterFraOpenStreetMap>();
        services.AddSingleton<IStedStore>(steder);
        services.AddSingleton<ITilbagemeldingStore>(new JsonTilbagemeldingStore(dir));
        services.AddTransient<FindSteder>();
        services.AddTransient<VisSted>();
        services.AddTransient<SendTilbagemelding>();
        services.AddTransient<StedAdmin>();
        services.AddTransient<ImportSteder>();
        return services;
    }
}
