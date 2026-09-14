namespace ITMartinElPriser.Server.Services;

// Pulls the last week of meter readings a few times a day. Datahub is
// typically 1-2 days behind, so re-fetching a whole week each time picks
// up late corrections without any bookkeeping.
public sealed class ConsumptionSync(
    EloverblikService eloverblik,
    HouseholdStore store,
    ConsumptionStore consumption,
    ILogger<ConsumptionSync> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), ct);
        while (!ct.IsCancellationRequested)
        {
            await SyncAsync(ct);
            await Task.Delay(TimeSpan.FromHours(6), ct);
        }
    }

    public async Task<bool> SyncAsync(CancellationToken ct = default, int days = 8)
    {
        var s = store.Get().Settings;
        if (string.IsNullOrWhiteSpace(s.EloverblikToken) || string.IsNullOrWhiteSpace(s.MeteringPointId)) return false;
        try
        {
            var to = DateOnly.FromDateTime(DkTime.Now).AddDays(-1);
            var readings = await eloverblik.GetHourlyAsync(s.EloverblikToken, s.MeteringPointId, to.AddDays(-days), to, ct);
            consumption.Merge(readings);
            consumption.LastSyncError = null;
            logger.LogInformation("Synced {Count} hourly readings from Eloverblik", readings.Count);
            return true;
        }
        catch (Exception ex)
        {
            consumption.LastSyncError = ex.Message;
            logger.LogWarning(ex, "Eloverblik sync failed");
            return false;
        }
    }
}
