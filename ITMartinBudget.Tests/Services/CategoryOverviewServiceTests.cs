using FluentAssertions;
using ITMartinBudget.Application.Services;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Tests.Services;

// The pure part of the Kategorier page: groups by the part before " · ",
// month sums, income apart, savings moves out, and the Privat/Forretning filter.
[TestFixture]
public class CategoryOverviewServiceTests
{
    private static CategoryTransaction Tx(string date, decimal amount, string category, TransactionScope scope = TransactionScope.Private) =>
        new(0, DateTime.Parse(date), "tekst", amount, "Andet", "p", category, scope);

    private static readonly CategoryTransaction[] Sample =
    [
        Tx("2026-01-05", -100m, "Børn · Eigil"),
        Tx("2026-01-20", -50m, "Børn · Eigil"),
        Tx("2026-02-03", -350m, "Børn · Bertil"),
        Tx("2026-02-10", -478.59m, "Transport · Brændstof"),
        Tx("2026-02-28", 15000m, "Indtægt · Løn"),
        Tx("2026-03-01", -5000m, "Opsparing (flytning)"),
        Tx("2026-03-02", -2000m, "Forretning · Lager (Boxit)", TransactionScope.Business),
    ];

    [Test]
    public void Sub_categories_fold_under_their_group_with_month_sums()
    {
        var o = CategoryOverviewService.Build(Sample, ScopeFilter.All);

        var boern = o.Spending.Single(g => g.Name == "Børn");
        boern.Sum.Should().Be(-500m);
        boern.Lines.Select(l => l.Sub).Should().BeEquivalentTo(["Bertil", "Eigil"]);
        boern.Lines.Single(l => l.Sub == "Eigil").ByMonth[1].Should().Be(-150m);
        o.Months.Should().Equal(1, 2, 3);
    }

    [Test]
    public void Income_is_apart_and_savings_moves_are_left_out()
    {
        var o = CategoryOverviewService.Build(Sample, ScopeFilter.All);

        o.Income.Single().Name.Should().Be("Indtægt");
        o.Spending.Should().NotContain(g => g.Name.StartsWith("Opsparing"));
        o.Spending.Should().NotContain(g => g.Name == "Indtægt");
    }

    [Test]
    public void Business_filter_shows_only_the_business_costs()
    {
        CategoryOverviewService.Build(Sample, ScopeFilter.Business).Spending
            .Should().ContainSingle(g => g.Name == "Forretning");
        CategoryOverviewService.Build(Sample, ScopeFilter.Private).Spending
            .Should().NotContain(g => g.Name == "Forretning");
    }

    [Test]
    public void Clean_puts_one_space_around_the_dot()
    {
        CategoryOverviewService.Clean("  Børn ·Eigil  ").Should().Be("Børn · Eigil");
        CategoryOverviewService.Clean("Transport  ·  Brændstof").Should().Be("Transport · Brændstof");
        CategoryOverviewService.Clean("Pension").Should().Be("Pension");
    }

    [Test]
    public void New_category_is_business_only_under_Forretning_and_existing_keeps_its_scope()
    {
        CategoryOverviewService.ScopeFor("Forretning · Telefon", Sample).Should().Be(TransactionScope.Business);
        CategoryOverviewService.ScopeFor("Kæledyr", Sample).Should().Be(TransactionScope.Private);
        CategoryOverviewService.ScopeFor("Forretning · Lager (Boxit)", Sample).Should().Be(TransactionScope.Business);
    }

    [Test]
    public void Pace_gives_average_purchase_average_week_and_this_week()
    {
        // Wed 18.11.2026: this week = Mon 16.11 - today. Two full weeks before it (2.11 and 9.11).
        var rows = new[]
        {
            Tx("2026-11-03", -300m, "Mad og indkøb · Dagligvarer"),
            Tx("2026-11-05", -100m, "Mad og indkøb · Dagligvarer"),
            Tx("2026-11-10", -200m, "Mad og indkøb · Dagligvarer"),
            Tx("2026-11-12", 50m, "Mad og indkøb · Dagligvarer"),   // a refund is not a purchase
            Tx("2026-11-17", -400m, "Mad og indkøb · Dagligvarer"),
        };
        var pace = CategoryOverviewService.Pace(rows, new DateTime(2026, 11, 18))!;

        pace.AveragePurchase.Should().Be(250m);
        pace.WeeksCounted.Should().Be(2);
        pace.AverageWeek.Should().Be(300m);
        pace.ThisWeek.Should().Be(400m);
        pace.LeftThisWeek.Should().Be(-100m);
        pace.IsAbove(400m).Should().BeTrue();
        pace.IsAbove(100m).Should().BeFalse();
    }

    [Test]
    public void Pace_is_null_without_purchases()
    {
        CategoryOverviewService.Pace([Tx("2026-11-12", 50m, "X")], new DateTime(2026, 11, 18)).Should().BeNull();
    }

    [Test]
    public void Biggest_spending_group_comes_first()
    {
        var o = CategoryOverviewService.Build(Sample, ScopeFilter.All);
        o.Spending.First().Name.Should().Be("Forretning");
    }

    // 2026-11-18 Sortér betalinger: what is left, oldest first; the payee's own category is the first choice ("Bliv i ...").
    [Test]
    public void The_sort_queue_shows_unsorted_payments_oldest_first_and_counts_progress()
    {
        CategoryTransaction T(int id, string d, decimal a, bool byHand = false) =>
            new(id, DateTime.Parse(d), "MobilePay Eigil", a, "", "mobilepay eigil", "Børn · Eigil", TransactionScope.Private, byHand);
        var q = CategoryOverviewService.Queue([T(1, "2026-11-16", -136.50m), T(2, "2026-03-01", -60m), T(3, "2026-05-01", -40m, byHand: true), T(4, "2026-06-01", 200m)], "Børn · Eigil");
        q.Total.Should().Be(3);   // money IN from him is not sorted
        q.Sorted.Should().Be(1);
        q.Left.Select(t => t.Id).Should().Equal(2, 1);
        q.Choices[0].Should().Be("Børn · Eigil");
        q.Choices.Should().Contain("Mad og indkøb · Dagligvarer").And.Contain("Transport · Brændstof");
    }
}
