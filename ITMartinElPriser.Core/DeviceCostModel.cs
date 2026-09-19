namespace ITMartinElPriser.Core;

// 24 prices, one per hour of the day. Either one real day (today) or an
// average over recent days - the "typical" curve used for kr/month and kr/year
// so a single freak day does not decide what the fridge costs a year.
public sealed record HourProfile(double[] KrPerKwh)
{
    public double Average => KrPerKwh.Average();

    public static HourProfile FromPoints(IEnumerable<PricedPoint> points, bool allIn)
    {
        var byHour = points.GroupBy(p => p.TimeDk.Hour).ToDictionary(g => g.Key, g => g.Average(p => p.KrPerKwh(allIn)));
        if (byHour.Count == 0) return new HourProfile(new double[24]);
        var fallback = byHour.Values.Average();
        return new HourProfile(Enumerable.Range(0, 24).Select(h => byHour.GetValueOrDefault(h, fallback)).ToArray());
    }

    // Average price for `hours` hours starting at `fromHour`, wrapping past
    // midnight. A fractional last hour counts for its fraction.
    public double WindowAvg(int fromHour, double hours)
    {
        if (hours <= 0) return Average;
        double sum = 0, weight = 0;
        for (var i = 0; i < 24 && weight < hours; i++)
        {
            var w = Math.Min(1, hours - weight);
            sum += KrPerKwh[(fromHour + i) % 24] * w;
            weight += w;
        }
        return sum / weight;
    }

    public int CheapestWindowStart(double hours) =>
        Enumerable.Range(0, 24).MinBy(h => WindowAvg(h, hours));
}

public sealed record DeviceCost(
    Device Device,
    double KwhPerDay,
    double DayKr,             // what a day costs at its usual hours
    double AvgKrPerKwh,       // the price it actually pays
    int? BestFromHour,        // shiftable only: cheapest window start
    double? BestDayKr)        // shiftable only: what that window would cost
{
    public double MonthKr => DayKr * 365 / 12.0;
    public double YearKr => DayKr * 365;
    public double? SavingPerDayKr => BestDayKr is { } b && DayKr - b > 0.005 ? DayKr - b : null;
}

public sealed record ApplianceCost(
    Appliance Appliance,
    double PerRunKr,          // one run at the day's average price
    double MonthKr,
    double YearKr,
    string Basis);            // "gennemsnitspris" or "dine registrerede ture"

public static class DeviceCostModel
{
    public static DeviceCost Price(Device d, HourProfile profile)
    {
        var hours = Math.Clamp(d.HoursPerDay, 0, 24);
        var kwh = d.KwhPerDay;
        var allDay = hours >= 24 || d.UsualFromHour is null;
        var avg = allDay ? profile.Average : profile.WindowAvg(d.UsualFromHour!.Value, hours);
        var dayKr = kwh * avg;

        int? bestFrom = null; double? bestKr = null;
        if (d.Shiftable && hours < 24 && hours > 0)
        {
            bestFrom = profile.CheapestWindowStart(hours);
            bestKr = kwh * profile.WindowAvg(bestFrom.Value, hours);
        }
        return new DeviceCost(d, kwh, dayKr, avg, bestFrom, bestKr);
    }

    // Per-run machines priced for the month: real logged runs when there are
    // enough of them, otherwise kWh x runs x the typical average price.
    public static ApplianceCost Price(Appliance a, HourProfile profile, IReadOnlyList<RunEntry> recentRuns)
    {
        var mine = recentRuns.Where(r => r.ApplianceId == a.Id).ToList();
        var perRunAvg = a.KwhPerRun * profile.Average;
        var perWeek = a.RunsPerWeek;
        if (mine.Count >= 3)
        {
            var perRun = mine.Average(r => r.CostKr);
            var month = perRun * perWeek * 365 / 7 / 12.0;
            return new ApplianceCost(a, perRun, month, month * 12, "dine registrerede ture");
        }
        var m = perRunAvg * perWeek * 365 / 7 / 12.0;
        return new ApplianceCost(a, perRunAvg, m, m * 12, "gennemsnitspris");
    }
}
