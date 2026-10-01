using FluentAssertions;
using ITMartinElPriser.Core;

namespace ITMartinElPriser.Tests;

[TestFixture]
public class ChargeComparerTests
{
    private static ChargeProfile Profile(double kwh, params (ChargeSpot s, double pct)[] shares)
    {
        var p = new ChargeProfile { KwhPerMonth = kwh, ForeningKrPerKwh = 2.38 };
        foreach (var s in Enum.GetValues<ChargeSpot>()) p.SharePct[s] = 0;
        foreach (var (s, pct) in shares) p.SharePct[s] = pct;
        return p;
    }

    [Test]
    public void Association_driver_pays_per_kwh_without_subscription()
    {
        var p = Profile(230, (ChargeSpot.Forening, 90), (ChargeSpot.Offentlig, 5), (ChargeSpot.Lyn, 5));
        var best = ChargeComparer.Compare(p).First();

        best.Id.Should().Be("ingen");
        // 207 kWh × 2,38 + 11,5 × 2,99 (Norlys) + 11,5 × 3,29 (Norlys lyn)
        best.MonthlyKr.Should().BeApproximately(207 * 2.38 + 11.5 * 2.99 + 11.5 * 3.29, 0.5);
    }

    [Test]
    public void Clever_one_covers_the_association_charger_only_when_clever_runs_it()
    {
        var p = Profile(500, (ChargeSpot.Forening, 100));
        p.ForeningOperator = "clever";
        p.ForeningKrPerKwh = 2.99;
        var one = ChargeComparer.Compare(p).Single(o => o.Id == "clever-one");
        one.MonthlyKr.Should().BeApproximately(999 + 500 * 0.29, 0.5);   // free charging + energitillæg

        p.ForeningOperator = "egen";
        p.ForeningKrPerKwh = 2.38;
        var notCovered = ChargeComparer.Compare(p).Single(o => o.Id == "clever-one");
        notCovered.MonthlyKr.Should().BeApproximately(999 + 500 * 2.38, 0.5);
    }

    [Test]
    public void Included_kwh_are_used_before_the_normal_price()
    {
        var p = Profile(300, (ChargeSpot.Offentlig, 100));
        var norlys = ChargeComparer.Compare(p).Single(o => o.Id == "norlys-ude-15");
        norlys.MonthlyKr.Should().BeApproximately(499 + 50 * 2.99, 0.5);
    }

    [Test]
    public void Home_charging_needs_a_box_unless_the_product_brings_one()
    {
        var p = Profile(200, (ChargeSpot.Hjem, 100));
        p.HasHomeBox = false;
        p.HomeKrPerKwh = 1.20;
        var all = ChargeComparer.Compare(p);

        all.Single(o => o.Id == "ingen").Problem.Should().NotBeNull();
        all.Single(o => o.Id == "clever-you").MonthlyKr.Should().BeApproximately(240 + 200 * 1.20, 0.5);
        all.First().Problem.Should().BeNull("a workable option is always ranked first");
    }

    [Test]
    public void Employer_paid_work_charging_costs_nothing()
    {
        var p = Profile(200, (ChargeSpot.Arbejde, 100));
        ChargeComparer.Compare(p).Single(o => o.Id == "ingen").MonthlyKr.Should().Be(0);
    }
}
