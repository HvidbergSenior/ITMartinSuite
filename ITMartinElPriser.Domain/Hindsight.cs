namespace ITMartinElPriser.Domain;

/// <summary>"Hvad havde du sparet?" on the front page: the last days with real prices, the household's machine started at
/// 18:00 every day (when most people start it) against the same machine started at that day's cheapest quarter - all-in
/// with this household's tariffs. Each run is priced quarter by quarter for as long as the machine runs, like the big
/// number on the page.</summary>
public sealed record Hindsight(double SavedKr, int Days, double UsualKr, double CheapestKr)
{
    public const int UsualHour = 18;
    /// <summary>Fewer days with prices than this = not enough to say anything.</summary>
    public const int MinDays = 7;
    private const int SlotMinutes = 15;

    /// <summary>The saving per year at the same pace.</summary>
    public double PerYearKr => Days > 0 ? SavedKr / Days * 365 : 0;

    /// <summary>The days before today (today not included). raw must cover them AND today, so a run that starts late in
    /// the evening and ends after midnight is priced too. Null when there are fewer than MinDays days with prices.</summary>
    public static Hindsight? For(IReadOnlyList<PricePoint> raw, HouseholdSettings s, Appliance a, DateOnly today, int days = 30)
    {
        if (a.KwhPerRun <= 0 || a.DurationHours <= 0) return null;
        // Priced once (the old page priced every point again for every start - 30 days x 96 starts x 3,000 points).
        var points = raw.Select(p => PriceBreakdownCalculator.Compute(p, s)).GroupBy(p => p.TimeUtc).Select(g => g.First())
            .OrderBy(p => p.TimeUtc).ToList();
        var slots = Math.Max(1, (int)Math.Round(a.DurationHours * 60 / SlotMinutes));
        var kwhPerSlot = a.KwhPerRun / slots;
        var sums = new double[points.Count + 1];
        for (var i = 0; i < points.Count; i++) sums[i + 1] = sums[i] + points[i].KrPerKwh(s.ShowAllIn);

        // The cost of a run starting at each quarter, when the prices cover the whole run without a gap (by UTC, so the
        // night the clocks change is priced right too).
        var runs = new Dictionary<DateTime, double>();   // by Danish start time
        var byDay = new Dictionary<DateOnly, double>();  // the cheapest start per day
        for (var i = 0; i + slots <= points.Count; i++)
        {
            if (points[i + slots - 1].TimeUtc != points[i].TimeUtc.AddMinutes(SlotMinutes * (slots - 1))) continue;
            var cost = (sums[i + slots] - sums[i]) * kwhPerSlot;
            runs.TryAdd(points[i].TimeDk, cost);
            var day = DateOnly.FromDateTime(points[i].TimeDk);
            byDay[day] = byDay.TryGetValue(day, out var best) ? Math.Min(best, cost) : cost;
        }

        double saved = 0, usual = 0, cheapest = 0;
        var counted = 0;
        for (var d = today.AddDays(-days); d < today; d = d.AddDays(1))
        {
            if (!runs.TryGetValue(d.ToDateTime(new TimeOnly(UsualHour, 0)), out var atSix) || !byDay.TryGetValue(d, out var low)) continue;
            usual += atSix;
            cheapest += low;
            saved += atSix - low;   // 18:00 is one of the day's starts, so never below 0
            counted++;
        }
        return counted < MinDays ? null : new Hindsight(Math.Round(saved, 2), counted, Math.Round(usual, 2), Math.Round(cheapest, 2));
    }
}
