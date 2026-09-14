using FluentAssertions;
using ITMartinElPriser.Server.Services;

namespace ITMartinElPriser.Tests;

[TestFixture]
public class PriceModelTests
{
    // A synthetic day: 15-minute slots, spot price by hour so cheap hours
    // are 02-04 and dear hours are 17-19. Tomorrow (if included) is flat.
    private static List<PricePoint> Day(DateOnly date, Func<int, double> spotByHour)
    {
        var list = new List<PricePoint>();
        for (var h = 0; h < 24; h++)
            for (var q = 0; q < 4; q++)
            {
                var t = date.ToDateTime(new TimeOnly(h, q * 15));
                list.Add(new PricePoint { TimeDk = t, TimeUtc = t.AddHours(-2), PriceKrPerKwh = spotByHour(h) });
            }
        return list;
    }

    private static double Shape(int h) => h switch { >= 2 and < 4 => 0.20, >= 17 and < 19 => 2.00, _ => 1.00 };

    private static HouseholdData Household(bool allIn = false) => new()
    {
        Settings = new HouseholdSettings { ShowAllIn = allIn, QuietFromHour = 23, QuietToHour = 6 },
        Appliances = [new Appliance { Name = "Vaskemaskine", KwhPerRun = 1.0, DurationHours = 2, SortOrder = 0 }],
    };

    private static readonly DateOnly Today = new(2026, 9, 14);

    [Test]
    public void Now_price_is_the_current_quarter_and_ranked_against_the_day()
    {
        var snap = PriceModel.Build(Day(Today, Shape), Household(), Today.ToDateTime(new TimeOnly(17, 40)));

        snap.HasData.Should().BeTrue();
        snap.NowKrPerKwh.Should().Be(2.00);
        snap.NowRank01.Should().Be(1.0, "17:40 is in the dearest hour");
        snap.Today!.Cheapest!.Start.Hour.Should().Be(2);
        snap.Today.Dearest!.Start.Hour.Should().Be(17);
    }

    [Test]
    public void Run_cost_is_summed_over_every_slot_the_machine_is_on_not_the_start_price()
    {
        // Start 16:00: first hour at 1.00, second hour at 2.00 -> 1 kWh spread
        // evenly = 0.5 kWh * 1.00 + 0.5 kWh * 2.00 = 1.50 kr, NOT 1.00 kr.
        var snap = PriceModel.Build(Day(Today, Shape), Household(), Today.ToDateTime(new TimeOnly(16, 0)));

        var wash = snap.Appliances.Single();
        wash.Now!.Start.Should().Be(Today.ToDateTime(new TimeOnly(16, 0)));
        wash.Now.CostKr.Should().Be(1.50);
    }

    [Test]
    public void Cheapest_run_today_ignores_quiet_hours_and_the_past()
    {
        // Cheap hours are 02-04 but that's night (quiet) and already past at
        // 10:00 - the best daytime start is any 2 h block at 1.00 = 1.00 kr.
        var snap = PriceModel.Build(Day(Today, Shape), Household(), Today.ToDateTime(new TimeOnly(10, 0)));

        var wash = snap.Appliances.Single();
        wash.CheapestToday!.CostKr.Should().Be(1.00);
        wash.CheapestToday.Start.Hour.Should().BeGreaterThanOrEqualTo(6);
        wash.DearestUpcoming!.Start.Should().Be(Today.ToDateTime(new TimeOnly(17, 0)));
        wash.DearestUpcoming.CostKr.Should().Be(2.00);
    }

    [Test]
    public void A_run_never_bridges_a_gap_where_tomorrow_is_not_published_yet()
    {
        // At 23:00 with only today's data, a 2 h run starting 23:00 would need
        // slots into tomorrow that don't exist - so there is no "Now" option.
        var snap = PriceModel.Build(Day(Today, Shape), Household(), Today.ToDateTime(new TimeOnly(23, 0)));

        snap.Appliances.Single().Now.Should().BeNull();
        snap.Tomorrow.Should().BeNull();
    }

    [Test]
    public void Tomorrow_appears_once_published_and_gets_its_own_cheapest()
    {
        var points = Day(Today, Shape).Concat(Day(Today.AddDays(1), _ => 0.50)).ToList();
        var snap = PriceModel.Build(points, Household(), Today.ToDateTime(new TimeOnly(14, 0)));

        snap.Tomorrow.Should().NotBeNull();
        snap.Tomorrow!.Hours.Should().HaveCount(24);
        snap.Appliances.Single().CheapestTomorrow!.CostKr.Should().Be(0.50);
    }

    [Test]
    public void All_in_price_is_higher_than_spot_and_includes_vat()
    {
        var spot = PriceModel.Build(Day(Today, _ => 1.00), Household(allIn: false), Today.ToDateTime(new TimeOnly(10, 0)));
        var allIn = PriceModel.Build(Day(Today, _ => 1.00), Household(allIn: true), Today.ToDateTime(new TimeOnly(10, 0)));

        spot.NowKrPerKwh.Should().Be(1.00);
        allIn.NowKrPerKwh.Should().BeGreaterThan(1.25, "spot + nettarif + afgift + tillæg, times 1.25 moms");
    }

    [Test]
    public void No_data_means_no_made_up_numbers()
    {
        var snap = PriceModel.Build([], Household(), DateTime.Now);

        snap.HasData.Should().BeFalse();
        snap.NowKrPerKwh.Should().BeNull();
        snap.Appliances.Should().BeEmpty();
    }

    [TestCase(23, 6, 23, true)]
    [TestCase(23, 6, 3, true)]
    [TestCase(23, 6, 6, false)]
    [TestCase(23, 6, 12, false)]
    [TestCase(1, 5, 3, true)]
    [TestCase(1, 5, 0, false)]
    [TestCase(7, 7, 7, false)]
    public void Quiet_hours_wrap_around_midnight(int from, int to, int hour, bool quiet)
    {
        var s = new HouseholdSettings { QuietFromHour = from, QuietToHour = to };
        PriceModel.IsQuiet(Today.ToDateTime(new TimeOnly(hour, 0)), s).Should().Be(quiet);
    }
}
