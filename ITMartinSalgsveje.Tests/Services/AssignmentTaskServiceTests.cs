using FluentAssertions;
using ITMartinSalgsveje.Domain;
using ITMartinSalgsveje.Infrastructure.Persistence;
using ITMartinSalgsveje.Application;
using Microsoft.EntityFrameworkCore;

namespace ITMartinSalgsveje.Tests.Services;

// Covers "ended" (completed) task behavior: marking a task done, and the
// daily-main-task reopen logic that used to live untestable inside
// GroupHome.razor's RefreshAsync (extracted 2026-09-06 specifically so this
// could be tested directly against real EF queries instead of a full page).
[TestFixture]
public class AssignmentTaskServiceTests
{
    private ClubDb _db = null!;
    private TaskBoard _sut = null!;
    private Guid _groupId;

    [SetUp]
    public void SetUp()
    {
        (_db, _sut) = TestBoard.Create();
        _groupId = Guid.NewGuid();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private Assignment AddAssignment(Guid? groupId = null, Guid? mainTaskId = null, bool isCompleted = false, DateTime? completedAt = null)
    {
        var a = new Assignment
        {
            GroupId = groupId ?? _groupId,
            MainTaskId = mainTaskId,
            Title = "Test task",
            AssignedToNames = "",
            CreatedByName = "Admin",
            IsCompleted = isCompleted,
            CompletedAt = completedAt,
            CompletedByName = isCompleted ? "Admin" : "",
        };
        _db.Assignments.Add(a);
        _db.SaveChanges();
        return a;
    }

    // ── CompleteAsync ────────────────────────────────────────────────────

    [Test]
    public async Task CompleteAsync_marks_the_task_completed_with_who_and_when()
    {
        var t = AddAssignment();

        await _sut.CompleteAsync(t.Id, _groupId, "Rico");

        var reloaded = await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id);
        reloaded!.IsCompleted.Should().BeTrue();
        reloaded.CompletedByName.Should().Be("Rico");
        reloaded.CompletedAt.Should().NotBeNull();
        reloaded.CompletedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task CompleteAsync_does_nothing_when_the_task_belongs_to_a_different_group()
    {
        var otherGroupId = Guid.NewGuid();
        var t = AddAssignment(groupId: otherGroupId);

        await _sut.CompleteAsync(t.Id, _groupId, "Rico");

        var reloaded = await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id);
        reloaded!.IsCompleted.Should().BeFalse("a task from another group must not be completable via this group's session");
    }

    [Test]
    public async Task CompleteAsync_does_not_touch_other_tasks_in_the_same_group()
    {
        var target = AddAssignment();
        var untouched = AddAssignment();

        await _sut.CompleteAsync(target.Id, _groupId, "Rico");

        (await TestBoard.Fresh(_db).Assignments.FindAsync(untouched.Id))!.IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task CompleteAsync_does_not_clear_who_the_task_is_assigned_to()
    {
        var t = AddAssignment();
        t.AddAssignee("Rico");
        await _db.SaveChangesAsync();

        await _sut.CompleteAsync(t.Id, _groupId, "Rico");

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.Assignees.Should().Equal("Rico");
    }

    [Test]
    public async Task CompleteAsync_called_again_by_someone_else_keeps_who_completed_it_first()
    {
        // Was "last write wins" (2026-09). TaskBoard now ignores a second click on a task that is already done,
        // so the person who actually did it keeps the credit.
        var t = AddAssignment();
        await _sut.CompleteAsync(t.Id, _groupId, "Rico");

        await _sut.CompleteAsync(t.Id, _groupId, "Martin Hvidberg");

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.CompletedByName.Should().Be("Rico");
    }

    [Test]
    public async Task CompleteAsync_on_an_unknown_id_does_not_throw()
    {
        var act = async () => await _sut.CompleteAsync(Guid.NewGuid(), _groupId, "Rico");
        await act.Should().NotThrowAsync();
    }

    // ── ReopenStaleAsync (daily) ────────────────────────────────────

    private static MainTask Daily(Guid id) => new() { Id = id, GroupId = Guid.NewGuid(), IsDaily = true };

    private static MainTask Weekly(Guid id, params DayOfWeek[] days) =>
        new() { Id = id, GroupId = Guid.NewGuid(), RecurrenceDaysRaw = string.Join(',', days) };

    private static MainTask Monthly(Guid id, params DayOfWeek[] days) =>
        new() { Id = id, GroupId = Guid.NewGuid(), IsMonthly = true, RecurrenceDaysRaw = string.Join(',', days) };

    private static MainTask Biweekly(Guid id, params DayOfWeek[] days) =>
        new() { Id = id, GroupId = Guid.NewGuid(), IsBiweekly = true, RecurrenceDaysRaw = string.Join(',', days) };

    [Test]
    public async Task ReopenStaleAsync_reopens_a_daily_task_completed_before_todays_local_start()
    {
        var dailyMainTaskId = Guid.NewGuid();
        var localTodayStartUtc = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
        var completedYesterday = localTodayStartUtc.AddHours(-2);
        var t = AddAssignment(mainTaskId: dailyMainTaskId, isCompleted: true, completedAt: completedYesterday);

        await _sut.ReopenStaleAsync(_groupId, [Daily(dailyMainTaskId)], localTodayStartUtc);

        var reloaded = await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id);
        reloaded!.IsCompleted.Should().BeFalse();
        reloaded.CompletedAt.Should().BeNull();
        reloaded.CompletedByName.Should().BeEmpty();
    }

    [Test]
    public async Task ReopenStaleAsync_leaves_a_daily_task_completed_today_alone()
    {
        var dailyMainTaskId = Guid.NewGuid();
        var localTodayStartUtc = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
        var completedThisMorning = localTodayStartUtc.AddHours(2);
        var t = AddAssignment(mainTaskId: dailyMainTaskId, isCompleted: true, completedAt: completedThisMorning);

        await _sut.ReopenStaleAsync(_groupId, [Daily(dailyMainTaskId)], localTodayStartUtc);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue("it was already completed on today's local calendar day");
    }

    [Test]
    public async Task ReopenStaleAsync_leaves_a_stale_completed_task_alone_when_its_main_task_is_not_recurring()
    {
        var nonDailyMainTaskId = Guid.NewGuid();
        var localTodayStartUtc = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
        var t = AddAssignment(mainTaskId: nonDailyMainTaskId, isCompleted: true, completedAt: localTodayStartUtc.AddDays(-5));

        // nonDailyMainTaskId is deliberately NOT in the recurring list passed in -
        // a one-off backlog task (Bogshoppen-style) must never auto-reopen.
        await _sut.ReopenStaleAsync(_groupId, [], localTodayStartUtc);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task ReopenStaleAsync_leaves_open_tasks_alone()
    {
        var dailyMainTaskId = Guid.NewGuid();
        var t = AddAssignment(mainTaskId: dailyMainTaskId, isCompleted: false);

        await _sut.ReopenStaleAsync(_groupId, [Daily(dailyMainTaskId)], DateTime.UtcNow);

        var reloaded = await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id);
        reloaded!.IsCompleted.Should().BeFalse();
        reloaded.CompletedByName.Should().BeEmpty();
    }

    // ── ReopenStaleAsync (weekly) ───────────────────────────────────

    [Test]
    public async Task ReopenStaleAsync_reopens_a_weekly_task_completed_before_this_weeks_occurrence()
    {
        // 2026-09-10 is a Thursday.
        var weeklyMainTaskId = Guid.NewGuid();
        var thisThursdayLocalStartUtc = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var completedLastThursday = thisThursdayLocalStartUtc.AddDays(-7).AddHours(10);
        var t = AddAssignment(mainTaskId: weeklyMainTaskId, isCompleted: true, completedAt: completedLastThursday);

        await _sut.ReopenStaleAsync(_groupId, [Weekly(weeklyMainTaskId, DayOfWeek.Thursday)], thisThursdayLocalStartUtc);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeFalse("it's Thursday again - last week's cleaning doesn't count for this week");
    }

    [Test]
    public async Task ReopenStaleAsync_leaves_a_weekly_task_completed_this_week_alone_on_a_later_day()
    {
        // Completed Thursday 2026-09-10, checked on Saturday 2026-09-12 - still this week's cycle.
        var weeklyMainTaskId = Guid.NewGuid();
        var thisThursday = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var saturdayLocalStartUtc = thisThursday.AddDays(2);
        var completedThisThursday = thisThursday.AddHours(9);
        var t = AddAssignment(mainTaskId: weeklyMainTaskId, isCompleted: true, completedAt: completedThisThursday);

        await _sut.ReopenStaleAsync(_groupId, [Weekly(weeklyMainTaskId, DayOfWeek.Thursday)], saturdayLocalStartUtc);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue("Thursday's cleaning still counts through the rest of that week");
    }

    [Test]
    public async Task ReopenStaleAsync_leaves_a_weekly_task_alone_before_this_weeks_occurrence_arrives()
    {
        // Completed last Thursday, checked on the Tuesday before this week's Thursday -
        // this week's occurrence hasn't happened yet, so last week's completion still stands.
        var weeklyMainTaskId = Guid.NewGuid();
        var thisThursday = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var tuesdayBeforeLocalStartUtc = thisThursday.AddDays(-2);
        var completedLastThursday = thisThursday.AddDays(-7).AddHours(9);
        var t = AddAssignment(mainTaskId: weeklyMainTaskId, isCompleted: true, completedAt: completedLastThursday);

        await _sut.ReopenStaleAsync(_groupId, [Weekly(weeklyMainTaskId, DayOfWeek.Thursday)], tuesdayBeforeLocalStartUtc);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue("this week's Thursday hasn't arrived yet");
    }

    [Test]
    public async Task ReopenStaleAsync_handles_daily_and_weekly_main_tasks_together()
    {
        var dailyId = Guid.NewGuid();
        var weeklyId = Guid.NewGuid();
        var localTodayStartUtc = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc); // Thursday
        var staleDaily = AddAssignment(mainTaskId: dailyId, isCompleted: true, completedAt: localTodayStartUtc.AddDays(-1));
        var staleWeekly = AddAssignment(mainTaskId: weeklyId, isCompleted: true, completedAt: localTodayStartUtc.AddDays(-7));

        await _sut.ReopenStaleAsync(_groupId, [Daily(dailyId), Weekly(weeklyId, DayOfWeek.Thursday)], localTodayStartUtc);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(staleDaily.Id))!.IsCompleted.Should().BeFalse();
        (await TestBoard.Fresh(_db).Assignments.FindAsync(staleWeekly.Id))!.IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task ReopenStaleAsync_a_multi_day_weekly_task_uses_its_most_recent_configured_day()
    {
        // Recurs Monday + Thursday. 2026-09-10 is Thursday, 2026-09-07 is the
        // Monday before it. Completed on that Monday, checked on the
        // following Thursday - Monday's completion is stale by Thursday
        // (its own occurrence, the more recent of the two configured days).
        var weeklyMainTaskId = Guid.NewGuid();
        var thisThursdayLocalStartUtc = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var completedThisMonday = thisThursdayLocalStartUtc.AddDays(-3).AddHours(8);
        var t = AddAssignment(mainTaskId: weeklyMainTaskId, isCompleted: true, completedAt: completedThisMonday);

        await _sut.ReopenStaleAsync(
            _groupId, [Weekly(weeklyMainTaskId, DayOfWeek.Monday, DayOfWeek.Thursday)],
            thisThursdayLocalStartUtc);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeFalse("Thursday is one of its recurrence days too, and it's arrived");
    }

    [Test]
    public async Task ReopenStaleAsync_a_multi_day_weekly_task_completed_on_its_most_recent_day_stays_done()
    {
        // Recurs Monday + Thursday. Completed this Thursday, checked the next day (Friday) -
        // Thursday's own completion still stands until the next Monday or Thursday arrives.
        var weeklyMainTaskId = Guid.NewGuid();
        var thisThursday = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var fridayLocalStartUtc = thisThursday.AddDays(1);
        var completedThisThursday = thisThursday.AddHours(9);
        var t = AddAssignment(mainTaskId: weeklyMainTaskId, isCompleted: true, completedAt: completedThisThursday);

        await _sut.ReopenStaleAsync(
            _groupId, [Weekly(weeklyMainTaskId, DayOfWeek.Monday, DayOfWeek.Thursday)],
            fridayLocalStartUtc);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue();
    }

    // ── ReopenStaleAsync (monthly) ──────────────────────────────────

    [Test]
    public async Task ReopenStaleAsync_reopens_a_monthly_task_completed_last_month()
    {
        var monthlyMainTaskId = Guid.NewGuid();
        var startOfSeptemberUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var completedInAugust = startOfSeptemberUtc.AddDays(-3);
        var t = AddAssignment(mainTaskId: monthlyMainTaskId, isCompleted: true, completedAt: completedInAugust);

        await _sut.ReopenStaleAsync(_groupId, [Monthly(monthlyMainTaskId)], startOfSeptemberUtc.AddDays(9));

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeFalse("it's a new calendar month since it was completed");
    }

    [Test]
    public async Task ReopenStaleAsync_leaves_a_monthly_task_completed_earlier_this_month_alone()
    {
        var monthlyMainTaskId = Guid.NewGuid();
        var localTodayStartUtc = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        var completedEarlierThisMonth = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc);
        var t = AddAssignment(mainTaskId: monthlyMainTaskId, isCompleted: true, completedAt: completedEarlierThisMonth);

        await _sut.ReopenStaleAsync(_groupId, [Monthly(monthlyMainTaskId)], localTodayStartUtc);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue("still the same calendar month");
    }

    [Test]
    public async Task ReopenStaleAsync_a_monthly_task_with_a_day_picked_reopens_on_the_first_occurrence_of_that_day()
    {
        // September 2026's 1st is a Tuesday, so "first Wednesday" is 2026-09-02.
        var monthlyMainTaskId = Guid.NewGuid();
        var startOfSeptemberUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var completedInAugust = startOfSeptemberUtc.AddDays(-2);
        var t = AddAssignment(mainTaskId: monthlyMainTaskId, isCompleted: true, completedAt: completedInAugust);

        // Checked on the first Wednesday itself.
        await _sut.ReopenStaleAsync(_groupId, [Monthly(monthlyMainTaskId, DayOfWeek.Wednesday)], startOfSeptemberUtc.AddDays(1));

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeFalse("the month's first Wednesday has arrived");
    }

    [Test]
    public async Task ReopenStaleAsync_a_monthly_task_with_a_day_picked_leaves_it_done_before_that_days_first_occurrence()
    {
        var monthlyMainTaskId = Guid.NewGuid();
        var startOfSeptemberUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc); // Tuesday
        var completedOnTheFirst = startOfSeptemberUtc.AddHours(9);
        var t = AddAssignment(mainTaskId: monthlyMainTaskId, isCompleted: true, completedAt: completedOnTheFirst);

        // Checked on the 1st itself - the first Wednesday (2026-09-02) hasn't arrived yet.
        await _sut.ReopenStaleAsync(_groupId, [Monthly(monthlyMainTaskId, DayOfWeek.Wednesday)], startOfSeptemberUtc);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue("this month's first Wednesday hasn't happened yet");
    }

    // ── ReopenStaleAsync (biweekly) ─────────────────────────────────

    [Test]
    public async Task ReopenStaleAsync_reopens_a_biweekly_task_completed_15_days_ago()
    {
        var biweeklyMainTaskId = Guid.NewGuid();
        var localTodayStartUtc = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        var completed15DaysAgo = localTodayStartUtc.AddDays(-15);
        var t = AddAssignment(mainTaskId: biweeklyMainTaskId, isCompleted: true, completedAt: completed15DaysAgo);

        await _sut.ReopenStaleAsync(_groupId, [Biweekly(biweeklyMainTaskId)], localTodayStartUtc);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeFalse("15 days have passed since it was last done");
    }

    [Test]
    public async Task ReopenStaleAsync_leaves_a_biweekly_task_completed_10_days_ago_alone()
    {
        var biweeklyMainTaskId = Guid.NewGuid();
        var localTodayStartUtc = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        var completed10DaysAgo = localTodayStartUtc.AddDays(-10);
        var t = AddAssignment(mainTaskId: biweeklyMainTaskId, isCompleted: true, completedAt: completed10DaysAgo);

        await _sut.ReopenStaleAsync(_groupId, [Biweekly(biweeklyMainTaskId)], localTodayStartUtc);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue("only 10 of the 14 days have passed");
    }

    [Test]
    public async Task ReopenStaleAsync_a_biweekly_task_with_a_day_picked_stays_done_on_the_off_week()
    {
        // BiweeklyEpochMonday is 2024-01-01. 2025-12-31 is a Wednesday whose
        // week bucket ((date - epoch).Days / 7, floored) is 104, an even
        // ("active") bucket - verified by direct calculation, not assumed,
        // since which absolute weeks land on the active parity is an
        // implementation detail of the epoch, not something callers should
        // have to reason about by eye.
        var biweeklyMainTaskId = Guid.NewGuid();
        var activeWednesday = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var completedOnActiveWednesday = activeWednesday.AddHours(9);
        var t = AddAssignment(mainTaskId: biweeklyMainTaskId, isCompleted: true, completedAt: completedOnActiveWednesday);

        var offWeekWednesday = activeWednesday.AddDays(7);
        await _sut.ReopenStaleAsync(_groupId, [Biweekly(biweeklyMainTaskId, DayOfWeek.Wednesday)], offWeekWednesday);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue("this Wednesday is the off week - it only reopens every other Wednesday");
    }

    [Test]
    public async Task ReopenStaleAsync_a_biweekly_task_with_a_day_picked_reopens_on_the_next_active_occurrence()
    {
        var biweeklyMainTaskId = Guid.NewGuid();
        var activeWednesday = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var completedOnActiveWednesday = activeWednesday.AddHours(9);
        var t = AddAssignment(mainTaskId: biweeklyMainTaskId, isCompleted: true, completedAt: completedOnActiveWednesday);

        var nextActiveWednesday = activeWednesday.AddDays(14);
        await _sut.ReopenStaleAsync(_groupId, [Biweekly(biweeklyMainTaskId, DayOfWeek.Wednesday)], nextActiveWednesday);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeFalse("the next active Wednesday, two weeks later, has arrived");
    }

    // ── DeclineAsync ─────────────────────────────────────────────────────

    [Test]
    public async Task DeclineAsync_removes_the_declining_member_from_assignees()
    {
        var t = AddAssignment();
        t.AddAssignee("Martin");
        t.AddAssignee("Rico");
        await _db.SaveChangesAsync();

        await _sut.DeclineAsync(t.Id, _groupId, "Martin");

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.Assignees.Should().Equal("Rico");
    }

    [Test]
    public async Task DeclineAsync_does_not_complete_or_delete_the_task()
    {
        var t = AddAssignment();
        t.AddAssignee("Martin");
        await _db.SaveChangesAsync();

        await _sut.DeclineAsync(t.Id, _groupId, "Martin");

        var reloaded = await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id);
        reloaded.Should().NotBeNull();
        reloaded!.IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task DeclineAsync_on_someone_not_assigned_does_nothing()
    {
        var t = AddAssignment();
        t.AddAssignee("Rico");
        await _db.SaveChangesAsync();

        await _sut.DeclineAsync(t.Id, _groupId, "Martin");

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.Assignees.Should().Equal("Rico");
    }

    [Test]
    public async Task DeclineAsync_does_nothing_when_the_task_belongs_to_a_different_group()
    {
        var otherGroupId = Guid.NewGuid();
        var t = AddAssignment(groupId: otherGroupId);
        t.AddAssignee("Martin");
        await _db.SaveChangesAsync();

        await _sut.DeclineAsync(t.Id, _groupId, "Martin");

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.Assignees.Should().Equal("Martin");
    }

    // ── done this cycle (was GetDailyDoneCountsAsync, now part of the home board) ──

    [Test]
    public async Task HomeAsync_counts_done_tasks_per_daily_main_task_and_ignores_open_ones()
    {
        var group = new Group { Id = _groupId, Slug = "testhold", Name = "Testhold" };
        var dailyA = new MainTask { GroupId = _groupId, Title = "Køkken", IsDaily = true };
        var dailyB = new MainTask { GroupId = _groupId, Title = "Have", IsDaily = true };
        _db.Groups.Add(group);
        _db.MainTasks.AddRange(dailyA, dailyB);
        _db.SaveChanges();
        AddAssignment(mainTaskId: dailyA.Id, isCompleted: true, completedAt: DateTime.UtcNow);
        AddAssignment(mainTaskId: dailyA.Id, isCompleted: true, completedAt: DateTime.UtcNow);
        AddAssignment(mainTaskId: dailyA.Id, isCompleted: false);
        AddAssignment(mainTaskId: dailyB.Id, isCompleted: true, completedAt: DateTime.UtcNow);

        var board = await _sut.HomeAsync(group);

        board.DoneThisCycle[dailyA.Id].Should().Be(2);
        board.DoneThisCycle[dailyB.Id].Should().Be(1);
        board.OpenNow.Should().ContainSingle();
    }
}
