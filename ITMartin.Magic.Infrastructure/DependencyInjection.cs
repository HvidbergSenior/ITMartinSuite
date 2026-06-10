using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Infrastructure.Persistence;
using ITMartin.Magic.Infrastructure.Services;
using ITMartin.OCR.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Magic.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOpenCv(
        this IServiceCollection services,
        IConfiguration configuration)
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
        var connectionString =
            configuration.GetConnectionString(
                "MagicDb")
            ?? "Data Source=magic.db";

        services.AddDbContextFactory<MagicDbContext>(
            options =>
            {
                options.UseSqlite(
                    connectionString);
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