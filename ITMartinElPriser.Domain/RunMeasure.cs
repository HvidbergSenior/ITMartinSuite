namespace ITMartinElPriser.Domain;

// What a machine really uses, from the household's own meter instead of the label (user 2026-10-29: "is to find out what
// it uses right? instead of guessing"). The meter gives the whole house per 15 minutes (the finest Datahub has), so a run
// is its extra draw above the day's base load while it ran.
public sealed record MeasureResult(double Kwh, bool Uncertain, string Note);

// A short calibration recipe per kind of machine ("oven on 200 degrees for an hour" - user 2026-10-29, "maybe even shorter").
public sealed record TestRecipe(string Text, double Hours);

public static class RunMeasure
{
    private const int SlotMinutes = 15;
    // At least this many normal (not test) runs before their average replaces the label.
    public const int RunsForAverage = 3;

    // The test for this machine: as short as the meter can still see clearly (whole quarters), the normal program where
    // a shorter one would not be the real thing.
    public static TestRecipe Recipe(Appliance a)
    {
        var n = a.Name.ToLowerInvariant();
        if (n.Contains("ovn") && !n.Contains("mikro"))
            return new("Tænd ovnen på 200° varmluft i 30 minutter – tom, med lågen lukket.", 0.5);
        if (n.Contains("vask") && !n.Contains("opvask"))
            return new($"Start jeres mest brugte program (fx 40°) med en normal fyldning. Regnet med {Hours(a.DurationHours)}.", a.DurationHours);
        if (n.Contains("opvask"))
            return new($"Start jeres normale program (fx Eco) med en fuld maskine. Regnet med {Hours(a.DurationHours)}.", a.DurationHours);
        if (n.Contains("tørretumbler"))
            return new($"Start jeres normale program med en fuld tromle. Regnet med {Hours(a.DurationHours)}.", a.DurationHours);
        return new($"Start maskinen som I plejer. Regnet med {Hours(a.DurationHours)}.", a.DurationHours);
    }

    // quarters = the meter's kWh per quarter for the run's start day (and the next day, when it runs past midnight).
    // Null = the meter does not have the whole run yet. labelKwh = what the machine is set up with, for the sanity check.
    public static MeasureResult? Measure(IReadOnlyDictionary<DateTime, double> quarters, DateTime start, double hours, double labelKwh, bool isTest)
    {
        var day = DateOnly.FromDateTime(start);
        var dayQuarters = quarters.Where(q => DateOnly.FromDateTime(q.Key) == day).Select(q => q.Value).OrderBy(v => v).ToList();
        if (dayQuarters.Count < 90) return null;
        var baseLoad = dayQuarters[dayQuarters.Count / 4];

        var from = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute / SlotMinutes * SlotMinutes, 0);
        var slots = Math.Max(1, (int)Math.Ceiling((start - from).TotalMinutes / SlotMinutes + hours * 60 / SlotMinutes));
        double kwh = 0;
        for (var i = 0; i < slots; i++)
        {
            if (!quarters.TryGetValue(from.AddMinutes(SlotMinutes * i), out var q)) return null;
            kwh += Math.Max(0, q - baseLoad);
        }
        kwh = Math.Round(kwh, 2);

        if (kwh < 0.05) return new(kwh, true, "Måleren så ingen tydelig stigning – startede maskinen?");
        if (labelKwh > 0 && kwh > labelKwh * 2.5)
            return new(kwh, true, "Meget mere end forventet – kørte der andet stort samtidig?");
        if (labelKwh > 0 && kwh < labelKwh * 0.3 && !isTest)
            return new(kwh, true, "Meget mindre end forventet – kortere program?");
        return new(kwh, false, isTest ? "Test målt" : "Målt");
    }

    // The machine's measured figure: a clean test run wins (the newest); else the average of 3+ clean normal runs.
    public static void Calibrate(Appliance a, IEnumerable<RunEntry> runs)
    {
        var clean = runs.Where(r => r.ApplianceId == a.Id && r.MeasuredKwh is > 0 && !r.MeasuredUncertain).ToList();
        var test = clean.Where(r => r.IsTest).MaxBy(r => r.StartedAt);
        if (test is not null)
        {
            // A short oven test is scaled to the machine's own run time (kWh per hour x hours).
            var perRun = test.DurationHours > 0 && a.DurationHours > 0 ? test.MeasuredKwh!.Value / test.DurationHours * a.DurationHours : test.MeasuredKwh!.Value;
            (a.MeasuredKwhPerRun, a.MeasuredRuns, a.MeasuredByTest) = (Math.Round(perRun, 2), 1, true);
            return;
        }
        var normal = clean.Where(r => !r.IsTest).ToList();
        if (normal.Count >= RunsForAverage)
            (a.MeasuredKwhPerRun, a.MeasuredRuns, a.MeasuredByTest) = (Math.Round(normal.Average(r => r.MeasuredKwh!.Value), 2), normal.Count, false);
    }

    // The machine as it should be priced: its measured kWh when there is one.
    public static Appliance Priced(Appliance a)
    {
        if (a.MeasuredKwhPerRun is not > 0) return a;
        var c = a.Clone();
        c.KwhPerRun = a.MeasuredKwhPerRun.Value;
        return c;
    }

    private static string Hours(double h) => h == 1 ? "1 time" : h < 1 ? $"{h * 60:0} min" : h.ToString("0.#", new System.Globalization.CultureInfo("da-DK")) + " timer";
}
