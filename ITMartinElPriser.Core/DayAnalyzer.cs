namespace ITMartinElPriser.Core;

// "What did we use on that day, and what did it cost?" from the meter's own quarter-hours.
// The meter only knows the whole house, so the machines are worked out from the shape:
// the always-on base is taken out, every stretch above it becomes an event, and each event
// is matched against what the household has told us - a logged run beats a matching
// machine beats a matching device beats a guess from size and time of day. Every event
// says how sure it is.
public static class DayAnalyzer
{
    public enum Certainty { Registered, Likely, Guess }

    public sealed record Quarter(DateTime Start, double Kwh, double KrPerKwh)
    {
        public double Kw => Kwh * 4;
        public double Kr => Kwh * KrPerKwh;
    }

    public sealed record Event(DateTime Start, DateTime End, double Kwh, double PeakKw, double Kr, string Icon, string Label, Certainty Sure, string Why)
    {
        public double Hours => (End - Start).TotalHours;
        public double AvgKw => Hours > 0 ? Kwh / Hours : 0;
    }

    public sealed record Report(
        DateOnly Date,
        List<Quarter> Quarters,
        double TotalKwh,
        double EnergyKr,
        double BaseKw,
        double BaseKwh,
        double BaseKr,
        double AlwaysOnKnownKw,
        List<Event> Events,
        double SubscriptionsKr);

    // A quarter counts as "something is on" this far above the base.
    private const double EventThresholdKw = 0.4;
    // Short dips inside one run (a dishwasher between its two heatings) do not split it.
    private const int MaxGapQuarters = 3;

    public static Report Analyze(
        DateOnly date,
        IReadOnlyDictionary<DateTime, double> quarterKwh,
        IReadOnlyList<PricePoint> dayPrices,
        HouseholdSettings settings,
        IReadOnlyList<Appliance> appliances,
        IReadOnlyList<Device> devices,
        IReadOnlyList<RunEntry> runs)
    {
        var allIn = settings.ShowAllIn;
        var price = dayPrices
            .Select(p => PriceBreakdownCalculator.Compute(p, settings))
            .GroupBy(p => p.TimeDk)
            .ToDictionary(g => g.Key, g => g.First().KrPerKwh(allIn));
        double PriceAt(DateTime t)
        {
            if (price.TryGetValue(t, out var v)) return v;
            var hour = new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0);
            return price.TryGetValue(hour, out var h) ? h : price.Count > 0 ? price.Values.Average() : 0;
        }

        var quarters = quarterKwh.OrderBy(kv => kv.Key)
            .Select(kv => new Quarter(kv.Key, kv.Value, PriceAt(kv.Key)))
            .ToList();

        // The always-on base: the low end of the day (10th percentile), not the single lowest quarter.
        var sortedKw = quarters.Select(q => q.Kw).OrderBy(x => x).ToList();
        var baseKw = sortedKw.Count == 0 ? 0 : sortedKw[(int)(sortedKw.Count * 0.10)];

        var events = FindEvents(quarters, baseKw)
            .Select(span => Describe(span, baseKw, appliances, devices, runs))
            .ToList();

        var totalKwh = quarters.Sum(q => q.Kwh);
        var energyKr = quarters.Sum(q => q.Kr);
        var baseKwh = Math.Min(totalKwh, baseKw * quarters.Count / 4.0);
        var baseKr = quarters.Sum(q => Math.Min(q.Kwh, baseKw / 4) * q.KrPerKwh);
        var alwaysOn = devices.Where(d => d.HoursPerDay >= 24).Sum(d => d.Watts) / 1000.0;

        // Subscriptions are per month; the day carries its share, VAT on top.
        var subsKr = settings.Meter is { } m
            ? m.SubscriptionsKrPerMonth * 1.25 / DateTime.DaysInMonth(date.Year, date.Month)
            : 0;

        return new Report(date, quarters, Math.Round(totalKwh, 3), Math.Round(energyKr, 2), baseKw,
            Math.Round(baseKwh, 3), Math.Round(baseKr, 2), alwaysOn, events, Math.Round(subsKr, 2));
    }

    private static List<List<Quarter>> FindEvents(List<Quarter> quarters, double baseKw)
    {
        var spans = new List<List<Quarter>>();
        List<Quarter>? current = null;
        var gap = 0;
        foreach (var q in quarters)
        {
            if (q.Kw >= baseKw + EventThresholdKw)
            {
                if (current is null) { current = []; spans.Add(current); }
                else if (gap > 0) current.AddRange(quarters.SkipWhile(x => x.Start <= current[^1].Start).TakeWhile(x => x.Start < q.Start));
                current.Add(q);
                gap = 0;
            }
            else if (current is not null && ++gap > MaxGapQuarters)
            {
                current = null;
                gap = 0;
            }
        }
        return spans;
    }

    private static Event Describe(List<Quarter> span, double baseKw, IReadOnlyList<Appliance> appliances, IReadOnlyList<Device> devices, IReadOnlyList<RunEntry> runs)
    {
        var start = span[0].Start;
        var end = span[^1].Start.AddMinutes(15);
        var kwh = span.Sum(q => Math.Max(0, q.Kw - baseKw) / 4);
        var kr = span.Sum(q => Math.Max(0, q.Kw - baseKw) / 4 * q.KrPerKwh);
        var peak = span.Max(q => q.Kw);
        var hours = (end - start).TotalHours;
        var avgKw = hours > 0 ? kwh / hours : 0;
        Event Make(string icon, string label, Certainty sure, string why) =>
            new(start, end, Math.Round(kwh, 2), Math.Round(peak, 2), Math.Round(kr, 2), icon, label, sure, why);

        // 1. She pressed "Startet nu" around then.
        var run = runs.Where(r => r.StartedAt >= start.AddMinutes(-45) && r.StartedAt < end)
            .MinBy(r => Math.Abs((r.StartedAt - start).TotalMinutes));
        if (run is not null)
            return Make(run.Icon.Length > 0 ? run.Icon : "🔌", run.ApplianceName, Certainty.Registered, $"registreret kl. {run.StartedAt:HH:mm}");

        // 2. A machine whose run uses about this much and fits in the time.
        var machine = appliances
            .Where(a => a.KwhPerRun > 0 && hours <= a.DurationHours + 0.75)
            .Select(a => (a, score: Math.Abs(kwh - a.KwhPerRun) / a.KwhPerRun))
            // Within 35 %: a half-hour blip of 0.5 kWh is not a 1 kWh wash.
            .Where(x => x.score < 0.35)
            .OrderBy(x => x.score)
            .FirstOrDefault();   // MinBy throws on an empty list of tuples
        if (machine.a is { } am)
            return Make(am.Icon, am.Name, Certainty.Likely, $"{kwh:0.0#} kWh passer med ca. {am.KwhPerRun:0.0#} kWh pr. tur");

        // 3. A device drawing about this many watts, at its usual hour if it has one.
        var device = devices
            .Where(d => d.Watts >= 300 && d.HoursPerDay < 24)
            .Select(d => (d, score: Math.Abs(avgKw - d.Watts / 1000) / (d.Watts / 1000)
                + (d.UsualFromHour is { } h ? Math.Min(Math.Abs(h - start.Hour), 24 - Math.Abs(h - start.Hour)) / 12.0 : 0)))
            .Where(x => x.score < 0.5)
            .OrderBy(x => x.score)
            .FirstOrDefault();
        if (device.d is { } dm)
            return Make(dm.Icon, dm.Name, Certainty.Likely, $"ca. {avgKw * 1000:0} W passer med {dm.Watts:0} W");

        // 4. A short 1-2 kW heating that could be the start of a wash - name the candidates, claim neither.
        var heaters = appliances.Where(a => a.KwhPerRun >= kwh && peak >= 0.9 && hours <= 1).Select(a => a.Name).Take(2).ToList();
        if (heaters.Count > 0 && start.Hour is < 16 or >= 20)
            return Make("♨️", string.Join(" eller ", heaters) + "?", Certainty.Guess, $"kort opvarmning på op til {peak * 1000:0} W – kan være starten af en tur");

        // 5. Size and time of day only.
        if (avgKw >= 3 && hours >= 1) return Make("🚗", "Elbil eller anden stor forbruger", Certainty.Guess, $"{avgKw:0.#} kW i {hours:0.#} timer");
        if (start.Hour is >= 16 and < 20 && hours <= 1.5) return Make("🍳", "Madlavning (komfur/ovn)", Certainty.Guess, "aftensmadstid");
        if (start.Hour is >= 6 and < 9 && hours <= 0.5) return Make("☕", "Morgen (kedel, kaffe, brødrister)", Certainty.Guess, "kort og om morgenen");
        return Make("❓", "Ukendt", Certainty.Guess, "læg dine apparater ind, så kan den genkendes");
    }
}
