using FluentAssertions;
using ITMartinHusk.Domain;

namespace ITMartinHusk.Tests;

// Regler: the weekly job-search reminder - this week from Friday, last week Monday-Thursday, until "Det er skrevet ind".
public class ReglerTests
{
    private static readonly DateTime Fri = new(2026, 11, 6, 13, 0, 0);   // uge 45

    [Test]
    public void The_week_is_this_week_from_friday_and_last_week_before()
    {
        RuleBook.WeekNumber(RuleBook.JobWeekMonday(Fri)).Should().Be(45);
        RuleBook.WeekNumber(RuleBook.JobWeekMonday(new DateTime(2026, 11, 4, 10, 0, 0))).Should().Be(44);   // Wednesday -> last week
        RuleBook.WeekKey(new DateTime(2026, 10, 26)).Should().Be("2026-W44");
    }

    [Test]
    public void Off_without_a_number_and_gone_when_written()
    {
        RuleBook.JobLogDue(0, [], Fri).Should().BeFalse();
        RuleBook.JobLogDue(2, [], Fri).Should().BeTrue();
        RuleBook.JobLogDue(2, ["2026-W45"], Fri).Should().BeFalse();
    }

    [Test]
    public void Friday_noon_then_monday_nine_once_each()
    {
        var sent = new HashSet<string>();
        RuleBook.JobLogPushes(2, [], Fri.Date.AddHours(11), sent).Should().BeEmpty();   // before noon
        var fri = RuleBook.JobLogPushes(2, [], Fri, sent);
        fri.Should().ContainSingle().Which.Key.Should().Be("joblog|2026-W45|fre");
        sent.Add(fri[0].Key);
        RuleBook.JobLogPushes(2, [], Fri.AddHours(2), sent).Should().BeEmpty();          // only once

        var mon = new DateTime(2026, 11, 9, 9, 30, 0);
        RuleBook.JobLogPushes(2, [], mon, sent).Single().Message.Title.Should().Be("Uge 45 mangler i Joblog");
        RuleBook.JobLogPushes(2, ["2026-W45"], mon, sent).Should().BeEmpty();
        RuleBook.JobLogPushes(2, [], mon.Date.AddHours(22), new HashSet<string>()).Should().BeEmpty();   // never at night
    }
}
