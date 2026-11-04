using FluentAssertions;
using ITMartinClub.Domain;
using ITMartinClub.Infrastructure.Persistence;
using ITMartinClub.Application;
using Microsoft.EntityFrameworkCore;

namespace ITMartinClub.Tests.Services;

// The two remaining task actions: removing a task entirely (the ✕ button),
// and moving an "Ikke kategoriseret" task under a real main task (the
// dropdown AssignmentRow renders for tasks with no MainTaskId).
[TestFixture]
public class AssignmentTaskService_DeleteAndRecategorizeTests
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

    private Assignment AddAssignment(Guid? groupId = null, Guid? mainTaskId = null)
    {
        var a = new Assignment { GroupId = groupId ?? _groupId, MainTaskId = mainTaskId, Title = "Test task", AssignedToNames = "", CreatedByName = "Admin" };
        _db.Assignments.Add(a);
        _db.SaveChanges();
        return a;
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────

    [Test]
    public async Task DeleteAsync_removes_the_task()
    {
        var t = AddAssignment();

        await _sut.DeleteAsync(t.Id, _groupId);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id)).Should().BeNull();
    }

    [Test]
    public async Task DeleteAsync_leaves_other_tasks_in_the_group_alone()
    {
        var target = AddAssignment();
        var other = AddAssignment();

        await _sut.DeleteAsync(target.Id, _groupId);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(other.Id)).Should().NotBeNull();
    }

    [Test]
    public async Task DeleteAsync_against_a_task_from_a_different_group_does_not_delete_it()
    {
        var otherGroupId = Guid.NewGuid();
        var t = AddAssignment(groupId: otherGroupId);

        await _sut.DeleteAsync(t.Id, _groupId);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id)).Should().NotBeNull("a task must not be deletable from a different group's session");
    }

    [Test]
    public async Task DeleteAsync_on_an_unknown_id_does_not_throw()
    {
        var act = async () => await _sut.DeleteAsync(Guid.NewGuid(), _groupId);
        await act.Should().NotThrowAsync();
    }

    // ── AssignMainTaskAsync (re-categorize an "Ikke kategoriseret" task) ──

    [Test]
    public async Task AssignMainTaskAsync_moves_an_uncategorized_task_under_a_main_task()
    {
        var mainTaskId = Guid.NewGuid();
        var t = AddAssignment(mainTaskId: null);

        await _sut.AssignMainTaskAsync(t.Id, _groupId, mainTaskId);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.MainTaskId.Should().Be(mainTaskId);
    }

    [Test]
    public async Task AssignMainTaskAsync_can_move_a_task_from_one_main_task_to_another()
    {
        var originalMainTaskId = Guid.NewGuid();
        var newMainTaskId = Guid.NewGuid();
        var t = AddAssignment(mainTaskId: originalMainTaskId);

        await _sut.AssignMainTaskAsync(t.Id, _groupId, newMainTaskId);

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.MainTaskId.Should().Be(newMainTaskId);
    }

    [Test]
    public async Task AssignMainTaskAsync_against_a_task_from_a_different_group_does_nothing()
    {
        var otherGroupId = Guid.NewGuid();
        var t = AddAssignment(groupId: otherGroupId, mainTaskId: null);

        await _sut.AssignMainTaskAsync(t.Id, _groupId, Guid.NewGuid());

        (await TestBoard.Fresh(_db).Assignments.FindAsync(t.Id))!.MainTaskId.Should().BeNull();
    }

    [Test]
    public async Task AssignMainTaskAsync_on_an_unknown_id_does_not_throw()
    {
        var act = async () => await _sut.AssignMainTaskAsync(Guid.NewGuid(), _groupId, Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }
}
