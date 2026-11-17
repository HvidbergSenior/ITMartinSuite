using FluentAssertions;
using ITMartinSnak.Domain;

namespace ITMartinSnak.Tests;

// 2026-11-17 Sonja room: whoever closes a task says how ("Vaccination d. 17.11") and may put it in the calendar.
public class ClosedHowTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 17);

    private static TaskItem Task(string day = "2026-11-17", bool daily = false) =>
        GroupRules.NewTask([], "Vaccination", "", "10:00", Now, null, day, daily)!;

    [Test]
    public void Closing_keeps_how_who_and_the_day()
    {
        var t = GroupRules.SetDone(Task(), true, Today, "  Vaccination givet d. 17.11 ", "Mette", true);
        t.Done.Should().BeTrue();
        t.DoneNote.Should().Be("Vaccination givet d. 17.11");
        t.DoneBy.Should().Be("Mette");
        t.DoneDay.Should().Be("2026-11-17");
        t.InCalendar.Should().BeTrue();
    }

    [Test]
    public void Reopening_forgets_how_it_was_closed()
    {
        var t = GroupRules.SetDone(GroupRules.SetDone(Task(), true, Today, "x", "Mette", true), false, Today);
        t.Done.Should().BeFalse();
        (t.DoneNote + t.DoneBy + t.DoneDay).Should().BeEmpty();
        t.InCalendar.Should().BeFalse();
    }

    [Test]
    public void A_daily_task_keeps_the_note_for_today_only()
    {
        var t = GroupRules.SetDone(Task("", true), true, Today, "Pillerne taget kl. 8", "Bent");
        t.DoneOn.Should().Be("2026-11-17");
        t.DoneNote.Should().Be("Pillerne taget kl. 8");
        GroupRules.IsDoneOn(t, Today.AddDays(1)).Should().BeFalse();
    }

    [Test]
    public void The_note_is_cut_to_a_short_line()
    {
        GroupRules.SetDone(Task(), true, Today, new string('a', 500), "Mette").DoneNote.Length.Should().Be(GroupRules.MaxDoneNote);
    }

    [Test]
    public void Next_time_must_be_a_real_day_after_today()
    {
        GroupRules.CleanNextDay("2027-05-17", Today).Should().Be("2027-05-17");
        GroupRules.CleanNextDay("2026-11-17", Today).Should().BeNull();
        GroupRules.CleanNextDay("17/5", Today).Should().BeNull();
        GroupRules.CleanNextDay(null, Today).Should().BeNull();
    }
}
