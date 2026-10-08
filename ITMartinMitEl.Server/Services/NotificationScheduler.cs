using ITMartinElPriser.Infrastructure;
using System.Globalization;
using ITMartinElPriser.Core;

namespace ITMartinMitEl.Server.Services;

// Every few minutes: look at today's cheapest (and dearest) window and, if
// it starts about an hour from now, tell everyone who asked. The "window"
// is the first appliance's run length (normally the washing machine, 2 h)
// so the message matches what people actually want to start.
//
// Sends are logged per subscriber+kind+day in the household, so a restart
// or a second tick in the same window never sends twice. Since accounts arrived
// the tick runs for every household - each has its own price area, appliances
// and subscribers.
public sealed class NotificationScheduler(
    ElectricityPriceService prices,
    HouseholdRegistry registry,
    PushService push,
    ILogger<NotificationScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LeadTime = TimeSpan.FromHours(1);
    private static readonly CultureInfo Da = new("da-DK");

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Let the web host come up first
        await Task.Delay(TimeSpan.FromSeconds(20), ct);
        while (!ct.IsCancellationRequested)
        {
            try { await RunAllAsync(DkTime.Now, ct); }
            catch (Exception ex) { logger.LogError(ex, "Notification tick failed"); }
            await Task.Delay(Tick, ct);
        }
    }

    public async Task RunAllAsync(DateTime now, CancellationToken ct)
    {
        foreach (var householdId in registry.AllHouseholdIds())
        {
            if (ct.IsCancellationRequested) return;
            try { await RunOnceAsync(householdId, now, ct); }
            catch (Exception ex) { logger.LogError(ex, "Notification tick failed for household {Household}", householdId); }
        }
    }

    public async Task RunOnceAsync(Guid householdId, DateTime now, CancellationToken ct)
    {
        var store = registry.Household(householdId);
        var data = store.Get();
        if (data.Subscribers.Count == 0) return;

        var raw = await prices.GetPricesAsync(data.Settings.PriceArea, ct);
        var snap = PriceModel.Build(raw, data.Settings, data.Appliances, now);
        if (!snap.HasData || snap.Appliances.Count == 0) return;

        var lead = snap.Appliances[0];
        var unit = snap.AllIn ? "kr/kWh alt inkl." : "kr/kWh spot";

        // Cheapest run today that has not started yet; also consider tomorrow's
        // when today's is already behind us (late evening), so a 00:30 start
        // still gets its 23:30 heads-up.
        var cheapest = Pick(lead.CheapestToday, lead.CheapestTomorrow, now);
        if (cheapest is not null && Due(cheapest.Start, now))
        {
            var others = string.Join(", ", snap.Appliances.Skip(1).Take(2)
                .Select(a => (a.CheapestToday ?? a.CheapestTomorrow) is { } o ? $"{a.Appliance.Name.ToLower(Da)} {o.CostKr:0.00} kr" : null)
                .Where(x => x is not null));
            var body = $"Kl. {cheapest.Start:HH:mm}–{cheapest.End:HH:mm}: {lead.Appliance.Name.ToLower(Da)} koster {cheapest.CostKr:0.00} kr ({cheapest.AvgKrPerKwh:0.00} {unit})."
                     + (others.Length > 0 ? $" Også {others}." : "");
            await Broadcast(store, "cheapest", cheapest.Start, s => s.NotifyCheapest,
                new PushService.Message("Billigste strøm om en time ⚡", body), ct);
        }

        var dearest = lead.DearestUpcoming;
        if (dearest is not null && Due(dearest.Start, now))
        {
            var body = $"Kl. {dearest.Start:HH:mm}–{dearest.End:HH:mm} er dagens dyreste: {lead.Appliance.Name.ToLower(Da)} ville koste {dearest.CostKr:0.00} kr"
                     + (cheapest is not null ? $" mod {cheapest.CostKr:0.00} kr kl. {cheapest.Start:HH:mm}." : ".");
            await Broadcast(store, "dearest", dearest.Start, s => s.NotifyExpensive,
                new PushService.Message("Dyr strøm om en time 💸", body), ct);
        }
    }

    private static RunOption? Pick(RunOption? today, RunOption? tomorrow, DateTime now)
    {
        if (today is not null && today.Start > now) return today;
        return tomorrow;
    }

    // Fire in the tick that lands 60-65 minutes before the start. With a
    // 5-minute tick this is exactly one tick; the sent-log covers the rest.
    private static bool Due(DateTime start, DateTime now)
    {
        var ahead = start - now;
        return ahead <= LeadTime && ahead > LeadTime - Tick;
    }

    private async Task Broadcast(HouseholdStore store, string kind, DateTime windowStart, Func<PushSubscriber, bool> wants, PushService.Message msg, CancellationToken ct)
    {
        var key = $"{kind}|{windowStart:yyyy-MM-dd HH:mm}";
        var targets = store.Get().Subscribers.Where(wants).ToList();
        foreach (var sub in targets)
        {
            var logKey = $"{sub.Id}|{key}";
            if (store.Get().SentPushes.Contains(logKey)) continue;
            store.Update(d => d.SentPushes.Add(logKey));
            await push.SendAsync(sub, msg, ct);
        }
        if (targets.Count > 0) logger.LogInformation("Sent {Kind} push for {Start} to {Count} subscriber(s)", kind, windowStart, targets.Count);
    }
}
