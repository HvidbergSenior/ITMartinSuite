using ITMartinKunder.Client;
using Microsoft.Extensions.Configuration;
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
        services.AddSingleton(sp =>
        {
            // 120 s: the daily tariff list is one large Energi Data Service answer.
            var g = new GridTariffs(new HttpClient { Timeout = TimeSpan.FromSeconds(120) }, sp.GetRequiredService<ILogger<GridTariffs>>());
            g.Warm();
            return g;
        });
        services.AddSingleton<IPriceSource>(sp => sp.GetRequiredService<ElectricityPriceService>());
        services.AddSingleton<ICo2Source>(sp => sp.GetRequiredService<Co2Service>());
        services.AddSingleton<ISupplierSource>(sp => sp.GetRequiredService<SupplierCatalog>());
        services.AddSingleton<IGridTariffSource>(sp => sp.GetRequiredService<GridTariffs>());
        return services;
    }

    /// <summary>MinElpris: mail for "Glemt adgangskoden?" (from martinhvidbergnielsen@gmail.com via Gmail, see SmtpMailSender).</summary>
    public static IServiceCollection AddMinElprisMail(this IServiceCollection services)
    {
        services.AddSingleton<IMailSender, SmtpMailSender>();
        return services;
    }

    /// <summary>MinElpris: Kunder answers whether a kundenummer has the paid tick (sign-up gate, 2026-11-12).</summary>
    public static IServiceCollection AddMinElprisPaid(this IServiceCollection services, IConfiguration config)
    {
        services.AddKunderClient(config, config["DataDir"] ?? "/data");
        services.AddSingleton<IPaidCodes, KunderPaidCodes>();
        return services;
    }

    /// <summary>The free ElPriser app's own adapters: who wants a message (DataDir/subscribers.json), Web Push, PDF text,
    /// and Kunder for the kundenummer.</summary>
    public static IServiceCollection AddElPriserApp(this IServiceCollection services, IConfiguration config)
    {
        services.AddKunderClient(config, config["DataDir"] ?? "/data");
        services.AddSingleton<ICustomerCodes, KunderCodes>();
        services.AddSingleton<ISubscriberStore, SubscriberStore>();
        services.AddSingleton<IPushSender, WebPushSender>();
        services.AddSingleton<IPdfText, PdfPigText>();
        return services;
    }
}
