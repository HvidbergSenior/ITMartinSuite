using ITMartinElPriser.Application;
using ITMartinElPriser.Infrastructure;
using ITMartinElPriser.Core;

namespace ITMartinElPriser.Server.Services;

// Every few minutes: find today's cheapest (and dearest) two-hour window for
// each subscriber's own region and tariffs, and tell them an hour before it
// starts. Two hours is a wash or a dishwasher cycle - the thing people are
// actually waiting to start. Subscribers with identical settings share one
// price calculation.
//
// Sends are logged per subscriber+kind+day so a restart or a second tick in
// the same window never sends twice.
public sealed class NotificationScheduler(
    IPriceSource prices,
    SubscriberStore store,
    PushService push,
    IGridTariffSource tariffs,
    ILogger<NotificationScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LeadTime = TimeSpan.FromHours(1);

    // The generic "one wash": 1 kWh over two hours. Only the window matters
    // for the message; the kr amount is shown per kWh.
    public static readonly Appliance Window = new() { Name = "Vask", Icon = "🧺", KwhPerRun = 1.0, DurationHours = 2 };

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), ct);
        while (!ct.IsCancellationRequested)
        {
            try { await RunOnceAsync(DkTime.Now, ct); }
            catch (Exception ex) { logger.LogError(ex, "Notification tick failed"); }
            await Task.Delay(Tick, ct);
        }
    }

    public async Task RunOnceAsync(DateTime now, CancellationToken ct)
    {
        var subs = store.Get().Subscribers.ToList();
        if (subs.Count == 0) return;

        foreach (var group in subs.GroupBy(s => SettingsKey(s.Settings)))
        {
            var settings = group.First().Settings;
            await tariffs.AttachAsync(settings);
            var raw = await prices.GetPricesAsync(settings.PriceArea, ct);
            var snap = PriceModel.Build(raw, settings, [Window], now);
            if (!snap.HasData || snap.Appliances.Count == 0) continue;

            var w = snap.Appliances[0];
            var unit = snap.AllIn ? "kr/kWh alt inkl." : "kr/kWh spot";

            var cheapest = w.CheapestToday is { } t && t.Start > now ? t : w.CheapestTomorrow;
            if (cheapest is not null && Due(cheapest.Start, now))
            {
                var body = $"Kl. {cheapest.Start:HH:mm}–{cheapest.End:HH:mm} er dagens billigste to timer: {cheapest.AvgKrPerKwh:0.00} {unit}. En vask (1 kWh) koster {cheapest.CostKr:0.00} kr.";
                await Broadcast(group, "cheapest", cheapest.Start, s => s.NotifyCheapest,
                    new PushService.Message("Billigste strøm om en time ⚡", body), ct);
            }

            var dearest = w.DearestUpcoming;
            if (dearest is not null && Due(dearest.Start, now))
            {
                var body = $"Kl. {dearest.Start:HH:mm}–{dearest.End:HH:mm} er dagens dyreste: {dearest.AvgKrPerKwh:0.00} {unit}"
                         + (cheapest is not null ? $" mod {cheapest.AvgKrPerKwh:0.00} kl. {cheapest.Start:HH:mm}." : ".");
                await Broadcast(group, "dearest", dearest.Start, s => s.NotifyExpensive,
                    new PushService.Message("Dyr strøm om en time 💸", body), ct);
            }
        }
    }

    private static string SettingsKey(HouseholdSettings s) =>
        $"{s.PriceArea}|{s.ShowAllIn}|{s.GridCompanyId}|{s.GridSupplierId}|{s.CustomNettarifOre}|{s.SupplierId}|{s.CustomTillaegOre}|{s.QuietFromHour}|{s.QuietToHour}";

    // Fire in the tick that lands 60-65 minutes before the start. With a
    // 5-minute tick this is exactly one tick; the sent-log covers the rest.
    private static bool Due(DateTime start, DateTime now)
    {
        var ahead = start - now;
        return ahead <= LeadTime && ahead > LeadTime - Tick;
    }

    private async Task Broadcast(IEnumerable<PushSubscriber> group, string kind, DateTime windowStart, Func<PushSubscriber, bool> wants, PushService.Message msg, CancellationToken ct)
    {
        var key = $"{kind}|{windowStart:yyyy-MM-dd HH:mm}";
        var targets = group.Where(wants).ToList();
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
