namespace ITMartinElPriser.Core;

// "Sæt timeren": the cheapest contiguous start tonight for something that
// runs unattended - a dishwasher on a timer, an EV charger. Night is
// 22:00-07:00. Unlike PriceModel this ignores the quiet hours on purpose:
// nobody needs to be awake for it.
public sealed record NightWindow(DateTime Start, DateTime End, double AvgKrPerKwh, double CostKr);

public sealed class NightPlan
{
    public required DateOnly Night { get; init; }        // the date the night starts on
    public required bool Complete { get; init; }         // false = tomorrow not published, only part of the night known
    public required List<(double Hours, NightWindow? Best)> Windows { get; init; }
    public NightWindow? Ev { get; init; }                // cheapest start for the EV charge
    public NightWindow? EvFromHome { get; init; }        // the same charge started at 17:00 (the plug-in-when-home habit)
    public NightWindow? EvAllNight { get; init; }        // the same charge started 22:00
    public double EvKwh { get; init; }
    public double EvKw { get; init; }
}

public static class NightPlanner
{
    private const int SlotMinutes = 15;
    public static readonly double[] StandardHours = [2, 4, 8];

    public static NightPlan Plan(List<PricePoint> raw, HouseholdSettings s, DateTime now, double evKwh = 30, double evKw = 11)
    {
        var points = raw.Select(p => PriceBreakdownCalculator.Compute(p, s)).OrderBy(p => p.TimeDk).ToList();
        var allIn = s.ShowAllIn;

        // Before 07:00 "tonight" is the night already running; otherwise the coming one.
        var nightDate = now.Hour < 7 ? DateOnly.FromDateTime(now).AddDays(-1) : DateOnly.FromDateTime(now);
        var nightStart = nightDate.ToDateTime(new TimeOnly(22, 0));
        var nightEnd = nightDate.AddDays(1).ToDateTime(new TimeOnly(7, 0));
        var complete = points.Any(p => p.TimeDk >= nightEnd.AddMinutes(-SlotMinutes));

        var windows = StandardHours
            .Select(h => (h, Best(points, nightStart, nightEnd, h, 1.0, now, allIn)))
            .ToList();

        var evHours = evKw > 0 ? Math.Min(evKwh / evKw, 9) : 0;
        NightWindow? ev = null, evHome = null, evAll = null;
        if (evHours > 0 && evKwh > 0)
        {
            ev = Best(points, nightStart, nightEnd, evHours, evKwh, now, allIn);
            evHome = At(points, nightDate.ToDateTime(new TimeOnly(17, 0)), evHours, evKwh, allIn);
            evAll = At(points, nightStart, evHours, evKwh, allIn);
        }

        return new NightPlan
        {
            Night = nightDate, Complete = complete, Windows = windows,
            Ev = ev, EvFromHome = evHome, EvAllNight = evAll, EvKwh = evKwh, EvKw = evKw,
        };
    }

    // Cheapest contiguous run of `hours` that starts inside [from, to) and
    // has not started yet; the run may end after `to`.
    private static NightWindow? Best(List<PricedPoint> points, DateTime from, DateTime to, double hours, double kwh, DateTime now, bool allIn)
    {
        NightWindow? best = null;
        for (var t = from; t < to; t = t.AddMinutes(SlotMinutes))
        {
            if (t.AddMinutes(SlotMinutes) <= now) continue;
            var w = At(points, t, hours, kwh, allIn);
            if (w is not null && (best is null || w.CostKr < best.CostKr)) best = w;
        }
        return best;
    }

    // Price one run from `start`; null if the curve does not cover it all.
    public static NightWindow? At(List<PricedPoint> points, DateTime start, double hours, double kwh, bool allIn)
    {
        var slots = Math.Max(1, (int)Math.Round(hours * 60 / SlotMinutes));
        var i = points.FindIndex(p => p.TimeDk == start);
        if (i < 0 || i + slots > points.Count) return null;
        var run = points.Skip(i).Take(slots).ToList();
        if (run[^1].TimeDk != start.AddMinutes(SlotMinutes * (slots - 1))) return null;
        var avg = run.Average(p => p.KrPerKwh(allIn));
        return new NightWindow(start, start.AddMinutes(SlotMinutes * slots), Math.Round(avg, 3), Math.Round(avg * kwh, 2));
    }
}
