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
            var quarters = await eloverblik.GetQuartersAsync(s.EloverblikToken, s.MeteringPointId, to.AddDays(-days), to, ct);
            consumption.MergeQuarters(quarters);
            consumption.LastSyncError = null;
            logger.LogInformation("Synced {Count} quarter readings for household {Household}", quarters.Count, householdId);
            await RefreshChargesAsync(store, s, ct);
            return true;
        }
        catch (Exception ex)
        {
            consumption.LastSyncError = ex.Message;
            logger.LogWarning(ex, "Eloverblik sync failed for household {Household}", householdId);
            return false;
        }
    }

    // The meter's own grid company, supplier and charges - once a day is plenty (tariffs change
    // a few times a year). A slow or failing charges call never stops the consumption sync.
    private async Task RefreshChargesAsync(HouseholdStore store, HouseholdSettings s, CancellationToken ct)
    {
        if (s.Meter is { } m && DateTime.UtcNow - m.FetchedUtc < TimeSpan.FromHours(20)) return;
        try
        {
            var charges = await eloverblik.GetMeterChargesAsync(s.EloverblikToken, s.MeteringPointId, ct);
            store.Update(d => d.Settings.Meter = charges);
            logger.LogInformation("Meter charges: {Grid}, {Supplier}, {Subs} kr/md", charges.GridOperatorName, charges.SupplierName, charges.SubscriptionsKrPerMonth);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Meter charges fetch failed");
        }
    }

    // A day further back than the regular sync window: fetch its quarters on demand.
    public async Task<bool> EnsureDayAsync(Guid householdId, DateOnly date, CancellationToken ct = default)
    {
        var consumption = registry.Consumption(householdId);
        if (consumption.Quarters(date).Count >= 92) return true;
        var s = registry.Household(householdId).Get().Settings;
        if (string.IsNullOrWhiteSpace(s.EloverblikToken) || string.IsNullOrWhiteSpace(s.MeteringPointId)) return false;
        try
        {
            var quarters = await eloverblik.GetQuartersAsync(s.EloverblikToken, s.MeteringPointId, date, date, ct);
            if (quarters.Count == 0) return false;
            consumption.MergeQuarters(quarters);
            return true;
        }
        catch (Exception ex)
        {
            consumption.LastSyncError = ex.Message;
            logger.LogWarning(ex, "Eloverblik day fetch failed for {Date}", date);
            return false;
        }
    }
}
