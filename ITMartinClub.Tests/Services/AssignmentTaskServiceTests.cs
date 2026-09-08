using FluentAssertions;
using ITMartinClub.Server.Data;
using ITMartinClub.Server.Data.Entities;
using ITMartinClub.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace ITMartinClub.Tests.Services;

// Covers "ended" (completed) task behavior: marking a task done, and the
// daily-main-task reopen logic that used to live untestable inside
// GroupHome.razor's RefreshAsync (extracted 2026-09-06 specifically so this
// could be tested directly against real EF queries instead of a full page).
[TestFixture]
public class AssignmentTaskServiceTests
{
    private ClubDbContext _db = null!;
    private AssignmentTaskService _sut = null!;
    private Guid _groupId;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<ClubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new ClubDbContext(options);
        _sut = new AssignmentTaskService(_db, new ClubPushService());
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

        var reloaded = await _db.Assignments.FindAsync(t.Id);
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

        var reloaded = await _db.Assignments.FindAsync(t.Id);
        reloaded!.IsCompleted.Should().BeFalse("a task from another group must not be completable via this group's session");
    }

    [Test]
    public async Task CompleteAsync_does_not_touch_other_tasks_in_the_same_group()
    {
        var target = AddAssignment();
        var untouched = AddAssignment();

        await _sut.CompleteAsync(target.Id, _groupId, "Rico");

        (await _db.Assignments.FindAsync(untouched.Id))!.IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task CompleteAsync_does_not_clear_who_the_task_is_assigned_to()
    {
        var t = AddAssignment();
        t.AddAssignee("Rico");
        await _db.SaveChangesAsync();

        await _sut.CompleteAsync(t.Id, _groupId, "Rico");

        (await _db.Assignments.FindAsync(t.Id))!.Assignees.Should().Equal("Rico");
    }

    [Test]
    public async Task CompleteAsync_called_again_by_someone_else_reassigns_who_completed_it()
    {
        // Last write wins - there's no lock preventing two assignees from both
        // clicking complete in quick succession, so the second click's name
        // is what sticks. Documenting the actual behavior, not asserting it's
        // the only reasonable choice.
        var t = AddAssignment();
        await _sut.CompleteAsync(t.Id, _groupId, "Rico");

        await _sut.CompleteAsync(t.Id, _groupId, "Martin Hvidberg");

        (await _db.Assignments.FindAsync(t.Id))!.CompletedByName.Should().Be("Martin Hvidberg");
    }

    [Test]
    public async Task CompleteAsync_on_an_unknown_id_does_not_throw()
    {
        var act = async () => await _sut.CompleteAsync(Guid.NewGuid(), _groupId, "Rico");
        await act.Should().NotThrowAsync();
    }

    // ── ReopenStaleTasksAsync (daily) ────────────────────────────────────

    private static readonly IReadOnlyDictionary<Guid, IReadOnlyList<DayOfWeek>> NoWeekly = new Dictionary<Guid, IReadOnlyList<DayOfWeek>>();

    [Test]
    public async Task ReopenStaleTasksAsync_reopens_a_daily_task_completed_before_todays_local_start()
    {
        var dailyMainTaskId = Guid.NewGuid();
        var localTodayStartUtc = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
        var completedYesterday = localTodayStartUtc.AddHours(-2);
        var t = AddAssignment(mainTaskId: dailyMainTaskId, isCompleted: true, completedAt: completedYesterday);

        await _sut.ReopenStaleTasksAsync(_groupId, [dailyMainTaskId], NoWeekly, localTodayStartUtc);

        var reloaded = await _db.Assignments.FindAsync(t.Id);
        reloaded!.IsCompleted.Should().BeFalse();
        reloaded.CompletedAt.Should().BeNull();
        reloaded.CompletedByName.Should().BeEmpty();
    }

    [Test]
    public async Task ReopenStaleTasksAsync_leaves_a_daily_task_completed_today_alone()
    {
        var dailyMainTaskId = Guid.NewGuid();
        var localTodayStartUtc = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
        var completedThisMorning = localTodayStartUtc.AddHours(2);
        var t = AddAssignment(mainTaskId: dailyMainTaskId, isCompleted: true, completedAt: completedThisMorning);

        await _sut.ReopenStaleTasksAsync(_groupId, [dailyMainTaskId], NoWeekly, localTodayStartUtc);

        (await _db.Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue("it was already completed on today's local calendar day");
    }

    [Test]
    public async Task ReopenStaleTasksAsync_leaves_a_stale_completed_task_alone_when_its_main_task_is_not_recurring()
    {
        var nonDailyMainTaskId = Guid.NewGuid();
        var localTodayStartUtc = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
        var t = AddAssignment(mainTaskId: nonDailyMainTaskId, isCompleted: true, completedAt: localTodayStartUtc.AddDays(-5));

        // nonDailyMainTaskId is deliberately NOT in the daily list passed in -
        // a one-off backlog task (Bogshoppen-style) must never auto-reopen.
        await _sut.ReopenStaleTasksAsync(_groupId, [], NoWeekly, localTodayStartUtc);

        (await _db.Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task ReopenStaleTasksAsync_leaves_open_tasks_alone()
    {
        var dailyMainTaskId = Guid.NewGuid();
        var t = AddAssignment(mainTaskId: dailyMainTaskId, isCompleted: false);

        await _sut.ReopenStaleTasksAsync(_groupId, [dailyMainTaskId], NoWeekly, DateTime.UtcNow);

        var reloaded = await _db.Assignments.FindAsync(t.Id);
        reloaded!.IsCompleted.Should().BeFalse();
        reloaded.CompletedByName.Should().BeEmpty();
    }

    // ── ReopenStaleTasksAsync (weekly) ───────────────────────────────────

    [Test]
    public async Task ReopenStaleTasksAsync_reopens_a_weekly_task_completed_before_this_weeks_occurrence()
    {
        // 2026-09-10 is a Thursday.
        var weeklyMainTaskId = Guid.NewGuid();
        var thisThursdayLocalStartUtc = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var completedLastThursday = thisThursdayLocalStartUtc.AddDays(-7).AddHours(10);
        var t = AddAssignment(mainTaskId: weeklyMainTaskId, isCompleted: true, completedAt: completedLastThursday);

        await _sut.ReopenStaleTasksAsync(_groupId, [], new Dictionary<Guid, IReadOnlyList<DayOfWeek>> { [weeklyMainTaskId] = [DayOfWeek.Thursday] }, thisThursdayLocalStartUtc);

        (await _db.Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeFalse("it's Thursday again - last week's cleaning doesn't count for this week");
    }

    [Test]
    public async Task ReopenStaleTasksAsync_leaves_a_weekly_task_completed_this_week_alone_on_a_later_day()
    {
        // Completed Thursday 2026-09-10, checked on Saturday 2026-09-12 - still this week's cycle.
        var weeklyMainTaskId = Guid.NewGuid();
        var thisThursday = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var saturdayLocalStartUtc = thisThursday.AddDays(2);
        var completedThisThursday = thisThursday.AddHours(9);
        var t = AddAssignment(mainTaskId: weeklyMainTaskId, isCompleted: true, completedAt: completedThisThursday);

        await _sut.ReopenStaleTasksAsync(_groupId, [], new Dictionary<Guid, IReadOnlyList<DayOfWeek>> { [weeklyMainTaskId] = [DayOfWeek.Thursday] }, saturdayLocalStartUtc);

        (await _db.Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue("Thursday's cleaning still counts through the rest of that week");
    }

    [Test]
    public async Task ReopenStaleTasksAsync_leaves_a_weekly_task_alone_before_this_weeks_occurrence_arrives()
    {
        // Completed last Thursday, checked on the Tuesday before this week's Thursday -
        // this week's occurrence hasn't happened yet, so last week's completion still stands.
        var weeklyMainTaskId = Guid.NewGuid();
        var thisThursday = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var tuesdayBeforeLocalStartUtc = thisThursday.AddDays(-2);
        var completedLastThursday = thisThursday.AddDays(-7).AddHours(9);
        var t = AddAssignment(mainTaskId: weeklyMainTaskId, isCompleted: true, completedAt: completedLastThursday);

        await _sut.ReopenStaleTasksAsync(_groupId, [], new Dictionary<Guid, IReadOnlyList<DayOfWeek>> { [weeklyMainTaskId] = [DayOfWeek.Thursday] }, tuesdayBeforeLocalStartUtc);

        (await _db.Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue("this week's Thursday hasn't arrived yet");
    }

    [Test]
    public async Task ReopenStaleTasksAsync_handles_daily_and_weekly_main_tasks_together()
    {
        var dailyId = Guid.NewGuid();
        var weeklyId = Guid.NewGuid();
        var localTodayStartUtc = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc); // Thursday
        var staleDaily = AddAssignment(mainTaskId: dailyId, isCompleted: true, completedAt: localTodayStartUtc.AddDays(-1));
        var staleWeekly = AddAssignment(mainTaskId: weeklyId, isCompleted: true, completedAt: localTodayStartUtc.AddDays(-7));

        await _sut.ReopenStaleTasksAsync(_groupId, [dailyId], new Dictionary<Guid, IReadOnlyList<DayOfWeek>> { [weeklyId] = [DayOfWeek.Thursday] }, localTodayStartUtc);

        (await _db.Assignments.FindAsync(staleDaily.Id))!.IsCompleted.Should().BeFalse();
        (await _db.Assignments.FindAsync(staleWeekly.Id))!.IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task ReopenStaleTasksAsync_a_multi_day_weekly_task_uses_its_most_recent_configured_day()
    {
        // Recurs Monday + Thursday. 2026-09-10 is Thursday, 2026-09-07 is the
        // Monday before it. Completed on that Monday, checked on the
        // following Thursday - Monday's completion is stale by Thursday
        // (its own occurrence, the more recent of the two configured days).
        var weeklyMainTaskId = Guid.NewGuid();
        var thisThursdayLocalStartUtc = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var completedThisMonday = thisThursdayLocalStartUtc.AddDays(-3).AddHours(8);
        var t = AddAssignment(mainTaskId: weeklyMainTaskId, isCompleted: true, completedAt: completedThisMonday);

        await _sut.ReopenStaleTasksAsync(
            _groupId, [],
            new Dictionary<Guid, IReadOnlyList<DayOfWeek>> { [weeklyMainTaskId] = [DayOfWeek.Monday, DayOfWeek.Thursday] },
            thisThursdayLocalStartUtc);

        (await _db.Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeFalse("Thursday is one of its recurrence days too, and it's arrived");
    }

    [Test]
    public async Task ReopenStaleTasksAsync_a_multi_day_weekly_task_completed_on_its_most_recent_day_stays_done()
    {
        // Recurs Monday + Thursday. Completed this Thursday, checked the next day (Friday) -
        // Thursday's own completion still stands until the next Monday or Thursday arrives.
        var weeklyMainTaskId = Guid.NewGuid();
        var thisThursday = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var fridayLocalStartUtc = thisThursday.AddDays(1);
        var completedThisThursday = thisThursday.AddHours(9);
        var t = AddAssignment(mainTaskId: weeklyMainTaskId, isCompleted: true, completedAt: completedThisThursday);

        await _sut.ReopenStaleTasksAsync(
            _groupId, [],
            new Dictionary<Guid, IReadOnlyList<DayOfWeek>> { [weeklyMainTaskId] = [DayOfWeek.Monday, DayOfWeek.Thursday] },
            fridayLocalStartUtc);

        (await _db.Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue();
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

        (await _db.Assignments.FindAsync(t.Id))!.Assignees.Should().Equal("Rico");
    }

    [Test]
    public async Task DeclineAsync_does_not_complete_or_delete_the_task()
    {
        var t = AddAssignment();
        t.AddAssignee("Martin");
        await _db.SaveChangesAsync();

        await _sut.DeclineAsync(t.Id, _groupId, "Martin");

        var reloaded = await _db.Assignments.FindAsync(t.Id);
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

        (await _db.Assignments.FindAsync(t.Id))!.Assignees.Should().Equal("Rico");
    }

    [Test]
    public async Task DeclineAsync_does_nothing_when_the_task_belongs_to_a_different_group()
    {
        var otherGroupId = Guid.NewGuid();
        var t = AddAssignment(groupId: otherGroupId);
        t.AddAssignee("Martin");
        await _db.SaveChangesAsync();

        await _sut.DeclineAsync(t.Id, _groupId, "Martin");

        (await _db.Assignments.FindAsync(t.Id))!.Assignees.Should().Equal("Martin");
    }

    // ── GetDailyDoneCountsAsync ──────────────────────────────────────────

    [Test]
    public async Task GetDailyDoneCountsAsync_counts_completed_tasks_per_daily_main_task()
    {
        var dailyA = Guid.NewGuid();
        var dailyB = Guid.NewGuid();
        AddAssignment(mainTaskId: dailyA, isCompleted: true, completedAt: DateTime.UtcNow);
        AddAssignment(mainTaskId: dailyA, isCompleted: true, completedAt: DateTime.UtcNow);
        AddAssignment(mainTaskId: dailyA, isCompleted: false);
        AddAssignment(mainTaskId: dailyB, isCompleted: true, completedAt: DateTime.UtcNow);

        var counts = await _sut.GetDailyDoneCountsAsync(_groupId, [dailyA, dailyB]);

        counts[dailyA].Should().Be(2);
        counts[dailyB].Should().Be(1);
    }

    [Test]
    public async Task GetDailyDoneCountsAsync_ignores_completed_tasks_on_main_tasks_not_passed_in()
    {
        var daily = Guid.NewGuid();
        var notPassedIn = Guid.NewGuid();
        AddAssignment(mainTaskId: notPassedIn, isCompleted: true, completedAt: DateTime.UtcNow);

        var counts = await _sut.GetDailyDoneCountsAsync(_groupId, [daily]);

        counts.Should().NotContainKey(notPassedIn);
        counts.GetValueOrDefault(daily).Should().Be(0);
    }

    [Test]
    public async Task GetDailyDoneCountsAsync_returns_empty_dictionary_when_no_daily_main_tasks()
    {
        var counts = await _sut.GetDailyDoneCountsAsync(_groupId, []);
        counts.Should().BeEmpty();
    }
}
