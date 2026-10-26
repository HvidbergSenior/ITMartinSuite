using System.Net.Http.Json;
using ITMartinAdmin.Application;
using ITMartinAdmin.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinAdmin.Infrastructure;

/// <summary>Asks one app GET /api/admin/status over the LAN (the apps are on the same docker network).</summary>
public sealed class HttpStatusSource(string app, HttpClient http) : IStatusSource
{
    public string App => app;

    public async Task<List<StatusLine>> LinesAsync(CancellationToken ct) =>
        (await http.GetFromJsonAsync<AppStatus>(AdminStatusApi.Path, ct))?.Lines ?? [];
}

public static class DependencyInjection
{
    /// <summary>The apps that answer the status call. Admin__StatusKey = the key every app has; the addresses can be
    /// changed with Admin__Apps__{Name} (default: the container on the photoserver).</summary>
    public static readonly (string Name, string Url)[] Apps =
    [
        ("Salg", "http://salg-web:8080"),
        ("Vareinfo", "http://vareinfo-web:8080"),
        ("Lager", "http://lager-web:8080"),
        ("Kunder", "http://kunder-web:8080"),
    ];

    public static IServiceCollection AddAdminInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var key = cfg["Admin:StatusKey"] ?? "";
        foreach (var (name, url) in Apps)
        {
            var client = $"status-{name}";
            services.AddHttpClient(client, c =>
            {
                c.BaseAddress = new Uri(cfg[$"Admin:Apps:{name}"] ?? url);
                c.Timeout = TimeSpan.FromSeconds(10);
                c.DefaultRequestHeaders.Add(AdminStatusApi.KeyHeader, key);
            });
            services.AddScoped<IStatusSource>(sp => new HttpStatusSource(name, sp.GetRequiredService<IHttpClientFactory>().CreateClient(client)));
        }
        return services;
    }
}
