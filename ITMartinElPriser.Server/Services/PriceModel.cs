namespace ITMartinElPriser.Server.Services;

public sealed record HourBar(DateTime Start, double KrPerKwh, bool IsNow, bool IsPast, double Rank01);

// Cost of running one appliance from a given start: every 15-minute slot the
// machine is on contributes its share of the run's kWh at that slot's price.
public sealed record RunOption(DateTime Start, DateTime End, double CostKr, double AvgKrPerKwh);

public sealed class ApplianceView
{
    public required Appliance Appliance { get; init; }
    public RunOption? Now { get; init; }
    public RunOption? CheapestToday { get; init; }
    public RunOption? CheapestTomorrow { get; init; }
    public RunOption? DearestUpcoming { get; init; }
}

public sealed class DayView
{
    public required DateOnly Date { get; init; }
    public required List<HourBar> Hours { get; init; }
    public required double Min { get; init; }
    public required double Max { get; init; }
    public required double Avg { get; init; }
    public HourBar? Cheapest => Hours.MinBy(h => h.KrPerKwh);
    public HourBar? Dearest => Hours.MaxBy(h => h.KrPerKwh);
}

public sealed class PriceSnapshot
{
    public required DateTime At { get; init; }
    public required bool AllIn { get; init; }
    public required bool HasData { get; init; }
    public double? NowKrPerKwh { get; init; }
    // 0 = cheapest slot of the day, 1 = dearest - drives the colour of the big number.
    public double? NowRank01 { get; init; }
    public DayView? Today { get; init; }
    public DayView? Tomorrow { get; init; }
    public List<ApplianceView> Appliances { get; init; } = [];
}

public static class PriceModel
{
    private const int SlotMinutes = 15;

    public static PriceSnapshot Build(List<PricePoint> raw, HouseholdData household, DateTime now)
    {
        var s = household.Settings;
        var allIn = s.ShowAllIn;
        var points = raw.Select(p => PriceBreakdownCalculator.Compute(p, s)).OrderBy(p => p.TimeDk).ToList();
        if (points.Count == 0)
            return new PriceSnapshot { At = now, AllIn = allIn, HasData = false };

        var today = DateOnly.FromDateTime(now);
        var tomorrow = today.AddDays(1);

        var todayView = BuildDay(points, today, now, allIn);
        var tomorrowView = BuildDay(points, tomorrow, now, allIn);

        var slotNow = points.LastOrDefault(p => p.TimeDk <= now);
        double? nowPrice = slotNow?.KrPerKwh(allIn);
        double? rank = null;
        if (nowPrice is { } np && todayView is { } tv && tv.Max > tv.Min)
            rank = (np - tv.Min) / (tv.Max - tv.Min);

        var appliances = household.Appliances.OrderBy(a => a.SortOrder)
            .Select(a => BuildAppliance(a, points, s, now, allIn))
            .ToList();

        return new PriceSnapshot
        {
            At = now,
            AllIn = allIn,
            HasData = true,
            NowKrPerKwh = nowPrice,
            NowRank01 = rank,
            Today = todayView,
            Tomorrow = tomorrowView,
            Appliances = appliances,
        };
    }

    private static DayView? BuildDay(List<PricedPoint> points, DateOnly date, DateTime now, bool allIn)
    {
        var day = points.Where(p => DateOnly.FromDateTime(p.TimeDk) == date).ToList();
        if (day.Count < 4) return null; // nothing published yet for that day

        var hours = day.GroupBy(p => p.TimeDk.Hour)
            .Select(g => (Start: date.ToDateTime(new TimeOnly(g.Key, 0)), Price: g.Average(p => p.KrPerKwh(allIn))))
            .OrderBy(h => h.Start)
            .ToList();
        var min = hours.Min(h => h.Price);
        var max = hours.Max(h => h.Price);

        var bars = hours.Select(h => new HourBar(
            h.Start,
            Math.Round(h.Price, 3),
            IsNow: now >= h.Start && now < h.Start.AddHours(1),
            IsPast: h.Start.AddHours(1) <= now,
            Rank01: max > min ? (h.Price - min) / (max - min) : 0.5)).ToList();

        return new DayView { Date = date, Hours = bars, Min = min, Max = max, Avg = hours.Average(h => h.Price) };
    }

    private static ApplianceView BuildAppliance(Appliance a, List<PricedPoint> points, HouseholdSettings s, DateTime now, bool allIn)
    {
        var slots = Math.Max(1, (int)Math.Round(a.DurationHours * 60 / SlotMinutes));
        var kwhPerSlot = a.KwhPerRun / slots;

        // Every start on the 15-minute grid from now on, priced across the full run.
        var options = new List<RunOption>();
        for (var i = 0; i + slots <= points.Count; i++)
        {
            var start = points[i].TimeDk;
            if (start.AddMinutes(SlotMinutes) <= now) continue; // the current slot counts as "now"
            var run = points.Skip(i).Take(slots).ToList();
            // A gap in the data (tomorrow not published yet) must not be
            // bridged silently - only contiguous slots count.
            if (run[^1].TimeDk != start.AddMinutes(SlotMinutes * (slots - 1))) continue;
            var cost = run.Sum(p => p.KrPerKwh(allIn) * kwhPerSlot);
            options.Add(new RunOption(start, start.AddHours(a.DurationHours), Math.Round(cost, 2), Math.Round(cost / a.KwhPerRun, 3)));
        }

        var today = DateOnly.FromDateTime(now);
        bool Awake(RunOption o) => !IsQuiet(o.Start, s);

        return new ApplianceView
        {
            Appliance = a,
            Now = options.FirstOrDefault(),
            CheapestToday = options.Where(o => DateOnly.FromDateTime(o.Start) == today && Awake(o)).MinBy(o => o.CostKr),
            CheapestTomorrow = options.Where(o => DateOnly.FromDateTime(o.Start) == today.AddDays(1) && Awake(o)).MinBy(o => o.CostKr),
            DearestUpcoming = options.Where(o => DateOnly.FromDateTime(o.Start) == today).MaxBy(o => o.CostKr),
        };
    }

    public static bool IsQuiet(DateTime t, HouseholdSettings s)
    {
        var h = t.Hour;
        if (s.QuietFromHour == s.QuietToHour) return false;
        return s.QuietFromHour < s.QuietToHour
            ? h >= s.QuietFromHour && h < s.QuietToHour
            : h >= s.QuietFromHour || h < s.QuietToHour;
    }
}
