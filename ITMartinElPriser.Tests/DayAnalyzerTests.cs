using ITMartinElPriser.Core;
using FluentAssertions;

namespace ITMartinElPriser.Tests;

[TestFixture]
public class DayAnalyzerTests
{
    private static readonly DateOnly Day = new(2026, 9, 1);

    // 0.2 kW all day, plus whatever the test adds on top, 1 kr/kWh spot everywhere.
    private static (Dictionary<DateTime, double> Q, List<PricePoint> P) Flat(Action<Dictionary<DateTime, double>>? add = null)
    {
        var q = new Dictionary<DateTime, double>();
        var p = new List<PricePoint>();
        for (var t = Day.ToDateTime(TimeOnly.MinValue); t < Day.AddDays(1).ToDateTime(TimeOnly.MinValue); t = t.AddMinutes(15))
        {
            q[t] = 0.05;
            p.Add(new PricePoint { TimeDk = t, TimeUtc = t.AddHours(-2), PriceKrPerKwh = 1 });
        }
        add?.Invoke(q);
        return (q, p);
    }

    private static readonly HouseholdSettings Spot = new() { ShowAllIn = false };

    [Test]
    public void Dishwasher_with_two_heatings_is_one_event_matched_to_the_machine()
    {
        var (q, p) = Flat(q =>
        {
            var s = Day.ToDateTime(new TimeOnly(21, 45));
            q[s] += 0.45; q[s.AddMinutes(15)] += 0.1;        // first heating 2 kW
            q[s.AddMinutes(60)] += 0.4; q[s.AddMinutes(75)] += 0.05; // second heating after a pump-only gap
        });
        var dishwasher = new Appliance { Name = "Opvaskemaskine", Icon = "🍽️", KwhPerRun = 1.0, DurationHours = 2 };

        var r = DayAnalyzer.Analyze(Day, q, p, Spot, [dishwasher], [], []);

        r.Events.Should().ContainSingle();
        var e = r.Events[0];
        e.Label.Should().Be("Opvaskemaskine");
        e.Sure.Should().Be(DayAnalyzer.Certainty.Likely);
        e.Kwh.Should().BeApproximately(1.0, 0.1); // a low tail after the last heating is left in the base
        r.BaseKw.Should().BeApproximately(0.2, 0.01);
        r.TotalKwh.Should().BeApproximately(r.EnergyKr, 0.01); // 1 kr/kWh
    }

    [Test]
    public void A_logged_run_beats_a_guess()
    {
        var start = Day.ToDateTime(new TimeOnly(17, 30));
        var (q, p) = Flat(q => { q[start] += 0.3; q[start.AddMinutes(15)] += 0.3; });
        var run = new RunEntry { ApplianceName = "Ovn", Icon = "🔥", StartedAt = start.AddMinutes(-10) };

        var r = DayAnalyzer.Analyze(Day, q, p, Spot, [], [], [run]);

        r.Events.Should().ContainSingle();
        var e = r.Events[0];
        e.Label.Should().Be("Ovn");
        e.Sure.Should().Be(DayAnalyzer.Certainty.Registered);
    }

    [Test]
    public void Meter_tariff_is_used_when_the_meter_lists_one()
    {
        var s = new HouseholdSettings { Meter = new MeterCharges { NetTariffByHour = [.. Enumerable.Repeat(0.5, 24)] } };
        var pp = PriceBreakdownCalculator.Compute(new PricePoint { TimeDk = Day.ToDateTime(new TimeOnly(12, 0)), PriceKrPerKwh = 0 }, s);
        pp.NettarifKrPerKwh.Should().BeApproximately(0.5, 0.0001);
    }

    [Test]
    public void An_event_nothing_matches_is_a_guess_not_a_crash()
    {
        var start = Day.ToDateTime(new TimeOnly(11, 0));
        var (q, p) = Flat(q => q[start] += 0.3);

        var r = DayAnalyzer.Analyze(Day, q, p, Spot, [new Appliance { Name = "Tørretumbler", KwhPerRun = 2.5, DurationHours = 4 }], [], []);

        r.Events.Should().ContainSingle().Which.Sure.Should().Be(DayAnalyzer.Certainty.Guess);
    }
}
