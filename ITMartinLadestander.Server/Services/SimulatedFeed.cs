using ITMartinElPriser.Core;

namespace ITMartinLadestander.Server.Services;

// Stand-in for the Zaptec API until the association has chargers and a login: 10-12 cars from the households
// charge in the evening and at night, 5-40 kWh each, on the Zaptec outlets. The same day always gives the same
// sessions (seeded by date), so live status and history agree and pages are stable between refreshes.
public sealed class SimulatedFeed : IChargerFeed
{
    public string Name => "Simulator (ingen Spirii-nøgle endnu)";
    public bool IsSimulated => true;

    public Task<List<OutletLive>> LiveAsync(ForeningData f, CancellationToken ct)
    {
        var now = DkTime.Now;
        var live = new List<OutletLive>();
        var today = Day(f, DateOnly.FromDateTime(now)).Concat(Day(f, DateOnly.FromDateTime(now).AddDays(-1))).ToList();
        foreach (var o in f.OutletSetup.Where(o => o.Source != "manual"))
        {
            var s = today.FirstOrDefault(x => x.Outlet == o.Number && x.StartDk <= now && now < x.EndDk.AddHours(1.5));
            if (s is null) { live.Add(new OutletLive(o.Number, OutletState.Free, 0, 0, null, "")); continue; }
            var hours = (s.EndDk - s.StartDk).TotalHours;
            if (now >= s.EndDk) { live.Add(new OutletLive(o.Number, OutletState.Finished, 0, s.Kwh, s.StartDk, s.Token)); continue; }
            var done = (now - s.StartDk).TotalHours / hours;
            live.Add(new OutletLive(o.Number, OutletState.Charging, Math.Round(s.Kwh / hours, 1), Math.Round(s.Kwh * done, 1), s.StartDk, s.Token));
        }
        return Task.FromResult(live);
    }

    public Task<List<FeedSession>> SessionsAsync(ForeningData f, DateTime fromDk, DateTime toDk, CancellationToken ct)
    {
        var now = DkTime.Now;
        var list = new List<FeedSession>();
        for (var d = DateOnly.FromDateTime(fromDk).AddDays(-1); d <= DateOnly.FromDateTime(toDk); d = d.AddDays(1))
            list.AddRange(Day(f, d).Where(s => s.EndDk <= now && s.StartDk >= fromDk && s.StartDk < toDk));
        return Task.FromResult(list);
    }

    // One day's sessions: each Zaptec outlet gets 0-2 charges starting between 16:00 and 01:00.
    private static IEnumerable<FeedSession> Day(ForeningData f, DateOnly day)
    {
        var rng = new Random(day.DayNumber * 7919 + 17);
        var evCount = Math.Min(12, f.HouseholdList.Count);
        var owners = f.HouseholdList.Take(evCount).Where(h => h.Tokens.Count > 0).ToList();
        if (owners.Count == 0) yield break;
        foreach (var o in f.OutletSetup.Where(o => o.Source != "manual"))
        {
            var start = day.ToDateTime(new TimeOnly(16, 0)).AddMinutes(rng.Next(0, 9 * 60) / 15 * 15);
            for (var n = rng.Next(0, 3); n > 0; n--)
            {
                var kwh = Math.Round(5 + rng.NextDouble() * 35, 1);
                var kw = Math.Min(o.MaxKw, rng.Next(0, 3) == 0 ? 7.4 : 11);
                var end = start.AddHours(kwh / kw);
                var who = owners[rng.Next(owners.Count)].Tokens[0];
                // "Spirii's" bill: a spot-based tariff priced a little differently from our 15-minute maths.
                var billedPerKwh = 2.30 + rng.NextDouble() * 0.5;
                yield return new FeedSession($"sim-{day:yyyyMMdd}-{o.Number}-{n}", o.Number, start, end, kwh, who, Math.Round(kwh * billedPerKwh, 2));
                start = end.AddMinutes(30 + rng.Next(0, 8) * 15);
            }
        }
    }
}
