using ITMartin.Documents.Interfaces;
using ITMartin.Documents.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartin.Documents;

public static class DependencyInjection
{
    public static IServiceCollection AddDocuments(this IServiceCollection services)
    {
        services.AddSingleton<IDocxImportService, DocxImportService>();
        return services;
    }
}
