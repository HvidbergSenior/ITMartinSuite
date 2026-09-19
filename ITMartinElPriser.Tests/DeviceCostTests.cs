using FluentAssertions;
using ITMartinElPriser.Core;
using ITMartinMitEl.Server.Services;

namespace ITMartinElPriser.Tests;

[TestFixture]
public class DeviceCostTests
{
    // 1 kr/kWh all day except 02-04 at 0.20 and 17-19 at 2.00.
    private static HourProfile Profile() =>
        new(Enumerable.Range(0, 24).Select(h => h switch { 2 or 3 => 0.20, 17 or 18 => 2.00, _ => 1.00 }).ToArray());

    [Test]
    public void Always_on_device_pays_the_day_average()
    {
        var fridge = new Device { Name = "Køleskab", Watts = 17, HoursPerDay = 24 };
        var c = DeviceCostModel.Price(fridge, Profile());

        c.KwhPerDay.Should().BeApproximately(0.408, 0.001);
        c.AvgKrPerKwh.Should().BeApproximately(Profile().Average, 0.0001);
        c.DayKr.Should().BeApproximately(0.408 * Profile().Average, 0.001);
        c.YearKr.Should().BeApproximately(c.DayKr * 365, 0.01);
        c.BestFromHour.Should().BeNull("an always-on device cannot be moved");
    }

    [Test]
    public void Device_with_usual_hours_is_priced_for_those_hours()
    {
        var tv = new Device { Name = "TV", Watts = 100, HoursPerDay = 2, UsualFromHour = 17 };
        var c = DeviceCostModel.Price(tv, Profile());

        c.KwhPerDay.Should().BeApproximately(0.2, 0.0001);
        c.AvgKrPerKwh.Should().Be(2.00, "17-19 is the dear window");
        c.DayKr.Should().BeApproximately(0.40, 0.0001);
    }

    [Test]
    public void Shiftable_device_gets_the_cheapest_window_and_the_saving()
    {
        var heater = new Device { Name = "Vandvarmer", Watts = 2000, HoursPerDay = 2, UsualFromHour = 17, Shiftable = true };
        var c = DeviceCostModel.Price(heater, Profile());

        c.DayKr.Should().BeApproximately(8.00, 0.0001);
        c.BestFromHour.Should().Be(2);
        c.BestDayKr.Should().BeApproximately(0.80, 0.0001);
        c.SavingPerDayKr.Should().BeApproximately(7.20, 0.0001);
    }

    [Test]
    public void Window_wraps_past_midnight_and_weights_a_fractional_hour()
    {
        var p = Profile();
        p.WindowAvg(23, 2).Should().Be(1.00, "23-01 is flat");
        p.WindowAvg(1, 1.5).Should().BeApproximately((1.00 + 0.5 * 0.20) / 1.5, 0.0001, "01-02 full, 02-02:30 half");
    }

    [Test]
    public void Days_per_week_scales_the_daily_energy()
    {
        var pc = new Device { Watts = 150, HoursPerDay = 4, DaysPerWeek = 5 };
        pc.KwhPerDay.Should().BeApproximately(0.6 * 5 / 7.0, 0.0001);
    }

    [Test]
    public void Appliance_month_uses_average_price_until_three_real_runs_exist()
    {
        var washer = new Appliance { Name = "Vask", KwhPerRun = 1.0, DurationHours = 2, RunsPerWeek = 7 };
        var p = Profile();

        var est = DeviceCostModel.Price(washer, p, []);
        est.PerRunKr.Should().BeApproximately(p.Average, 0.0001);
        est.MonthKr.Should().BeApproximately(p.Average * 365 / 12, 0.001);
        est.Basis.Should().Be("gennemsnitspris");

        var runs = Enumerable.Range(0, 3).Select(_ => new RunEntry { ApplianceId = washer.Id, CostKr = 0.50 }).ToList();
        var real = DeviceCostModel.Price(washer, p, runs);
        real.PerRunKr.Should().Be(0.50);
        real.MonthKr.Should().BeApproximately(0.50 * 365 / 12, 0.001);
        real.Basis.Should().Be("dine registrerede ture");
    }

    [Test]
    public void Profile_from_points_averages_each_hour_across_days()
    {
        var pts = new List<PricedPoint>();
        foreach (var day in new[] { new DateTime(2026, 9, 1), new DateTime(2026, 9, 2) })
            for (var h = 0; h < 24; h++)
                pts.Add(new PricedPoint { TimeDk = day.AddHours(h), TotalKrPerKwh = h == 3 ? (day.Day == 1 ? 0.10 : 0.30) : 1.0, SpotKrPerKwh = 1.0 });

        var p = HourProfile.FromPoints(pts, allIn: true);
        p.KrPerKwh[3].Should().BeApproximately(0.20, 0.0001);
        p.KrPerKwh[10].Should().Be(1.0);
        p.CheapestWindowStart(1).Should().Be(3);
    }

    [Test]
    public void Catalog_entries_are_complete_and_unique()
    {
        DeviceCatalog.All.Select(e => e.Id).Should().OnlyHaveUniqueItems();
        foreach (var e in DeviceCatalog.All)
        {
            e.Name.Should().NotBeNullOrWhiteSpace();
            if (e.IsRun) { e.KwhPerRun.Should().BePositive(e.Id); e.DurationHours.Should().BePositive(e.Id); e.RunsPerWeek.Should().BePositive(e.Id); }
            else { e.Watts.Should().BePositive(e.Id); e.HoursPerDay.Should().BeInRange(0.5, 24, e.Id); }
            if (e.Shiftable) e.HoursPerDay.Should().BeLessThan(24, $"{e.Id}: an always-on device cannot be shifted");
        }
        DeviceCatalog.Find("koeleskab")!.ToDevice(0).KwhPerYear.Should().BeInRange(140, 160);
    }
}
