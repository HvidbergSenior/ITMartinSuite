using ITMartinElPriser.Domain;

namespace ITMartinElPriser.Application;

/// <summary>One logged-in household's meals and the machines that can replace the typical figures (MinElpris).</summary>
public interface IHouseholdMeals
{
    MealHousehold Get();
    /// <summary>Changes one meal and saves the household.</summary>
    void Change(Guid mealId, Action<MealActivity> change);
}

public sealed record MealHousehold(HouseholdSettings Settings, IReadOnlyList<MealActivity> Meals, IReadOnlyList<Appliance> Appliances, IReadOnlyList<Device> Devices);

/// <summary>The household's own meter (eloverblik): kWh per quarter-hour, Danish wall-clock time.</summary>
public interface IMeterReadings
{
    DateOnly? LatestDay { get; }
    IReadOnlyDictionary<DateTime, double> Quarters(DateOnly fromInclusive, DateOnly toInclusive);
}

/// <summary>One meal as the page shows it: the next time it is cooked, the cheapest start in its window, and what
/// the meter says the meal really cost.</summary>
public sealed record MealCard(MealActivity Meal, Appliance Run, bool IsToday, MealPlan Plan, MealMeterReport? Meter, IReadOnlyList<TimeOnly> Starts)
{
    /// <summary>Meals a month at the chosen days a week.</summary>
    public double PerMonth => 30.0 * Meal.DaysPerWeek / 7;
    /// <summary>A tip only when moving the meal saves 5 kr a month or more (user 2026-10-29: "never a tip under ~5 kr/md").</summary>
    public bool WorthMoving => Plan.SavingKr * PerMonth >= 5;
}

/// <summary>MinElpris "Mad" (user 2026-10-29: "our cooking in the evening", "the kids usually eat around 23"): several
/// devices together, tapped - never typed. The page only shows; the sums are MealPlanner's.</summary>
public sealed class MealUseCases(IPriceSource prices, IHouseholdMeals household, IMeterReadings meter)
{
    public const int MeterDays = 28;

    public async Task<List<MealCard>> LoadAsync(CancellationToken ct = default)
    {
        var h = household.Get();
        var now = DkTime.Now;
        var raw = await prices.GetPricesAsync(h.Settings.PriceArea, ct);
        var history = await HistoryAsync(h.Settings, ct);
        var cards = new List<MealCard>();
        foreach (var m in h.Meals)
        {
            var run = MealPlanner.AsRun(m, h.Appliances, h.Devices);
            var day = MealPlanner.NextDay(m, raw, now);
            var meterReport = history is { } x ? MealPlanner.FromMeter(x.Quarters, x.Prices, h.Settings, m, run.DurationHours) : null;
            cards.Add(new MealCard(m, run, day == DateOnly.FromDateTime(now), MealPlanner.Plan(raw, h.Settings, m, run, day), meterReport, MealPlanner.Starts(m, run)));
        }
        return cards;
    }

    public void ToggleKind(Guid meal, string kind) => household.Change(meal, m => { if (!m.Kinds.Remove(kind)) m.Kinds.Add(kind); });

    public void SetStart(Guid meal, TimeOnly start) => household.Change(meal, m => { m.StartHour = start.Hour; m.StartMinute = start.Minute; });

    public void SetDays(Guid meal, int daysPerWeek) => household.Change(meal, m => m.DaysPerWeek = Math.Clamp(daysPerWeek, 1, 7));

    // The last 4 weeks the meter has, with the real prices of those days. Null = no meter connected.
    private async Task<(IReadOnlyDictionary<DateTime, double> Quarters, List<PricePoint> Prices)?> HistoryAsync(HouseholdSettings s, CancellationToken ct)
    {
        if (meter.LatestDay is not { } last) return null;
        var from = last.AddDays(-(MeterDays - 1));
        var quarters = meter.Quarters(from, last);
        if (quarters.Count == 0) return null;
        return (quarters, await prices.GetRangeAsync(from, last.AddDays(1), s.PriceArea, ct));
    }
}
