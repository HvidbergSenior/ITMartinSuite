using ITMartinElPriser.Core;

namespace ITMartinMitEl.Server.Services;

// Pulls the last week of meter readings a few times a day, for every household that
// has given us a token. Datahub is typically 1-2 days behind, so re-fetching a whole
// week each time picks up late corrections without any bookkeeping.
public sealed class ConsumptionSync(
    EloverblikService eloverblik,
    HouseholdRegistry registry,
    ILogger<ConsumptionSync> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), ct);
        while (!ct.IsCancellationRequested)
        {
            await SyncAllAsync(ct);
            await Task.Delay(TimeSpan.FromHours(6), ct);
        }
    }

    public async Task SyncAllAsync(CancellationToken ct = default, int days = 8)
    {
        foreach (var householdId in registry.AllHouseholdIds())
        {
            if (ct.IsCancellationRequested) return;
            // One household's bad token must not stop the others being synced.
            await SyncAsync(householdId, ct, days);
        }
    }

    public async Task<bool> SyncAsync(Guid householdId, CancellationToken ct = default, int days = 8)
    {
        var store = registry.Household(householdId);
        var consumption = registry.Consumption(householdId);

        var s = store.Get().Settings;
        if (string.IsNullOrWhiteSpace(s.EloverblikToken) || string.IsNullOrWhiteSpace(s.MeteringPointId)) return false;

        try
        {
            var to = DateOnly.FromDateTime(DkTime.Now).AddDays(-1);
            var readings = await eloverblik.GetHourlyAsync(s.EloverblikToken, s.MeteringPointId, to.AddDays(-days), to, ct);
            consumption.Merge(readings);
            consumption.LastSyncError = null;
            logger.LogInformation("Synced {Count} hourly readings for household {Household}", readings.Count, householdId);
            return true;
        }
        catch (Exception ex)
        {
            consumption.LastSyncError = ex.Message;
            logger.LogWarning(ex, "Eloverblik sync failed for household {Household}", householdId);
            return false;
        }
    }
}
