using ITMartinElPriser.Domain;

namespace ITMartinElPriser.Application;

/// <summary>The logged-in household for MinElpris' front page: its settings, its own machines (from photos) and the last run it logged.</summary>
public interface IHouseholdHome
{
    HouseholdSettings Settings { get; }
    IReadOnlyList<Appliance> Appliances { get; }
    RunEntry? LastRun { get; }
}

/// <summary>Today and tomorrow priced for this household, and its own machines run against them - the same as the free
/// front page's NowView, but with the machines the household photographed instead of a dishwasher/washer cookie.</summary>
public sealed class MinNowView(HouseholdSettings settings, List<PricePoint> raw, PriceSnapshot snapshot, IReadOnlyList<Appliance> machines)
{
    public HouseholdSettings Settings { get; } = settings;
    public PriceSnapshot Snapshot { get; } = snapshot;
    public IReadOnlyList<Appliance> Machines { get; } = machines;

    /// <summary>The machine with this id, else the first one (a household always has at least the defaults).</summary>
    public Appliance? Machine(Guid? id) => Machines.FirstOrDefault(m => m.Id == id) ?? Machines.FirstOrDefault();

    public RunOption? RunAt(Appliance a, DateTime start) => raw.Count == 0 ? null : PriceModel.CostOfRun(raw, Settings, a, start);

    /// <summary>The cheapest start from this quarter on; night = also while people sleep (a machine with a delayed start).</summary>
    public RunOption? Cheapest(Appliance a, DateTime from, bool night) => raw.Count == 0 ? null : PriceModel.CheapestFrom(raw, Settings, a, from, night);

    public bool IsQuiet(DateTime t) => PriceModel.IsQuiet(t, Settings);
}

/// <summary>"Dit hjem" from the meter: the latest measured day and the month so far, with the real prices.</summary>
public sealed record HomeSummary(DateOnly LatestDay, double DayKwh, double DayKr, double MonthKwh, double MonthKr, double MonthProjectedKr, RunEntry? LastRun);

/// <summary>MinElpris' front page made like the free app's (user 2026-10-29: "it needs to have the same easy page as the
/// free one"): the price of the household's own machine now and its cheapest start, hour by hour, "Hvad havde du sparet?",
/// and one small box from the meter. The page only shows.</summary>
public sealed class MinFrontPageUseCases(IPriceSource prices, IHouseholdHome household, IMeterReadings meter)
{
    public async Task<MinNowView> LoadAsync(CancellationToken ct = default)
    {
        var s = household.Settings;
        var raw = await prices.GetPricesAsync(s.PriceArea, ct);
        var machines = household.Appliances.OrderBy(a => a.SortOrder).ToList();
        return new MinNowView(s, raw, PriceModel.Build(raw, s, [], DkTime.Now), machines);
    }

    /// <summary>The last 30 days with real prices: the machine at 18:00 every day vs at the day's cheapest start.</summary>
    public async Task<Hindsight?> HindsightAsync(Appliance machine, CancellationToken ct = default)
    {
        var s = household.Settings;
        var today = DateOnly.FromDateTime(DkTime.Now);
        var raw = await prices.GetRangeAsync(today.AddDays(-30), today.AddDays(1), s.PriceArea, ct);
        return Hindsight.For(raw, s, machine, today);
    }

    /// <summary>Null = no meter connected or nothing measured yet. One price range call covers the whole box.</summary>
    public async Task<HomeSummary?> HomeAsync(CancellationToken ct = default)
    {
        var s = household.Settings;
        if (string.IsNullOrWhiteSpace(s.MeteringPointId) || meter.LatestDay is not { } latest) return null;
        var today = DateOnly.FromDateTime(DkTime.Now);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var from = latest < monthStart ? latest : monthStart;
        var hours = meter.Hours(from, latest.AddDays(1));
        if (hours.Count == 0) return null;

        var priced = (await prices.GetRangeAsync(from, latest.AddDays(1), s.PriceArea, ct)).Select(p => PriceBreakdownCalculator.Compute(p, s)).ToList();
        var byHour = priced.GroupBy(p => new DateTime(p.TimeDk.Year, p.TimeDk.Month, p.TimeDk.Day, p.TimeDk.Hour, 0, 0))
            .ToDictionary(g => g.Key, g => g.Average(p => p.KrPerKwh(s.ShowAllIn)));
        var day = hours.Where(h => DateOnly.FromDateTime(h.Key) == latest).ToList();
        var dayKwh = day.Sum(h => h.Value);
        var dayKr = day.Sum(h => h.Value * byHour.GetValueOrDefault(h.Key));

        var month = hours.Where(h => h.Key >= monthStart.ToDateTime(TimeOnly.MinValue)).ToDictionary(h => h.Key, h => h.Value);
        var bill = BillCalculator.Build(monthStart, monthStart.AddMonths(1), month, priced, s);
        return new HomeSummary(latest, dayKwh, dayKr, bill.Kwh, bill.TotalKr, bill.DaysMeasured > 0 ? bill.ProjectedKr : 0, household.LastRun);
    }
}
