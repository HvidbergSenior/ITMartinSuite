using ITMartinElPriser.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinElPriser.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The four outside sources, each ONE instance (they cache), reachable both by its port and - for the apps
    /// that still inject the concrete class (MinElpris, Ladestander) - by its own type.</summary>
    public static IServiceCollection AddElPriserSources(this IServiceCollection services)
    {
        services.AddSingleton(sp => new ElectricityPriceService(new HttpClient(), sp.GetRequiredService<ILogger<ElectricityPriceService>>()));
        services.AddSingleton(sp => new Co2Service(new HttpClient(), sp.GetRequiredService<ILogger<Co2Service>>()));
        services.AddSingleton(sp => new SupplierCatalog(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, sp.GetRequiredService<ILogger<SupplierCatalog>>()));
        services.AddSingleton(sp => new GridTariffs(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, sp.GetRequiredService<ILogger<GridTariffs>>()));
        services.AddSingleton<IPriceSource>(sp => sp.GetRequiredService<ElectricityPriceService>());
        services.AddSingleton<ICo2Source>(sp => sp.GetRequiredService<Co2Service>());
        services.AddSingleton<ISupplierSource>(sp => sp.GetRequiredService<SupplierCatalog>());
        services.AddSingleton<IGridTariffSource>(sp => sp.GetRequiredService<GridTariffs>());
        return services;
    }
}
