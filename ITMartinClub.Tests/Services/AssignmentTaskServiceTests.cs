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

    // ── ReopenStaleDailyTasksAsync ───────────────────────────────────────

    [Test]
    public async Task ReopenStaleDailyTasksAsync_reopens_a_task_completed_before_todays_local_start()
    {
        var dailyMainTaskId = Guid.NewGuid();
        var localTodayStartUtc = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
        var completedYesterday = localTodayStartUtc.AddHours(-2);
        var t = AddAssignment(mainTaskId: dailyMainTaskId, isCompleted: true, completedAt: completedYesterday);

        await _sut.ReopenStaleDailyTasksAsync(_groupId, [dailyMainTaskId], localTodayStartUtc);

        var reloaded = await _db.Assignments.FindAsync(t.Id);
        reloaded!.IsCompleted.Should().BeFalse();
        reloaded.CompletedAt.Should().BeNull();
        reloaded.CompletedByName.Should().BeEmpty();
    }

    [Test]
    public async Task ReopenStaleDailyTasksAsync_leaves_a_task_completed_today_alone()
    {
        var dailyMainTaskId = Guid.NewGuid();
        var localTodayStartUtc = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
        var completedThisMorning = localTodayStartUtc.AddHours(2);
        var t = AddAssignment(mainTaskId: dailyMainTaskId, isCompleted: true, completedAt: completedThisMorning);

        await _sut.ReopenStaleDailyTasksAsync(_groupId, [dailyMainTaskId], localTodayStartUtc);

        (await _db.Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue("it was already completed on today's local calendar day");
    }

    [Test]
    public async Task ReopenStaleDailyTasksAsync_leaves_a_stale_completed_task_alone_when_its_main_task_is_not_daily()
    {
        var nonDailyMainTaskId = Guid.NewGuid();
        var localTodayStartUtc = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
        var t = AddAssignment(mainTaskId: nonDailyMainTaskId, isCompleted: true, completedAt: localTodayStartUtc.AddDays(-5));

        // nonDailyMainTaskId is deliberately NOT in the daily list passed in -
        // a one-off backlog task (Bogshoppen-style) must never auto-reopen.
        await _sut.ReopenStaleDailyTasksAsync(_groupId, [], localTodayStartUtc);

        (await _db.Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task ReopenStaleDailyTasksAsync_leaves_open_tasks_alone()
    {
        var dailyMainTaskId = Guid.NewGuid();
        var t = AddAssignment(mainTaskId: dailyMainTaskId, isCompleted: false);

        await _sut.ReopenStaleDailyTasksAsync(_groupId, [dailyMainTaskId], DateTime.UtcNow);

        var reloaded = await _db.Assignments.FindAsync(t.Id);
        reloaded!.IsCompleted.Should().BeFalse();
        reloaded.CompletedByName.Should().BeEmpty();
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
