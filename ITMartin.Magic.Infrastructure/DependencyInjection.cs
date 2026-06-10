using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Infrastructure.Services;
using ITMartin.OCR.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Magic.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOpenCv(
        this IServiceCollection services)
    {
        services.AddScoped<
            IScryfallService, ScryfallService>();
        services.AddHttpClient<
            IScryfallService,
            ScryfallService>(client =>
        {
            client.BaseAddress =
                new Uri(
                    "https://api.scryfall.com/");

            client.DefaultRequestHeaders.Add(
                "User-Agent",
                "ITMartin-MagicScanner/1.0");

            client.DefaultRequestHeaders.Add(
                "Accept",
                "application/json");
        });
      

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