using FluentAssertions;
using ITMartinClub.Server.Data;
using ITMartinClub.Server.Data.Entities;
using ITMartinClub.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace ITMartinClub.Tests.Services;

// SaveAsync covers two GroupHome/Assignments UI actions at once: "Opret
// opgave" (create, editingId null) and editing an existing task (editingId
// set) - both go through the same method, differing only in that branch.
[TestFixture]
public class AssignmentTaskService_SaveTests
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

    // ── Opret opgave (create) ────────────────────────────────────────────

    [Test]
    public async Task Create_stores_the_task_under_the_right_group_and_creator()
    {
        await _sut.SaveAsync(_groupId, null, null, null, null,
            "Skriv RABAT på skilt", null, [], null, null, "Rico");

        var t = _db.Assignments.Single();
        t.GroupId.Should().Be(_groupId);
        t.CreatedByName.Should().Be("Rico");
        t.IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task Create_prefixes_the_title_with_the_main_tasks_name()
    {
        var mainTaskId = Guid.NewGuid();

        await _sut.SaveAsync(_groupId, null, mainTaskId, "Salg", null,
            "Skriv RABAT på skilt", null, [], null, null, "Rico");

        _db.Assignments.Single().Title.Should().Be("Salg: Skriv RABAT på skilt");
    }

    [Test]
    public async Task Create_does_not_prefix_the_title_when_there_is_no_main_task()
    {
        await _sut.SaveAsync(_groupId, null, null, null, null,
            "Frigør lager", null, [], null, null, "Rico");

        _db.Assignments.Single().Title.Should().Be("Frigør lager");
    }

    [Test]
    public async Task Create_does_not_double_prefix_a_title_that_already_starts_with_the_main_task_name()
    {
        var mainTaskId = Guid.NewGuid();

        await _sut.SaveAsync(_groupId, null, mainTaskId, "Salg", null,
            "Salg: allerede prefixet", null, [], null, null, "Rico");

        _db.Assignments.Single().Title.Should().Be("Salg: allerede prefixet");
    }

    [Test]
    public async Task Create_stores_the_storage_location_when_given()
    {
        var locationId = Guid.NewGuid();

        await _sut.SaveAsync(_groupId, null, null, null, locationId,
            "Flyt kasser", null, [], null, null, "Rico");

        _db.Assignments.Single().StorageLocationId.Should().Be(locationId);
    }

    [Test]
    public async Task Create_joins_multiple_chosen_assignees_with_a_semicolon()
    {
        await _sut.SaveAsync(_groupId, null, null, null, null,
            "Kontakt auktionsfirma", null, ["Rico", "Martin Hvidberg"], null, null, "Admin");

        _db.Assignments.Single().AssignedToNames.Should().Be("Rico;Martin Hvidberg");
    }

    [Test]
    public async Task Create_with_no_assignees_leaves_the_task_unclaimed()
    {
        await _sut.SaveAsync(_groupId, null, null, null, null,
            "Kontakt auktionsfirma", null, [], null, null, "Admin");

        _db.Assignments.Single().AssignedToNames.Should().BeEmpty();
    }

    [Test]
    public async Task Create_stores_the_due_date_when_given()
    {
        var due = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);

        await _sut.SaveAsync(_groupId, null, null, null, null,
            "Aflever bøger", null, [], due, null, "Rico");

        _db.Assignments.Single().DueDate.Should().Be(due);
    }

    [Test]
    public async Task Create_with_no_due_date_leaves_it_null()
    {
        await _sut.SaveAsync(_groupId, null, null, null, null,
            "Aflever bøger", null, [], null, null, "Rico");

        _db.Assignments.Single().DueDate.Should().BeNull();
    }

    // ── Description ──────────────────────────────────────────────────────

    [Test]
    public async Task Description_is_trimmed()
    {
        await _sut.SaveAsync(_groupId, null, null, null, null,
            "Titel", "  lidt ekstra kontekst  ", [], null, null, "Rico");

        _db.Assignments.Single().Description.Should().Be("lidt ekstra kontekst");
    }

    [Test]
    public async Task Description_null_is_stored_as_null()
    {
        await _sut.SaveAsync(_groupId, null, null, null, null,
            "Titel", null, [], null, null, "Rico");

        _db.Assignments.Single().Description.Should().BeNull();
    }

    [Test]
    public async Task Description_whitespace_only_is_stored_as_null_not_blank()
    {
        await _sut.SaveAsync(_groupId, null, null, null, null,
            "Titel", "   ", [], null, null, "Rico");

        _db.Assignments.Single().Description.Should().BeNull();
    }

    // ── "Start" - hvornår opgaven vises (ScheduledFor: today vs tomorrow) ──

    [Test]
    public async Task Start_left_null_means_the_task_shows_immediately()
    {
        await _sut.SaveAsync(_groupId, null, null, null, null,
            "Vis i dag", null, [], null, scheduledForUtc: null, "Rico");

        _db.Assignments.Single().ScheduledFor.Should().BeNull();
    }

    [Test]
    public async Task Start_set_to_a_future_date_holds_the_task_back()
    {
        var tomorrow = DateTime.UtcNow.Date.AddDays(1);

        await _sut.SaveAsync(_groupId, null, null, null, null,
            "Vis i morgen", null, [], null, scheduledForUtc: tomorrow, "Rico");

        _db.Assignments.Single().ScheduledFor.Should().Be(tomorrow);
    }

    // ── Editing an existing task ─────────────────────────────────────────

    [Test]
    public async Task Update_changes_the_existing_tasks_fields_in_place()
    {
        var existing = new Assignment { GroupId = _groupId, Title = "Gammel titel", CreatedByName = "Admin", AssignedToNames = "" };
        _db.Assignments.Add(existing);
        await _db.SaveChangesAsync();

        await _sut.SaveAsync(_groupId, existing.Id, null, null, null,
            "Ny titel", "ny beskrivelse", ["Rico"], null, null, "Admin");

        var reloaded = await _db.Assignments.FindAsync(existing.Id);
        reloaded!.Title.Should().Be("Ny titel");
        reloaded.Description.Should().Be("ny beskrivelse");
        reloaded.AssignedToNames.Should().Be("Rico");
    }

    [Test]
    public async Task Update_does_not_create_a_second_task()
    {
        var existing = new Assignment { GroupId = _groupId, Title = "Gammel titel", CreatedByName = "Admin", AssignedToNames = "" };
        _db.Assignments.Add(existing);
        await _db.SaveChangesAsync();

        await _sut.SaveAsync(_groupId, existing.Id, null, null, null,
            "Ny titel", null, [], null, null, "Admin");

        _db.Assignments.Count().Should().Be(1);
    }

    [Test]
    public async Task Update_against_a_task_from_a_different_group_does_nothing()
    {
        var otherGroupId = Guid.NewGuid();
        var existing = new Assignment { GroupId = otherGroupId, Title = "Original", CreatedByName = "Admin", AssignedToNames = "" };
        _db.Assignments.Add(existing);
        await _db.SaveChangesAsync();

        await _sut.SaveAsync(_groupId, existing.Id, null, null, null,
            "Forsøgt overskrevet", null, [], null, null, "Nogen");

        (await _db.Assignments.FindAsync(existing.Id))!.Title.Should().Be("Original");
    }

    [Test]
    public async Task Update_on_an_unknown_editing_id_does_not_throw_or_create_anything()
    {
        var act = async () => await _sut.SaveAsync(_groupId, Guid.NewGuid(), null, null, null,
            "Titel", null, [], null, null, "Rico");

        await act.Should().NotThrowAsync();
        _db.Assignments.Should().BeEmpty();
    }
}
