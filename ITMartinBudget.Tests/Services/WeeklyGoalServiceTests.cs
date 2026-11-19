using FluentAssertions;
using ITMartinBudget.Application.Services;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartinBudget.Tests.Services;

// Ugemål (2026-11-19): this week, last week and a normal week inside the current half-year, against the goal.
[TestFixture]
public class WeeklyGoalServiceTests
{
    private static CategoryTransaction Tx(string date, decimal amount, string category) =>
        new(0, DateTime.Parse(date), "tekst", amount, "Andet", "p", category, TransactionScope.Private);

    [Test]
    public void This_week_last_week_and_normal_week_inside_the_second_half()
    {
        // Wed 18.11.2026: this week = Mon 16.11 - today, last week = 9.11-15.11.
        var rows = new[]
        {
            Tx("2026-06-29", -1000m, "Take away"),  // first half (and the week that starts in June) is not normal
            Tx("2026-07-08", -200m, "Take away"),
            Tx("2026-11-10", -150m, "Take away"),
            Tx("2026-11-12", 50m, "Take away"),     // a friend paying his share back lowers the week
            Tx("2026-11-16", -90m, "Take away"),
            Tx("2026-11-17", -30m, "Café"),
        };
        var s = WeeklyGoalService.Status([new WeeklyGoal("take away", 100m)], rows, new DateTime(2026, 11, 18)).Single();

        s.ThisWeek.Should().Be(90m);
        s.LastWeek.Should().Be(100m);
        s.Left.Should().Be(10m);
        s.Over.Should().BeFalse();
        s.Percent.Should().Be(90);
        // Full weeks from Mon 6.7 to Mon 16.11 = 19 weeks; 200 + 150 - 50 = 300 kr.
        s.AverageWeek.Should().Be(Math.Round(300m / 19, 0));
    }

    [Test]
    public void Over_the_goal_comes_first_and_the_bar_stops_at_100()
    {
        var rows = new[] { Tx("2026-11-17", -300m, "Sport"), Tx("2026-11-17", -10m, "Bar") };
        var list = WeeklyGoalService.Status([new WeeklyGoal("Bar", 100m), new WeeklyGoal("Sport", 200m)], rows, new DateTime(2026, 11, 18));

        list[0].Category.Should().Be("Sport");
        list[0].Over.Should().BeTrue();
        list[0].Percent.Should().Be(100);
    }

    [Test]
    public void Half_starts_1_january_or_1_july()
    {
        WeeklyGoalService.HalfStart(new DateTime(2026, 3, 4)).Should().Be(new DateTime(2026, 1, 1));
        WeeklyGoalService.HalfStart(new DateTime(2026, 11, 19)).Should().Be(new DateTime(2026, 7, 1));
    }

    [Test]
    public void A_goal_needs_a_category_and_an_amount()
    {
        FluentActions.Invoking(() => WeeklyGoal.Create(" ", 100m)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => WeeklyGoal.Create("Bar", 0m)).Should().Throw<ArgumentException>();
        WeeklyGoal.Create(" Bar ", 149.6m).Should().Be(new WeeklyGoal("Bar", 150m));
    }
}
