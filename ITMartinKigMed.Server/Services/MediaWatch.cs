using System.Text.Json;

namespace ITMartinKigMed.Server.Services;

// Asks MediaMTX every 5 s whether Martin's video is actually arriving (path "live" ready).
// "Gå live" alone is not proof: a closed or refreshed studio leaves the session marked live.
// - VideoOn feeds /api/status, so itmartin.dk only says "Jeg streamer nu" while there is video.
// - No video for 10 minutes while marked live -> the session ends by itself (questions/ideas are archived as usual).
public sealed class MediaWatch(IHttpClientFactory http, IConfiguration cfg, LiveHub hub, ILogger<MediaWatch> log) : BackgroundService
{
    public static readonly TimeSpan AutoEndAfter = TimeSpan.FromMinutes(10);

    public bool VideoOn { get; private set; }
    private DateTime _lastVideo = DateTime.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var url = (cfg["KigMed:MediaApi"] ?? "http://kigmed-media:9997") + "/v3/paths/get/live";
        while (!ct.IsCancellationRequested)
        {
            var on = false;
            try
            {
                using var c = http.CreateClient("media");
                using var res = await c.GetAsync(url, ct);
                if (res.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
                    on = doc.RootElement.TryGetProperty("ready", out var r) && r.GetBoolean();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogDebug(ex, "MediaMTX API not reachable"); }

            if (on) _lastVideo = DateTime.UtcNow;
            var changed = on != VideoOn;
            VideoOn = on;

            var live = hub.Read(s => s.Live);
            if (!live) _lastVideo = DateTime.UtcNow;   // the 10 minutes count from the moment video should have been there
            else if (!on && DateTime.UtcNow - _lastVideo > AutoEndAfter)
            {
                log.LogInformation("No video for {Min} min while live - ending the session", AutoEndAfter.TotalMinutes);
                hub.EndLive();
            }
            if (changed) hub.Touch();

            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { }
        }
    }
}
