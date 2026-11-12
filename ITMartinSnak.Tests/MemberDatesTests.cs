using FluentAssertions;
using ITMartinSnak.Domain;

namespace ITMartinSnak.Tests;

// 2026-11-12 family room: everyone adds their own dates, yearly days (birthdays) come back, a morning push on the day.
public class MemberDatesTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 12);

    private static TaskItem Birthday(string day, string when = "") =>
        GroupRules.NewTask([], "Julius har fødselsdag", "", when, Now, null, day, false, "Vibz", true)!;

    [Test]
    public void A_member_is_remembered_as_the_one_who_added_it()
    {
        Birthday("2008-04-12").By.Should().Be("Vibz");
        GroupRules.NewTask([], "Tandlæge", "", "14:00", Now, "Eigil", "2026-11-13")!.By.Should().BeEmpty();
    }

    [Test]
    public void Yearly_needs_a_day_and_is_never_daily()
    {
        GroupRules.NewTask([], "x", "", "", Now, null, "", false, "Vibz", true)!.Yearly.Should().BeFalse();
        GroupRules.NewTask([], "x", "", "", Now, null, "2026-04-12", true, "Vibz", true)!.Yearly.Should().BeFalse();
    }

    [Test]
    public void A_yearly_day_is_on_the_same_day_and_month_every_year()
    {
        var t = Birthday("2008-11-12");
        GroupRules.IsOn(t, Today).Should().BeTrue();
        GroupRules.IsToday(t, Today).Should().BeTrue();
        GroupRules.IsOn(t, new DateOnly(2027, 11, 12)).Should().BeTrue();
        GroupRules.IsOn(t, new DateOnly(2026, 11, 13)).Should().BeFalse();
    }

    [Test]
    public void The_29th_of_February_falls_on_the_28th_in_other_years()
    {
        var t = Birthday("2004-02-29");
        GroupRules.IsOn(t, new DateOnly(2027, 2, 28)).Should().BeTrue();
        GroupRules.IsOn(t, new DateOnly(2028, 2, 29)).Should().BeTrue();
        GroupRules.IsOn(t, new DateOnly(2028, 2, 28)).Should().BeFalse();
    }

    [Test]
    public void The_next_day_moves_to_next_year_when_this_years_has_passed()
    {
        GroupRules.NextDay(Birthday("2008-04-12"), Today).Should().Be("2027-04-12");
        GroupRules.NextDay(Birthday("2008-12-24"), Today).Should().Be("2026-12-24");
        GroupRules.NextDay(Birthday("2008-11-12"), Today).Should().Be("2026-11-12");
    }

    [Test]
    public void Ticking_a_yearly_day_only_counts_for_that_year()
    {
        var t = GroupRules.SetDone(Birthday("2008-11-12"), true, Today);
        GroupRules.IsDoneOn(t, Today).Should().BeTrue();
        GroupRules.IsDoneOn(t, new DateOnly(2027, 11, 12)).Should().BeFalse();
        t.Done.Should().BeFalse();
    }

    [Test]
    public void A_yearly_day_without_a_clock_gets_one_morning_push()
    {
        var t = Birthday("2008-11-12");
        GroupRules.DueReminders(t, new DateTime(2026, 11, 12, 8, 2, 0)).Should().Equal(0);
        GroupRules.DueReminders(t, new DateTime(2026, 11, 12, 9, 0, 0)).Should().BeEmpty();
        GroupRules.DueReminders(t, new DateTime(2026, 11, 13, 8, 2, 0)).Should().BeEmpty();
        GroupRules.ReminderText(t, 0).Should().Be("I dag: Julius har fødselsdag");
    }

    [Test]
    public void A_yearly_day_with_a_clock_reminds_before_like_other_tasks()
    {
        var t = Birthday("2008-11-12", "18:00");
        GroupRules.DueReminders(t, new DateTime(2026, 11, 12, 17, 0, 0)).Should().Equal(60);
        GroupRules.DueReminders(t, new DateTime(2026, 11, 12, 8, 2, 0)).Should().BeEmpty();
    }

    [Test]
    public void Plain_tasks_still_remind_as_before()
    {
        var t = GroupRules.NewTask([], "Tandlæge", "", "14:00", Now, "Eigil", "2026-11-12")!;
        GroupRules.DueReminders(t, new DateTime(2026, 11, 12, 13, 31, 0)).Should().Equal(30);
        var noDay = GroupRules.NewTask([], "Ring", "", "14:00", Now)!;
        GroupRules.DueReminders(noDay, new DateTime(2026, 11, 12, 13, 31, 0)).Should().BeEmpty();
    }

    [Test]
    public void Only_the_host_or_the_one_who_added_it_may_change_it()
    {
        var t = Birthday("2008-04-12");
        GroupRules.MayChange(t, "vibz", false).Should().BeTrue();
        GroupRules.MayChange(t, "Julius", false).Should().BeFalse();
        GroupRules.MayChange(t, "Julius", true).Should().BeTrue();
        var hosts = GroupRules.NewTask([], "Tandlæge", "", "", Now)!;
        GroupRules.MayChange(hosts, "", false).Should().BeFalse();
    }

    [Test]
    public void Editing_keeps_yearly_unless_changed_and_drops_it_without_a_day()
    {
        var t = Birthday("2008-04-12");
        GroupRules.Edit(t, "Julius 18 år", "", "")!.Yearly.Should().BeTrue();
        GroupRules.Edit(t, "x", "", "", null, "", null, null)!.Yearly.Should().BeFalse();
        GroupRules.Edit(t, "x", "", "", null, null, null, false)!.Yearly.Should().BeFalse();
    }
}
