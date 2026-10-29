using FluentAssertions;
using ITMartinElPriser.Domain;

namespace ITMartinElPriser.Tests;

[TestFixture]
public class MealPlannerTests
{
    private static readonly DateOnly D = new(2026, 10, 28);

    // Spot by hour: 17-19 dear (3.00), 19-20 cheaper (1.00), late evening 0.50, else 1.00.
    private static double Shape(int h) => h switch { >= 17 and < 19 => 3.00, >= 21 => 0.50, _ => 1.00 };

    private static List<PricePoint> Day(DateOnly date)
    {
        var list = new List<PricePoint>();
        for (var h = 0; h < 24; h++)
            for (var q = 0; q < 4; q++)
            {
                var t = date.ToDateTime(new TimeOnly(h, q * 15));
                list.Add(new PricePoint { TimeDk = t, TimeUtc = t.AddHours(-1), PriceKrPerKwh = Shape(h) });
            }
        return list;
    }

    private static readonly HouseholdSettings Spot = new() { ShowAllIn = false };

    private static MealActivity Dinner() => new()
    {
        Name = "Aftensmad", Kinds = ["ovn", "kogeplader", "emfang"], StartHour = 17, StartMinute = 30, WindowFromHour = 16, WindowToHour = 20,
    };

    [Test]
    public void Meal_adds_the_kinds_energy_over_the_longest_kind()
    {
        var run = MealPlanner.AsRun(Dinner(), [], []);
        run.KwhPerRun.Should().BeApproximately(1.1 + 0.9 + 0.08, 0.0001);
        run.DurationHours.Should().Be(1.0, "the oven is the longest");
    }

    [Test]
    public void Photographed_machine_with_the_kind_name_replaces_the_typical_value()
    {
        var oven = new Appliance { Name = "Ovn", KwhPerRun = 0.7, DurationHours = 1.5, Photo = new PhotoFacts() };
        var run = MealPlanner.AsRun(new MealActivity { Kinds = ["ovn"] }, [oven], []);
        run.KwhPerRun.Should().Be(0.7);
        run.DurationHours.Should().Be(1.5);
    }

    [Test]
    public void Cheapest_start_stays_inside_the_window_and_shows_the_saving()
    {
        var m = Dinner();
        var run = MealPlanner.AsRun(m, [], []);
        var plan = MealPlanner.Plan(Day(D), Spot, m, run, D);

        plan.Usual!.CostKr.Should().BeApproximately(run.KwhPerRun * 3.00, 0.01, "17:30-18:30 is all dear");
        plan.Cheapest!.Start.Hour.Should().BeOneOf(16, 19);
        plan.Cheapest.End.Should().BeOnOrBefore(D.ToDateTime(new TimeOnly(20, 0)), "dinner never moves out of the evening");
        plan.SavingKr.Should().BeApproximately(run.KwhPerRun * 2.00, 0.01);
    }

    [Test]
    public void Meter_finds_the_evening_bump_and_prices_it()
    {
        var quarters = new Dictionary<DateTime, double>();
        foreach (var t in Day(D).Select(p => p.TimeDk))
            quarters[t] = t.Hour == 17 || (t.Hour == 18 && t.Minute < 30) ? 0.6 : 0.1; // 1.5 h of cooking at 2 kW over a 0.4 kW base
        var r = MealPlanner.FromMeter(quarters, Day(D), Spot, Dinner(), 1.5)!;

        r.Days.Should().Be(1);
        r.KwhPerDay.Should().BeApproximately(6 * 0.5, 0.0001);
        r.RealKrPerDay.Should().BeApproximately(3.0 * 3.00, 0.001);
        r.UsualStart.Should().Be(new TimeOnly(17, 0));
        r.PerfectKrPerDay.Should().BeLessThan(r.RealKrPerDay);
        r.PerfectPercent.Should().BeLessThan(100);
    }

    [Test]
    public void Night_the_clocks_go_back_does_not_break_the_meter_report()
    {
        var prices = Day(D);
        prices.AddRange(prices.Where(p => p.TimeDk.Hour == 2).Select(p => new PricePoint { TimeDk = p.TimeDk, TimeUtc = p.TimeUtc.AddHours(1), PriceKrPerKwh = p.PriceKrPerKwh }).ToList());
        var quarters = Day(D).ToDictionary(p => p.TimeDk, _ => 0.1);
        MealPlanner.FromMeter(quarters, prices, Spot, Dinner(), 1.0).Should().NotBeNull();
    }

    [Test]
    public void Late_meal_is_cheaper_than_the_same_meal_at_dinner_time()
    {
        var late = new MealActivity { Name = "Børnenes sene mad", Kinds = ["airfryer"], StartHour = 23, WindowFromHour = 21, WindowToHour = 24 };
        var run = MealPlanner.AsRun(late, [], []);
        var at23 = MealPlanner.Plan(Day(D), Spot, late, run, D).Usual!;
        var at17 = PriceModel.CostOfRun(Day(D), Spot, run, D.ToDateTime(new TimeOnly(17, 30)))!;
        at23.CostKr.Should().BeLessThan(at17.CostKr);
    }
}

[TestFixture]
public class MealUseCasesTests
{
    private sealed class FakePrices(List<PricePoint> day) : ITMartinElPriser.Application.IPriceSource
    {
        public Task<List<PricePoint>> GetPricesAsync(string priceArea = "DK1", CancellationToken ct = default) => Task.FromResult(day);
        public Task<List<PricePoint>> GetDayAsync(DateOnly date, string priceArea = "DK1", CancellationToken ct = default) => Task.FromResult(day);
        public Task<List<PricePoint>> GetRangeAsync(DateOnly f, DateOnly t, string priceArea = "DK1", CancellationToken ct = default) => Task.FromResult(day);
    }

    private sealed class FakeHousehold : ITMartinElPriser.Application.IHouseholdMeals
    {
        public readonly List<MealActivity> Meals = MealActivity.Defaults();
        public ITMartinElPriser.Application.MealHousehold Get() => new(new HouseholdSettings { ShowAllIn = false }, Meals, [], []);
        public void Change(Guid id, Action<MealActivity> change) => change(Meals.First(m => m.Id == id));
    }

    private sealed class NoMeter : ITMartinElPriser.Application.IMeterReadings
    {
        public DateOnly? LatestDay => null;
        public IReadOnlyDictionary<DateTime, double> Quarters(DateOnly f, DateOnly t) => new Dictionary<DateTime, double>();
        public IReadOnlyDictionary<DateTime, double> Hours(DateOnly f, DateOnly t) => new Dictionary<DateTime, double>();
    }

    private static List<PricePoint> Today()
    {
        var d = DateOnly.FromDateTime(DkTime.Now);
        var list = new List<PricePoint>();
        for (var i = 0; i < 96; i++)
        {
            var t = d.ToDateTime(TimeOnly.MinValue).AddMinutes(15 * i);
            list.Add(new PricePoint { TimeDk = t, TimeUtc = t.AddHours(-1), PriceKrPerKwh = t.Hour is >= 17 and < 19 ? 3 : 1 });
        }
        return list;
    }

    [Test]
    public async Task Both_default_meals_load_without_a_meter_and_taps_change_them()
    {
        var h = new FakeHousehold();
        var uc = new ITMartinElPriser.Application.MealUseCases(new FakePrices(Today()), h, new NoMeter());

        var cards = await uc.LoadAsync();
        cards.Select(c => c.Meal.Name).Should().Equal("Aftensmad", "Børnenes sene mad");
        cards.Should().OnlyContain(c => c.Meter == null && c.Starts.Count > 0);

        var dinner = h.Meals[0];
        uc.ToggleKind(dinner.Id, "airfryer");
        uc.SetStart(dinner.Id, new TimeOnly(18, 0));
        uc.SetDays(dinner.Id, 5);
        dinner.Kinds.Should().Contain("airfryer");
        (dinner.StartHour, dinner.StartMinute, dinner.DaysPerWeek).Should().Be((18, 0, 5));
        uc.ToggleKind(dinner.Id, "airfryer");
        dinner.Kinds.Should().NotContain("airfryer");
    }
}

[TestFixture]
public class MinFrontPageUseCasesTests
{
    private sealed class Home : ITMartinElPriser.Application.IHouseholdHome
    {
        public HouseholdSettings Settings { get; } = new() { ShowAllIn = false };
        public IReadOnlyList<Appliance> Appliances { get; } = Appliance.Defaults();
        public RunEntry? LastRun => null;
    }

    private sealed class Prices(List<PricePoint> day) : ITMartinElPriser.Application.IPriceSource
    {
        public Task<List<PricePoint>> GetPricesAsync(string priceArea = "DK1", CancellationToken ct = default) => Task.FromResult(day);
        public Task<List<PricePoint>> GetDayAsync(DateOnly date, string priceArea = "DK1", CancellationToken ct = default) => Task.FromResult(day);
        public Task<List<PricePoint>> GetRangeAsync(DateOnly f, DateOnly t, string priceArea = "DK1", CancellationToken ct = default) => Task.FromResult(day);
    }

    private sealed class NoMeter : ITMartinElPriser.Application.IMeterReadings
    {
        public DateOnly? LatestDay => null;
        public IReadOnlyDictionary<DateTime, double> Quarters(DateOnly f, DateOnly t) => new Dictionary<DateTime, double>();
        public IReadOnlyDictionary<DateTime, double> Hours(DateOnly f, DateOnly t) => new Dictionary<DateTime, double>();
    }

    [Test]
    public async Task Front_page_prices_the_households_own_machines_and_has_no_home_box_without_a_meter()
    {
        var d = DateOnly.FromDateTime(DkTime.Now);
        var day = Enumerable.Range(0, 96).Select(i => d.ToDateTime(TimeOnly.MinValue).AddMinutes(15 * i))
            .Select(t => new PricePoint { TimeDk = t, TimeUtc = t.AddHours(-1), PriceKrPerKwh = t.Hour < 6 ? 0.5 : 1.5 }).ToList();
        var uc = new ITMartinElPriser.Application.MinFrontPageUseCases(new Prices(day), new Home(), new NoMeter());

        var view = await uc.LoadAsync();
        view.Machines.Select(m => m.Name).Should().Equal("Vaskemaskine", "Opvaskemaskine", "Tørretumbler");
        view.Machine(null)!.Name.Should().Be("Vaskemaskine", "the first machine is shown until another is tapped");
        view.Machine(view.Machines[2].Id)!.Name.Should().Be("Tørretumbler");
        view.RunAt(view.Machines[0], d.ToDateTime(new TimeOnly(2, 0)))!.CostKr.Should().BeApproximately(1.0 * 0.5, 0.001);
        (await uc.HomeAsync()).Should().BeNull();
    }
}

[TestFixture]
public class CookingKindTests
{
    [TestCase("Induktionskogeplade", "kogeplader")]
    [TestCase("Emhætte", "emfang")]
    [TestCase("Ovn Bosch", "ovn")]
    [TestCase("Airfryer", "airfryer")]
    [TestCase("Mikroovn", "mikroovn")]
    public void Camera_names_find_their_kind(string name, string key) =>
        CookingKinds.All.Where(k => k.Matches(name)).Select(k => k.Key).Should().Equal(key);

    [Test]
    public void A_hob_from_its_rating_plate_counts_a_realistic_share_of_its_max_power()
    {
        var hob = new Device { Name = "Induktionskogeplade", Watts = 7400, Photo = new PhotoFacts() };
        var run = MealPlanner.AsRun(new MealActivity { Kinds = ["kogeplader"] }, [], [hob]);
        run.KwhPerRun.Should().BeApproximately(7.4 * 0.75 * 0.2, 0.001);
    }
}
