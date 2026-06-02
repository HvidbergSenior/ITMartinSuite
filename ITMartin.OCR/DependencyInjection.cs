using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.OCR;

public static class DependencyInjection
{
    public static IServiceCollection AddOcr(
        this IServiceCollection services)
    {
        services.AddScoped<
            IGeneralOcrService,
            GeneralOcrService>();

        services.AddScoped<
            IOcrService,
            OcrService>();

        return services;
    }
}