using ITMartinElPriser.Core;

namespace ITMartinLadestander.Server.Services;

// What the pages read: the latest live status per outlet and when it was fetched.
public sealed class LiveBoard
{
    public IReadOnlyList<OutletLive> Outlets { get; private set; } = [];
    public DateTime? UpdatedDk { get; private set; }
    public string Source { get; private set; } = "";
    public bool Simulated { get; private set; }
    public string? Error { get; private set; }
    public event Action? Changed;

    public void Set(IReadOnlyList<OutletLive> outlets, string source, bool simulated, string? error)
    {
        Outlets = outlets; Source = source; Simulated = simulated; Error = error;
        if (error is null) UpdatedDk = DkTime.Now;
        Changed?.Invoke();
    }
}

// Background loop: live status every minute, finished sessions every 10 minutes (last 3 days; 45 days on start).
// New sessions are priced with the exact 15-minute prices they ran in and matched to a household by RFID tag.
public sealed class ChargerSync : BackgroundService
{
    private readonly IChargerFeed _feed;
    private readonly ForeningStore _store;
    private readonly LiveBoard _board;
    private readonly ElectricityPriceService _prices;
    private readonly ILogger<ChargerSync> _log;

    public ChargerSync(IChargerFeed feed, ForeningStore store, LiveBoard board, ElectricityPriceService prices, ILogger<ChargerSync> log)
    {
        _feed = feed; _store = store; _board = board; _prices = prices; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("Charger data from: {Feed}", _feed.Name);
        var lastSessions = DateTime.MinValue;
        var firstRun = true;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var f = _store.Get();
                _board.Set(await _feed.LiveAsync(f, ct), _feed.Name, _feed.IsSimulated, null);
                if (DateTime.UtcNow - lastSessions > TimeSpan.FromMinutes(10))
                {
                    await ImportSessionsAsync(DkTime.Now.AddDays(firstRun ? -45 : -3), ct);
                    lastSessions = DateTime.UtcNow;
                    firstRun = false;
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _log.LogWarning(ex, "Charger sync failed");
                _board.Set(_board.Outlets, _feed.Name, _feed.IsSimulated, ex.Message);
            }
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }

    private async Task ImportSessionsAsync(DateTime fromDk, CancellationToken ct)
    {
        var f = _store.Get();
        var known = f.Sessions.Where(s => s.ExternalId.Length > 0).Select(s => s.ExternalId).ToHashSet();
        var fresh = (await _feed.SessionsAsync(f, fromDk, DkTime.Now, ct)).Where(s => !known.Contains(s.ExternalId)).ToList();
        if (fresh.Count == 0) return;

        var raw = await _prices.GetRangeAsync(DateOnly.FromDateTime(fresh.Min(s => s.StartDk)), DateOnly.FromDateTime(DkTime.Now).AddDays(2), f.Settings.PriceArea, ct);
        var byToken = f.HouseholdList.SelectMany(h => h.Tokens.Select(t => (t, h.Id)))
            .GroupBy(x => x.t.Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var added = fresh.Select(s =>
        {
            var (cost, user) = SessionPricing.Price(raw, f, s.StartDk, s.EndDk, s.Kwh);
            return new ChargeSession
            {
                Source = _feed.IsSimulated ? "simulator" : "zaptec",
                ExternalId = s.ExternalId, Outlet = s.Outlet, StartedAt = s.StartDk, EndedAt = s.EndDk,
                PlannedKwh = s.Kwh, Kwh = s.Kwh, CostKr = cost, UserPriceKr = user, Token = s.Token,
                HouseholdId = byToken.TryGetValue(s.Token.Trim(), out var h) ? h : null,
            };
        }).ToList();
        _store.Update(d => d.Sessions.AddRange(added));
        _log.LogInformation("Imported {Count} charging sessions", added.Count);
    }
}

public static class SessionPricing
{
    // Meter cost (spot + tariffs + tax + moms, spread evenly over the 15-minute slots the charge ran in) and what
    // the household pays = meter cost + the association's tillæg incl. moms. Null when prices are missing.
    public static (double? MeterKr, double? UserKr) Price(List<PricePoint> raw, ForeningData f, DateTime startDk, DateTime endDk, double kwh)
    {
        if (kwh <= 0) return (0, 0);
        var hours = Math.Max(0.25, Math.Round((endDk - startDk).TotalHours * 4) / 4);
        var run = PriceModel.CostOfRun(raw, f.Settings, new Appliance { KwhPerRun = kwh, DurationHours = hours }, startDk);
        if (run is null) return (null, null);
        return (run.CostKr, Math.Round(run.CostKr + kwh * f.TillaegOrePerKwh / 100.0 * 1.25, 2));
    }
}
