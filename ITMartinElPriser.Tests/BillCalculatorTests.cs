using FluentAssertions;
using ITMartinElPriser.Server.Services;

namespace ITMartinElPriser.Tests;

[TestFixture]
public class BillCalculatorTests
{
    private static readonly HouseholdSettings S = new() { ShowAllIn = true, GridCompanyId = "n1", SupplierId = "nrgi-time" };

    private static List<PricedPoint> PricedDay(DateOnly day, double spot) =>
        Enumerable.Range(0, 96).Select(i => PriceBreakdownCalculator.Compute(
            new PricePoint { TimeDk = day.ToDateTime(TimeOnly.MinValue).AddMinutes(15 * i), PriceKrPerKwh = spot }, S)).ToList();

    [Test]
    public void Bill_sums_each_component_and_projects_the_rest_of_the_month()
    {
        var sep = new DateOnly(2026, 9, 1);
        // Two measured days of 1 kWh every hour at 0.50 kr spot.
        var kwh = new Dictionary<DateTime, double>();
        foreach (var d in new[] { sep, sep.AddDays(1) })
            for (var h = 0; h < 24; h++) kwh[d.ToDateTime(new TimeOnly(h, 0))] = 1.0;
        var priced = PricedDay(sep, 0.50).Concat(PricedDay(sep.AddDays(1), 0.50)).ToList();

        var bill = BillCalculator.Build(sep, sep.AddMonths(1), kwh, priced, S);

        bill.Kwh.Should().Be(48);
        bill.DaysMeasured.Should().Be(2);
        bill.DaysInPeriod.Should().Be(30);
        bill.IsComplete.Should().BeFalse();

        var line = bill.Lines.ToDictionary(l => l.Id, l => l.Kr);
        line["spot"].Should().BeApproximately(24.0, 0.01);
        line["afgift"].Should().BeApproximately(48 * 0.008, 0.001);
        line["energinet"].Should().BeApproximately(48 * 0.115, 0.001);
        line["net"].Should().BeApproximately(48 * 0.1074, 0.01, "September is summer -> lav rate");
        line["tillaeg"].Should().BeApproximately(48 * 0.072, 0.01);
        line["abo-lev"].Should().Be(23.2);
        line["abo-net"].Should().Be(36.92);
        var exMoms = bill.Lines.Where(l => l.Id != "moms").Sum(l => l.Kr);
        line["moms"].Should().BeApproximately(exMoms * 0.25, 0.001);
        bill.TotalKr.Should().BeApproximately(exMoms * 1.25, 0.001);

        // Projection: fixed fees once, usage x 15, moms on top of everything.
        var variableEx = exMoms - 23.2 - 36.92;
        bill.ProjectedKr.Should().BeApproximately((23.2 + 36.92 + variableEx * 15) * 1.25, 0.01);
        bill.ProjectedKwh.Should().BeApproximately(720, 0.01);
    }

    [Test]
    public void Hours_without_a_price_or_outside_the_month_are_ignored()
    {
        var sep = new DateOnly(2026, 9, 1);
        var kwh = new Dictionary<DateTime, double>
        {
            [sep.ToDateTime(new TimeOnly(3, 0))] = 2.0,
            [sep.AddDays(5).ToDateTime(new TimeOnly(3, 0))] = 99, // no prices for that day
            [sep.AddMonths(1).ToDateTime(new TimeOnly(3, 0))] = 99, // October
        };
        var bill = BillCalculator.Build(sep, sep.AddMonths(1), kwh, PricedDay(sep, 1.0), S);
        bill.Kwh.Should().Be(2.0);
        bill.DaysMeasured.Should().Be(1);
    }

    [Test]
    public void Every_line_explains_itself()
    {
        var bill = BillCalculator.Build(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1), new Dictionary<DateTime, double>(), [], S);
        bill.Lines.Should().HaveCount(8);
        foreach (var l in bill.Lines)
        {
            l.What.Should().NotBeNullOrWhiteSpace(l.Id);
            l.Influence.Should().NotBeNullOrWhiteSpace(l.Id);
            l.Recipient.Should().NotBeNullOrWhiteSpace(l.Id);
        }
        bill.Lines.Where(l => l.IsFixed).Select(l => l.Id).Should().BeEquivalentTo(["abo-lev", "abo-net"]);
    }
}
