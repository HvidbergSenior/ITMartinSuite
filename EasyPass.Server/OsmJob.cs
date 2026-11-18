using EasyPass.Application;

namespace EasyPass.Server;

/// <summary>
/// Refreshes the OpenStreetMap places: at start if there are none yet, then once a week. The admin button starts the same
/// run in the background (an Overpass answer can take minutes - longer than Cloudflare lets a request live).
/// </summary>
public sealed class OsmJob(IServiceScopeFactory scopes, IStedStore steder, IConfiguration cfg, ILogger<OsmJob> log) : BackgroundService
{
    private int _running;
    public string Status { get; private set; } = "Ikke kørt endnu.";

    /// <summary>False = a run is already going.</summary>
    public bool Start()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1) return false;
        _ = Task.Run(() => RunAsync(CancellationToken.None));
        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (!cfg.GetValue("EasyPass:OsmAuto", true)) return;   // tests and local runs: only the admin button
        if (!steder.Alle().Any(s => s.FraOpenStreetMap) && Interlocked.Exchange(ref _running, 1) == 0)
            await RunAsync(stop);
        using var timer = new PeriodicTimer(TimeSpan.FromDays(7));
        while (await timer.WaitForNextTickAsync(stop))
            if (Interlocked.Exchange(ref _running, 1) == 0) await RunAsync(stop);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        Status = "Henter fra OpenStreetMap …";
        try
        {
            using var scope = scopes.CreateScope();
            var r = await scope.ServiceProvider.GetRequiredService<OpdaterFraOpenStreetMap>().UdfoerAsync(ct);
            Status = $"{DateTime.Now:dd.MM.yyyy HH:mm}: {r.Nye} nye, {r.Opdateret} opdateret, {r.Fjernet} fjernet, {r.EksemplerSlettet} eksempler slettet.";
            log.LogInformation("OpenStreetMap refresh: {Status}", Status);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Status = $"{DateTime.Now:dd.MM.yyyy HH:mm}: OpenStreetMap svarede ikke ({e.GetType().Name}). Stederne er uændrede - prøv igen senere.";
            log.LogWarning(e, "OpenStreetMap refresh failed");
        }
        finally { Interlocked.Exchange(ref _running, 0); }
    }
}
