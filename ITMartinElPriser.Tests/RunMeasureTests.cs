using FluentAssertions;
using ITMartinElPriser.Domain;

namespace ITMartinElPriser.Tests;

[TestFixture]
public class RunMeasureTests
{
    private static readonly DateOnly D = new(2026, 10, 28);

    // A day at 0.1 kWh per quarter, plus extra kWh per quarter from `start` for `quarters` quarters.
    private static Dictionary<DateTime, double> Day(DateTime start, int quarters, double extra)
    {
        var q = new Dictionary<DateTime, double>();
        for (var i = 0; i < 96; i++)
        {
            var t = D.ToDateTime(TimeOnly.MinValue).AddMinutes(15 * i);
            q[t] = 0.1 + (t >= start && t < start.AddMinutes(15 * quarters) ? extra : 0);
        }
        return q;
    }

    [Test]
    public void Oven_test_measures_the_extra_draw_above_the_base_load()
    {
        var start = D.ToDateTime(new TimeOnly(18, 0));
        var m = RunMeasure.Measure(Day(start, 2, 0.6), start, 0.5, 0.55, isTest: true)!;
        m.Kwh.Should().Be(1.2);
        m.Uncertain.Should().BeFalse();
    }

    [Test]
    public void A_start_inside_a_quarter_counts_the_whole_run()
    {
        var start = D.ToDateTime(new TimeOnly(18, 10));
        var m = RunMeasure.Measure(Day(D.ToDateTime(new TimeOnly(18, 0)), 3, 0.4), start, 0.5, 1.0, isTest: false)!;
        m.Kwh.Should().BeApproximately(1.2, 0.001, "18:00, 18:15 and 18:30 are all touched by 18:10-18:40");
    }

    [Test]
    public void No_rise_or_far_too_much_is_uncertain_and_not_used()
    {
        var start = D.ToDateTime(new TimeOnly(10, 0));
        RunMeasure.Measure(Day(start, 0, 0), start, 2, 1.0, false)!.Uncertain.Should().BeTrue();
        RunMeasure.Measure(Day(start, 8, 1.0), start, 2, 1.0, false)!.Uncertain.Should().BeTrue();
    }

    [Test]
    public void Meter_without_the_whole_day_measures_nothing_yet() =>
        RunMeasure.Measure(new Dictionary<DateTime, double>(), D.ToDateTime(new TimeOnly(9, 0)), 2, 1, false).Should().BeNull();

    [Test]
    public void A_test_sets_the_machine_scaled_to_its_own_run_time_and_three_runs_average_otherwise()
    {
        var oven = new Appliance { Name = "Ovn", KwhPerRun = 1.1, DurationHours = 1 };
        var test = new RunEntry { ApplianceId = oven.Id, IsTest = true, DurationHours = 0.5, MeasuredKwh = 0.6, StartedAt = D.ToDateTime(new TimeOnly(18, 0)) };
        RunMeasure.Calibrate(oven, [test]);
        oven.MeasuredKwhPerRun.Should().Be(1.2);
        oven.MeasuredByTest.Should().BeTrue();
        RunMeasure.Priced(oven).KwhPerRun.Should().Be(1.2);

        var washer = new Appliance { Name = "Vaskemaskine", KwhPerRun = 1, DurationHours = 2 };
        var runs = new[] { 0.8, 0.9, 1.0 }.Select(k => new RunEntry { ApplianceId = washer.Id, MeasuredKwh = k, DurationHours = 2 }).ToList();
        RunMeasure.Calibrate(washer, runs.Take(2));
        washer.MeasuredKwhPerRun.Should().BeNull("two runs are not enough");
        RunMeasure.Calibrate(washer, runs);
        washer.MeasuredKwhPerRun.Should().Be(0.9);
    }

    [Test]
    public void Recipes_are_short_where_they_can_be()
    {
        RunMeasure.Recipe(new Appliance { Name = "Ovn" }).Hours.Should().Be(0.5);
        RunMeasure.Recipe(new Appliance { Name = "Vaskemaskine", DurationHours = 2 }).Hours.Should().Be(2);
        RunMeasure.Recipe(new Appliance { Name = "Mikroovn", DurationHours = 0.25 }).Text.Should().NotContain("200°");
    }
}
