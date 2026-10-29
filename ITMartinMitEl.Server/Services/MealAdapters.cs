using ITMartinElPriser.Application;
using ITMartinElPriser.Domain;

namespace ITMartinMitEl.Server.Services;

/// <summary>The Application's meal ports on top of the logged-in household's store (scoped, one per household).</summary>
public sealed class HouseholdMeals(HouseholdStore store) : IHouseholdMeals
{
    public MealHousehold Get()
    {
        var d = store.Get();
        return new MealHousehold(d.Settings, d.Meals, d.Appliances, d.Devices);
    }

    public void Change(Guid mealId, Action<MealActivity> change) =>
        store.Update(d => { if (d.Meals.FirstOrDefault(m => m.Id == mealId) is { } m) change(m); });
}

/// <summary>The household's quarter-hour meter readings from eloverblik.</summary>
public sealed class MeterReadings(ConsumptionStore consumption) : IMeterReadings
{
    public DateOnly? LatestDay => consumption.LatestDay;

    public IReadOnlyDictionary<DateTime, double> Quarters(DateOnly fromInclusive, DateOnly toInclusive)
    {
        var all = new Dictionary<DateTime, double>();
        for (var d = fromInclusive; d <= toInclusive; d = d.AddDays(1))
            foreach (var q in consumption.Quarters(d)) all[q.Key] = q.Value;
        return all;
    }

    public IReadOnlyDictionary<DateTime, double> Hours(DateOnly fromInclusive, DateOnly toExclusive) => consumption.Range(fromInclusive, toExclusive);
}

/// <summary>The front page's view of the logged-in household.</summary>
public sealed class HouseholdHome(HouseholdStore store) : IHouseholdHome
{
    public HouseholdSettings Settings => store.Get().Settings;
    public IReadOnlyList<Appliance> Appliances => store.Get().Appliances;
    public RunEntry? LastRun => store.Get().Runs.OrderByDescending(r => r.StartedAt).FirstOrDefault();
}

/// <summary>Runs and machines of the logged-in household, saved together.</summary>
public sealed class RunBook(HouseholdStore store) : IRunBook
{
    public IReadOnlyList<RunEntry> Runs => store.Get().Runs;
    public IReadOnlyList<Appliance> Appliances => store.Get().Appliances;
    public void Update(Action<List<RunEntry>, List<Appliance>> change) => store.Update(d => change(d.Runs, d.Appliances));
}
