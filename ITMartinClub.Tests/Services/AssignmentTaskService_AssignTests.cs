using FluentAssertions;
using ITMartinClub.Server.Data;
using ITMartinClub.Server.Data.Entities;
using ITMartinClub.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace ITMartinClub.Tests.Services;

// JoinAsync backs both "Jeg tager den" (first claim) and "Deltag også"
// (a second/third person joining an already-claimed task).
[TestFixture]
public class AssignmentTaskService_AssignTests
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

    private Assignment AddAssignment(string assignedToNames = "")
    {
        var a = new Assignment { GroupId = _groupId, Title = "Test task", AssignedToNames = assignedToNames, CreatedByName = "Admin" };
        _db.Assignments.Add(a);
        _db.SaveChanges();
        return a;
    }

    [Test]
    public async Task JoinAsync_claims_an_unassigned_task()
    {
        var t = AddAssignment();

        await _sut.JoinAsync(t.Id, _groupId, "Rico");

        (await _db.Assignments.FindAsync(t.Id))!.Assignees.Should().Equal("Rico");
    }

    [Test]
    public async Task JoinAsync_lets_a_second_person_join_an_already_claimed_task()
    {
        var t = AddAssignment(assignedToNames: "Rico");

        await _sut.JoinAsync(t.Id, _groupId, "Martin Hvidberg");

        (await _db.Assignments.FindAsync(t.Id))!.Assignees.Should().Equal("Rico", "Martin Hvidberg");
    }

    [Test]
    public async Task JoinAsync_twice_by_the_same_person_does_not_duplicate_them()
    {
        var t = AddAssignment();

        await _sut.JoinAsync(t.Id, _groupId, "Rico");
        await _sut.JoinAsync(t.Id, _groupId, "Rico");

        (await _db.Assignments.FindAsync(t.Id))!.Assignees.Should().Equal("Rico");
    }

    [Test]
    public async Task JoinAsync_against_a_task_from_a_different_group_does_nothing()
    {
        var otherGroupId = Guid.NewGuid();
        var t = new Assignment { GroupId = otherGroupId, Title = "Test task", AssignedToNames = "", CreatedByName = "Admin" };
        _db.Assignments.Add(t);
        await _db.SaveChangesAsync();

        await _sut.JoinAsync(t.Id, _groupId, "Rico");

        (await _db.Assignments.FindAsync(t.Id))!.Assignees.Should().BeEmpty();
    }

    [Test]
    public async Task JoinAsync_on_an_unknown_id_does_not_throw()
    {
        var act = async () => await _sut.JoinAsync(Guid.NewGuid(), _groupId, "Rico");
        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task JoinAsync_does_not_mark_the_task_completed()
    {
        var t = AddAssignment();

        await _sut.JoinAsync(t.Id, _groupId, "Rico");

        (await _db.Assignments.FindAsync(t.Id))!.IsCompleted.Should().BeFalse("claiming a task is not the same as finishing it");
    }
}
