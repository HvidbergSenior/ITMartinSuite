using System.Text.Json;
using ITMartinKigMed.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITMartinKigMed.Infrastructure;

// MediaMTX (container kigmed-media) knows whether path "live" is ready = Martin's video is arriving.
public sealed class MediaMtxProbe(HttpClient http, IConfiguration cfg, ILogger<MediaMtxProbe> log) : IMediaProbe
{
    private readonly string _url = (cfg["KigMed:MediaApi"] ?? "http://kigmed-media:9997") + "/v3/paths/get/live";

    public async Task<bool> VideoReadyAsync(CancellationToken ct)
    {
        try
        {
            using var res = await http.GetAsync(_url, ct);
            if (!res.IsSuccessStatusCode) return false;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("ready", out var r) && r.GetBoolean();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogDebug(ex, "MediaMTX API not reachable");
            return false;
        }
    }
}

// Runs the video check every 5 s.
public sealed class LiveVideoLoop(LiveVideo video) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await video.CheckAsync(ct); } catch (OperationCanceledException) { }
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { }
        }
    }
}

public static class KigMedInfrastructure
{
    public static IServiceCollection AddKigMedInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var dataDir = cfg["KigMed:DataDir"] ?? "/app/data";
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISessionStore>(sp => new JsonSessionStore(dataDir, sp.GetRequiredService<ILogger<JsonSessionStore>>()));
        services.AddSingleton<IPictureStore>(new FilePictureStore(dataDir));
        services.AddHttpClient<IMediaProbe, MediaMtxProbe>(c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<LiveHub>();
        services.AddSingleton<LiveVideo>();
        services.AddHostedService<LiveVideoLoop>();
        return services;
    }
}
