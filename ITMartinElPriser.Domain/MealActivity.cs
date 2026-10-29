namespace ITMartinElPriser.Domain;

// One kind of cooking device with what a normal meal on it draws. Typical values, so a meal
// can be priced the moment it is tapped together; a photographed machine replaces them.
// Words = what the camera's AI may call it ("Induktionskogeplade", "Emhætte"); Duty = the share of the plate's
// maximum watts a meal really draws (a 7.4 kW hob is never on full for 45 minutes).
public sealed record CookingKind(string Key, string Name, double KwhPerUse, double Hours, string[] Words, double Duty)
{
    public bool Matches(string name)
    {
        var n = name.ToLowerInvariant();
        if (Key == "ovn" && n.Contains("mikro")) return false;
        return Words.Any(n.Contains);
    }
}

public static class CookingKinds
{
    public static readonly CookingKind[] All =
    [
        new("ovn", "Ovn", 1.1, 1.0, ["ovn"], 0.5),
        new("kogeplader", "Kogeplader", 0.9, 0.75, ["kogeplade", "induktion", "komfur", "kogefelt"], 0.2),
        new("airfryer", "Airfryer", 0.5, 0.33, ["airfryer", "air fryer", "airfrier", "varmluftsfrituese"], 0.7),
        new("emfang", "Emfang", 0.08, 0.75, ["emfang", "emhætte", "emhaette", "udsugning"], 1.0),
        new("mikroovn", "Mikroovn", 0.2, 0.15, ["mikro"], 0.9),
        new("elkedel", "Elkedel", 0.1, 0.1, ["kedel"], 1.0),
    ];

    public static CookingKind? Find(string key) => All.FirstOrDefault(k => k.Key == key);
}

// A meal = several cooking devices used together at a usual time (user 2026-10-29: "our cooking in the evening",
// "the kids usually eat around 23"). Set up by tapping kinds and times - never typed numbers (camera-only rule).
// Window = the hours the meal could realistically move within: dinner stays in the evening, never "cook at 03".
public sealed class MealActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public List<string> Kinds { get; set; } = [];
    public int StartHour { get; set; }
    public int StartMinute { get; set; }
    public int WindowFromHour { get; set; }
    public int WindowToHour { get; set; }
    public int DaysPerWeek { get; set; } = 7;

    public static List<MealActivity> Defaults() =>
    [
        new() { Name = "Aftensmad", Kinds = ["ovn", "kogeplader", "emfang"], StartHour = 17, StartMinute = 30, WindowFromHour = 16, WindowToHour = 20 },
        new() { Name = "Børnenes sene mad", Kinds = ["airfryer"], StartHour = 23, StartMinute = 0, WindowFromHour = 21, WindowToHour = 24 },
    ];
}

// What one meal costs on one day: at the usual time, and at the cheapest start inside its window.
public sealed record MealPlan(double KwhPerMeal, double Hours, RunOption? Usual, RunOption? Cheapest)
{
    public double SavingKr => Usual is { } u && Cheapest is { } c ? Math.Max(0, u.CostKr - c.CostKr) : 0;
}

// What the meter says the meal really used over the last weeks: the household's extra draw inside the window,
// priced quarter by quarter at that quarter's real price, next to the same kWh at the window's cheapest start.
public sealed record MealMeterReport(int Days, double KwhPerDay, double RealKrPerDay, double PerfectKrPerDay, TimeOnly? UsualStart)
{
    public int PerfectPercent => RealKrPerDay <= 0 ? 100 : (int)Math.Round(100 * Math.Min(1, PerfectKrPerDay / RealKrPerDay));
}

public static class MealPlanner
{
    private const int SlotMinutes = 15;
    // A quarter this far over the day's base load (1.2 kW) counts as "cooking started".
    private const double StartKwhPerQuarter = 0.3;

    // The meal as one run: all kinds' energy over the longest kind's time. A photographed machine
    // (an appliance or device with the kind's name) replaces the typical value.
    public static Appliance AsRun(MealActivity m, IEnumerable<Appliance> own, IEnumerable<Device> devices)
    {
        double kwh = 0, hours = 0;
        foreach (var k in m.Kinds.Select(CookingKinds.Find).OfType<CookingKind>())
        {
            var a = own.FirstOrDefault(x => x.Photo is not null && k.Matches(x.Name) && x.KwhPerRun > 0);
            var d = devices.FirstOrDefault(x => x.Photo is not null && k.Matches(x.Name) && x.Watts > 0);
            var h = a?.DurationHours ?? k.Hours;
            kwh += a?.KwhPerRun ?? (d is not null ? d.Watts / 1000.0 * k.Hours * k.Duty : k.KwhPerUse);
            hours = Math.Max(hours, h);
        }
        return new Appliance { Name = m.Name, KwhPerRun = Math.Max(0.001, kwh), DurationHours = Math.Max(0.25, hours) };
    }

    public static MealPlan Plan(List<PricePoint> raw, HouseholdSettings s, MealActivity m, Appliance run, DateOnly day)
    {
        var usual = PriceModel.CostOfRun(raw, s, run, day.ToDateTime(new TimeOnly(m.StartHour % 24, m.StartMinute)));
        return new MealPlan(run.KwhPerRun, run.DurationHours, usual, CheapestInWindow(raw, s, m, run, day));
    }

    // Cheapest start where the whole meal still ends inside the window. Earliest wins a tie.
    public static RunOption? CheapestInWindow(List<PricePoint> raw, HouseholdSettings s, MealActivity m, Appliance run, DateOnly day)
    {
        RunOption? best = null;
        var end = day.ToDateTime(TimeOnly.MinValue).AddHours(m.WindowToHour);
        for (var t = day.ToDateTime(TimeOnly.MinValue).AddHours(m.WindowFromHour); t.AddHours(run.DurationHours) <= end; t = t.AddMinutes(SlotMinutes))
            if (PriceModel.CostOfRun(raw, s, run, t) is { } o && (best is null || o.CostKr < best.CostKr - 0.005)) best = o;
        return best;
    }

    // Today while the meal is still ahead, else tomorrow once its prices are known.
    public static DateOnly NextDay(MealActivity m, List<PricePoint> raw, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var start = today.ToDateTime(new TimeOnly(m.StartHour % 24, m.StartMinute));
        var tomorrowKnown = raw.Count > 0 && DateOnly.FromDateTime(raw.Max(p => p.TimeDk)) > today;
        return start > now || !tomorrowKnown ? today : today.AddDays(1);
    }

    // Half-hour starts where the whole meal still ends inside its window - the times that can be tapped.
    public static List<TimeOnly> Starts(MealActivity m, Appliance run)
    {
        var list = new List<TimeOnly>();
        for (var t = m.WindowFromHour * 60; t + run.DurationHours * 60 <= m.WindowToHour * 60; t += 30)
            list.Add(new TimeOnly(t / 60 % 24, t % 60));
        return list;
    }

    // quarters = the meter's kWh per quarter (any days), prices = the real prices for the same days.
    // Base load per day = its 25 % quietest quarter; everything above it inside the window is the meal.
    public static MealMeterReport? FromMeter(IReadOnlyDictionary<DateTime, double> quarters, List<PricePoint> prices, HouseholdSettings s, MealActivity m, double mealHours)
    {
        // Keyed by Danish wall-clock time: the night the clocks go back has 02:00-02:45 twice - the first one is kept.
        var priced = new Dictionary<DateTime, double>();
        foreach (var p in prices) priced.TryAdd(p.TimeDk, PriceBreakdownCalculator.Compute(p, s).KrPerKwh(s.ShowAllIn));
        int days = 0;
        double kwhSum = 0, realSum = 0, perfectSum = 0;
        var starts = new List<int>();
        foreach (var g in quarters.GroupBy(q => DateOnly.FromDateTime(q.Key)))
        {
            if (g.Count() < 90) continue; // a day the meter has not delivered in full
            var sorted = g.Select(q => q.Value).OrderBy(v => v).ToList();
            var baseLoad = sorted[sorted.Count / 4];
            var from = g.Key.ToDateTime(TimeOnly.MinValue).AddHours(m.WindowFromHour);
            var to = g.Key.ToDateTime(TimeOnly.MinValue).AddHours(m.WindowToHour);
            var window = g.Where(q => q.Key >= from && q.Key < to).OrderBy(q => q.Key).ToList();
            if (window.Any(q => !priced.ContainsKey(q.Key))) continue;
            double kwh = 0, real = 0;
            foreach (var q in window)
            {
                var extra = Math.Max(0, q.Value - baseLoad);
                kwh += extra;
                real += extra * priced[q.Key];
            }
            var first = window.FirstOrDefault(q => q.Value - baseLoad >= StartKwhPerQuarter);
            if (first.Key != default) starts.Add(first.Key.Hour * 60 + first.Key.Minute);
            var perfect = kwh <= 0 ? 0
                : CheapestInWindow(prices, s, m, new Appliance { KwhPerRun = kwh, DurationHours = Math.Max(0.25, mealHours) }, g.Key)?.CostKr ?? real;
            days++;
            kwhSum += kwh;
            realSum += real;
            perfectSum += Math.Min(real, perfect);
        }
        if (days == 0) return null;
        TimeOnly? usual = null;
        if (starts.Count > 0)
        {
            var mid = starts.OrderBy(x => x).ElementAt(starts.Count / 2) / SlotMinutes * SlotMinutes;
            usual = new TimeOnly(mid / 60, mid % 60);
        }
        return new MealMeterReport(days, kwhSum / days, realSum / days, perfectSum / days, usual);
    }

}
