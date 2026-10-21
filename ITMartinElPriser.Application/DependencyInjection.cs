using Microsoft.Extensions.DependencyInjection;

namespace ITMartinElPriser.Application;

public static class DependencyInjection
{
    /// <summary>The free app's use cases. The ports come from AddElPriserSources/AddElPriserApp (and the Server's cookie prefs).</summary>
    public static IServiceCollection AddElPriserApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<GetPriceSnapshot>();
        services.AddSingleton<PushUseCases>();
        services.AddSingleton<NotifyUseCases>();
        services.AddSingleton<BillCheckUseCases>();
        services.AddScoped<FrontPageUseCases>();
        services.AddScoped<MonthBillUseCases>();
        return services;
    }
}
