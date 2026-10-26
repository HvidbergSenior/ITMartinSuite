using Microsoft.Extensions.DependencyInjection;

namespace ITMartinLager.Application;

public static class DependencyInjection
{
    /// <summary>The use cases. The ports come from AddLagerInfrastructure.</summary>
    public static IServiceCollection AddLagerApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<BoxUseCases>();
        services.AddScoped<ItemUseCases>();
        services.AddScoped<RegistrationUseCases>();
        services.AddScoped<PriceHuntUseCases>();
        services.AddScoped<KolofonUseCases>();
        services.AddScoped<IntakeUseCases>();
        services.AddScoped<VareinfoLinks>();
        services.AddScoped<AuctionUseCases>();
        services.AddScoped<MagicImportUseCases>();
        services.AddScoped<BookCheckUseCases>();
        services.AddScoped<ExportUseCases>();
        services.AddScoped<GoldUseCases>();
        services.AddScoped<StockForSaleUseCases>();
        services.AddScoped<SalgLinks>();
        services.AddScoped<KioskUseCases>();
        return services;
    }
}
